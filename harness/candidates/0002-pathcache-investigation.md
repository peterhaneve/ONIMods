# Candidate 0002: Path-cache collapse — terrain-invalidation over-scan

- Status: DESIGN (gated on one discriminating measurement before BUILT)
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

## Patch (only if (a) confirmed)

Replace the union-bbox in `NavGrid_UpdateGraph_Patch.Postfix` with dirty-cell-membership invalidation:

- Add `PathCacher.InvalidateCells(List<int> dirtyCells)`: one pass over `pathCache` (same enumeration cost as today). For each grid: full-map grids (`!applyOffset`) invalidate unconditionally when the list is non-empty (unchanged, correct — any change can affect a full-map path); bounded grids invalidate iff at least one dirty cell falls inside their window `[rootX,rootX+w) x [rootY,rootY+h)` (AABB membership test against the dirty list). Keep the existing same-frame dedup.
- Guard cost: if `dirtyCells.Count` exceeds a threshold (a genuine map-wide event such as a large cave-in), fall back to the current full wipe — at that point most grids legitimately overlap and the per-cell test is wasted work.
- Change the `Postfix` to call `InvalidateCells(__0)` instead of building `minX..maxY`.

Cost note: per-grid x dirty-cell AABB tests (hundreds x hundreds ~ 1e4-1e5 cheap int compares/frame) are far cheaper than the full path probes this avoids; a single avoided 10-arg `Run` pays for the whole scan.

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
Pending — write-up to be reviewed by a fresh agent per repo policy before any build.

## Measurement (Stage 5)
Not run. The discriminating A/B above is the gate.

## Outcome
DESIGN — mechanism identified and verified against the current decompile; transpiler targeting cleared; the regression is localized to the fork's net-new `NavGrid.UpdateGraph -> InvalidateRegion` union-bbox over-scan. Hold at DESIGN until the one toggle/instrumentation capture distinguishes over-invalidation (a) from benign navigator movement (b). Build the membership-invalidation fix only if (a) is confirmed.
