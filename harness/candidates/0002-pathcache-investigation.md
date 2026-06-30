# Candidate 0002: Path-cache collapse — terrain-invalidation over-scan

- Status: QUEUED (built, Stage-4 review ADVANCE; pending in-game A/B measurement)
- Target: `PathPatches.NavGrid_UpdateGraph_Patch.Postfix` + `PathCacher.InvalidateRegion` — `FastTrack/PathPatches/NavPatches.cs:549-595`, `FastTrack/PathPatches/PathCacher.cs:118-165`
- Risk class: Postfix (game method) + static helper (no IL/transpiler, no threading change)
- Gating flag: `FastTrackOptions.CachePaths` (default on)
- Collision with Peter's rewrites: no — this hook is **net-new on the fork** (commits `dd9508c`/`53be7c8`/`b181abc`/`b3db7d7`/`2401a9d`); upstream Fast Track has no terrain-change invalidation at all.

## Summary of the evidence

Late-game capture (`harness/profiling-baseline.md`): Path Cache hit% is `99.8%` for a 36 s warm-up, collapses to `4.1%` for ~218 s (85% of the capture), then recovers to `77.6%` at the tail. During the collapse, `BrainScheduler` (24.6k us/s) and `Navigator+StatesInstance` (22.5k us/s) dominate — pathing runs mostly **uncached**. This is the #1 hot path.

## How the cache actually decides hit vs miss (verified against the current decompile)

The fork's machinery (not the original upstream serial-number scheme; this is the Aquatic-ported `PathGrid`-keyed design that was already present at the `c4abcf1` baseline):

- `PathCacher` is a `ConcurrentDictionary<PathGrid, double>` mapping each grid to an expiry = `now + 6.0` real seconds (`now` from `Time.timeAsDouble`, set once/frame in `Global_Update_Patch.Prefix`).
- **Hit test** (`PathCacher.CheckCache`, the *only* place the `PATH_CACHE` ratio metric is logged): `IsValid(grid) && (!grid.applyOffset || center(grid) == queryCell)`, where `center(grid) = XYToCell(rootX + w/2, rootY + h/2)`. After a full probe at `root_cell`, `BeginUpdate` centers the window so `center == root_cell`. So a bounded (per-navigator) grid is a hit only if the navigator's query cell is still the cell it last fully probed from — i.e. **the navigator hasn't moved a cell** and the entry hasn't been invalidated or expired.
- **Set-valid** (the only warm-up path): `PathProber_RunAsync_Patch` transpiles `path_grid.EndUpdate()` inside the 10-arg `PathProber.Run` into a custom `EndUpdate` that calls `SetValid(grid, true)`. The async worker (`AsyncPathProber.WorkOrder.Execute`, decompile line 53) calls exactly this 10-arg `Run`, and `TakeResult` installs the freshly-validated pool grid as `nav.PathGrid`. So a miss re-warms the entry. Confirmed the set-side works because the metric **recovers** to 77.6% at the tail — a broken set-side could not recover.

### Transpiler/patch targeting is correct on Aquatic

The 10-arg overload point flagged in the prior static audit is matched **exactly**: the fork's `PathProber_RunAsync_Patch` `[HarmonyPatch]` signature `(int, PathFinderAbilities, NavGrid, NavType, PathGrid, ushort, PotentialScratchPad, PotentialList, PotentialPath.Flags, List<int>)` equals the current game body in `harness/decompiled/.../PathProber.cs:23`. The 2-arg `Run(Navigator, List<int>)` is matched by `PathProber_RunSync_Patch`. `EndUpdate()` is still present and called in the 10-arg body. The transpiler is not mis-targeting — hypothesis "(a) the CachePaths transpiler mis-targets the new Run" is **not** supported.

## Diagnosis

Best-supported: **(a), but a narrower form than "the transpiler is broken."** The transpiler is fine. The fork-introduced regression is in the **net-new terrain-invalidation hook**, which over-invalidates.

`NavGrid.UpdateGraph()` (decompile `NavGrid.cs:494-521`) accumulates dirty cells across the whole map between nav update cycles, expands each by the nav update range, then fires `OnNavGridUpdateComplete(dirty_nav_cells)`. The fork's `NavGrid_UpdateGraph_Patch.Postfix` computes a **single union AABB over the entire dirty-cell list** and calls `InvalidateRegion(minX,minY,maxX,maxY)`, which drops every cached grid whose window overlaps that box.

When dirty cells are spatially **scattered** — exactly the late-game Aquatic case: liquid sloshing in several places, digging/building in multiple spots, all accumulated into one list over a ~200 ms nav cycle — the union box balloons to nearly the whole map even though only a few isolated cells actually changed. The per-grid AABB test then matches essentially every grid, so the **entire path cache is wiped on every frame that has any scattered terrain churn**. This depresses the hit rate far below what upstream (which never invalidated on terrain) would show, and it scales precisely with map churn. The bimodal pattern fits: settled map → few/local dirty cells → cache survives (warm-up, tail); active map → map-wide scattered churn → full wipe every frame (collapse).

Critically, this hook can **only lower** the hit rate relative to upstream (it adds an invalidation source, never a hit). So narrowing it to invalidate only grids overlapping an *actual* dirty cell is a strict improvement, provided correctness (the stuck-swim-nav-critter fix from `dd9508c`) is preserved.

The same-frame dedup added in `b181abc`/`b3db7d7` does **not** address this: it collapses the ~12 identical per-navgrid repeats of one change, but does nothing about scattered cells *within* a single navgrid's accumulated list. The fix below is therefore not already applied.

### Honest confidence and what would falsify it

Confidence: **medium.** Static analysis cannot separate, in the metric, a miss caused by `!IsValid` (invalidation/expiry — fixable here) from a miss caused by `center != queryCell` (the navigator genuinely moved — benign, hypothesis (b)). The `PATH_CACHE` `RatioProfiler` logs only hit/total, not the reason. The bimodal-with-recovery shape is consistent with **both** (a) over-invalidation and (b) a colony where most navigators are simply in motion.

It is **not** (c) measurement-artifact: the metric is a straightforward hit/total ratio over real `CheckCache` calls; 4% means 4% of probes were served from cache.

## Discriminating measurement (do this before building the fix)

Cheapest decisive A/B — temporarily disable only the net-new hook and re-capture the same colony:

- Make `NavGrid_UpdateGraph_Patch.Prepare()` return `false` (or comment the `InvalidateRegion` call), keep everything else identical, run the same save under Debug Metrics.
- If hit% during the active window climbs back toward the warm-up/upstream regime → **(a) confirmed**, build the fix. Accept the temporary swim-nav-critter staleness for the measurement run only.
- If hit% stays ~4% → the misses are navigator-movement (`center` mismatch) → **(b) benign**, do not ship the fix; the real lever is reducing probe *cost*/frequency, not cache invalidation.

Stronger instrumented variant (if the toggle is ambiguous): split `CheckCache` into two counters — `missInvalid` (`!IsValid`) vs `missMoved` (valid but `center != cell`) — and log both, plus count `InvalidateRegion` calls and mean bbox area per second. This directly attributes the collapse.

## Patch (built)

Built per the design below. Real field/method names confirmed against `PathCacher.cs`/`NavPatches.cs` (not guessed): `PathGrid.rootX`, `.rootY`, `.widthInCells`, `.heightInCells`, `.applyOffset`; `Grid.IsValidCell`, `Grid.CellToXY`; `pathCache` is `ConcurrentDictionary<PathGrid, double>`, mutated only from this main-thread nav-update path (unchanged assumption).

Window convention: **half-open**, `[root, root+size)`. Confirmed against `PathCacher.CheckCache`'s own center calc (`XYToCell(rootX + widthInCells/2, rootY + heightInCells/2)`) and against the old `InvalidateRegion`'s AABB-overlap test (`gx <= maxX && gx+width > minX && ...`), both of which are half-open-consistent. `PathCacheGeometry.CellInWindow` and its unit test match this.

### 1. New file `FastTrack/PathPatches/PathCacheGeometry.cs`
Pure, game-dependency-free static method:
```csharp
public static bool CellInWindow(int cx, int cy, int rootX, int rootY, int width, int height) {
    return cx >= rootX && cx < rootX + width && cy >= rootY && cy < rootY + height;
}
```

### 2. `PathCacher.InvalidateCells(List<int> dirtyCells)` (replaces `InvalidateRegion`, which is now dead — its only caller was the Postfix below)
- Empty/null list: no-op (unchanged).
- `dirtyCells.Count > INVALIDATE_ALL_THRESHOLD` (const `8192`, post-expansion-aware — see code comment and the Stage-4 note): `pathCache.Clear()` and return, matching the old code's conservative behavior for genuine map-wide events.
- Otherwise: filters to `Grid.IsValidCell` cells (same guard the old Postfix had), then does ONE pass over `pathCache`: full-map grids (`!applyOffset`) invalidate unconditionally (unchanged); bounded grids invalidate iff `PathCacheGeometry.CellInWindow(cx, cy, grid.rootX, grid.rootY, grid.widthInCells, grid.heightInCells)` is true for at least one dirty cell (first hit breaks to the next grid).
- Same-frame dedup is **reimplemented as a literal cell-set dedup** (`HashSet<int> cellsInvalidatedThisFrame`, reset when `now != lastInvalidateTime`) instead of porting the old bbox-containment dedup. Reason (documented in code): once invalidation is precise per-cell membership rather than a bbox-overlap over-approximation, "new bbox ⊆ old bbox" no longer proves "every grid relevant to the new cells was already invalidated" — a grid can sit inside the old box without containing any of the old call's actual dirty cells, yet contain one of the new call's. Tracking the literal already-tested cell set is the correctness-preserving equivalent of the same optimization (collapsing the ~16 nested per-navgrid repeats of one terrain change into effectively one dictionary scan).

### 3. `NavGrid_UpdateGraph_Patch.Postfix` (`FastTrack/PathPatches/NavPatches.cs`)
Before:
```csharp
internal static void Postfix(List<int> __0) {
    var dirtyCells = __0;
    int n;
    if (dirtyCells == null || (n = dirtyCells.Count) < 1) return;
    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
    for (int i = 0; i < n; i++) {
        int cell = dirtyCells[i];
        if (!Grid.IsValidCell(cell)) continue;
        Grid.CellToXY(cell, out int x, out int y);
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
    }
    if (minX > maxX) return;
    PathCacher.InvalidateRegion(minX, minY, maxX, maxY);
}
```
After:
```csharp
internal static void Postfix(List<int> __0) {
    PathCacher.InvalidateCells(__0);
}
```
The min/max bbox construction is fully removed (now lives, in precise per-cell form, inside `InvalidateCells`).

Cost note: per-grid x dirty-cell window tests (hundreds x hundreds ~ 1e4-1e5 cheap int compares/frame) are far cheaper than the full path probes this avoids; a single avoided 10-arg `Run` pays for the whole scan.

## Static gates (Stage 3) — re-run after build
- Compiles: **yes**. `dotnet build FastTrack/FastTrack.csproj -c Debug -p:Platform=Mergedown` → 0 errors (30 pre-existing warnings, none from this change).
- Unit test: **pass**. `harness/tests/PathCacheTests` (mirrors `RyuTests` structure exactly — net8.0 xUnit island walled off via empty `Directory.Build.props`/`.targets`, links only `PathCacheGeometry.cs`). 12/12 `PathCacheGeometryTests` pass (boundary cases at root 0,0 and at a non-zero-root window to exercise the offset, not just the size).
- IL verify: n/a (no transpiler in this patch, Postfix-only).
- Thread-safety check: unchanged — `pathCache` is still only mutated from this main-thread nav-update path; the new `cellsInvalidatedThisFrame`/`newDirtyCells` statics follow the same (non-thread-safe, main-thread-only) assumption as the old `lastInvalidateMinX/Y/MaxX/Y` fields they replace.
- Save-compat: n/a (no serialized layout change).

## Predicted impact

If (a) holds: restores hit rate from ~4% toward the warm regime during churn, directly cutting the two largest hot-path classes in the profile (`BrainScheduler` 24.6k us/s + `Navigator+StatesInstance` 22.5k us/s, both running mostly uncached during the collapse). Potentially the single largest win available in this capture. If (b) holds: ~0 — and the fix should not ship.

## Risk class detail

Postfix on `NavGrid.UpdateGraph(List<int>)` (already patched here) plus a static `PathCacher` helper. No transpiler/IL. `pathCache` is a `ConcurrentDictionary` already mutated from this main-thread nav-update path. No serialized layout change → save-compat n/a. Correctness boundary: must still invalidate every grid overlapping a real dirty cell, or the `dd9508c` stuck-swim-nav-critter bug returns — the membership test preserves this; only grids in the *empty gaps between* scattered clusters stop being needlessly dropped.

## Static gates (Stage 3)
- Compiles: na (not yet written — gated on measurement)
- Unit test: na (game-coupled; the AABB-membership helper is the one extractable, testable unit)
- IL verify: na (no transpiler in the proposed fix)
- Thread-safety check: unchanged — same main-thread caller, same `ConcurrentDictionary`
- Save-compat: na

## Review (Stage 4)
**ADVANCE** (Opus, fresh context). Key verifications:
- The membership predicate `CellInWindow` is **provably identical** to the game's own cell-to-grid mapping `PathGrid.OffsetCell` (decompile `PathGrid.cs:250-262`): a bounded grid covers exactly the cells where `OffsetCell != -1`, i.e. `x>=rootX && x<rootX+widthInCells && y>=rootY && y<rootY+heightInCells`. No off-by-one possible; the `dd9508c` stuck-critter regression cannot return from a geometry error. (`BeginUpdate` sets root to true origin, width/height to full extent — confirmed.)
- The `HashSet<int>` cell-set dedup is sound and the implementer's reasoning correct (old bbox-nesting dedup *is* unsound under precise membership). Not a regression vs old code under main-thread sequencing.
- `InvalidateRegion` removal loses no behavior (full-map grids now invalidated explicitly/unconditionally).
- Cost: early-break on first hit, full-map short-circuit before the cell loop, no pathological blowup below threshold. Thread-safety unchanged (ConcurrentDictionary, main-thread statics as before).
- No Critical/Important blockers. Notes: (1) a worker-thread validate racing a main-thread invalidate is pre-existing and not widened here — the A/B must watch for stale-path symptoms. (2) Minor: the 1024 threshold counted **post-expansion** cells (NavGrid expands dirty tiles by update range first), risking a `Clear()` fallback on modest digs that would mask the win — **raised to 8192** post-review (comment updated) so ordinary play stays on the precise path.

## Measurement (Stage 5)
DONE — A/B captured (same save, 1 cycle, Debug Metrics on, fixed DLL confirmed loaded: local-only, `mtime 16:13`, `InvalidateCells` present). Full report: `harness/profiling-after-fix.md`.
- **Path Cache hit%: 4.1% → 3.7% — statistically FLAT. No win.**
- Hot paths unchanged/slightly higher (busier cycle): BrainScheduler 24.6k → 26.4k us/s, Navigator+StatesInstance 22.5k → 24.9k us/s.
- Correctness: clean — zero exceptions/NullRefs around NavGrid/PathCacher/Navigator; no observed pathing-through-terrain. GC 7.1/min vs 6.4 baseline (no regression).
- This confirms hypothesis **(b)**: the misses are dominated by navigator MOVEMENT (`center(grid) != queryCell` — dupes re-querying from new cells in a busy, deep-queue colony), not by over-invalidation. The path cache is structurally limited for this workload; invalidation precision was not the lever.

## Outcome
PARKED (pending decision) — the fix is correct (predicate proven against game ground truth, Stage-4 ADVANCE, no runtime errors) but produced NO measured win on this colony: navigator-movement misses dominate, so cache invalidation is not the bottleneck. Honest negative result. Open question for a follow-up instrumented capture (split `missInvalid` vs `missMoved` counters): definitively attribute the miss cause — if `missMoved` dominates, the path cache is inherently limited here (the lever is probe cost/frequency, a deeper change); if `missInvalid` still dominates, another invalidation/expiry source remains (a new lead). New lead surfaced this capture: a recurring `Update→Game` spike (~3.5x a tick) on Duplicant selection / info-panel open — candidate 0003.
