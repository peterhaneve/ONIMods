# Candidate 0007: Stage-1/2 survey — next hot path after Sublimates / ScenePartitioner / FastReachabilityMonitor

- Status: SURVEY (top-3 profile targets all inherent/already-tight; monitor-cluster lead is uncertain; strategic inflection — see Recommendation)
- Target: survey of three ranked hot paths (`200s`→Sublimates, `1000s`→ScenePartitioner, `4000s`→FastReachabilityMonitor)
- Risk class: n/a (survey); recommended follow-up is profile-first
- Gating flag: n/a
- Collision with Peter's rewrites: see per-target notes

## What and why

Survey to find the next net-new Fast Track optimization on the `fasttrack-aquatic-optim` fork. Three targets from `profiling-baseline.md`, ranked. Verdict below is: none of the three is a clean net-new win; the recommended action is a profile-first probe on the `1000s` per-instance StateMachine-monitor cluster, which is the more promising frontier.

### Target 1 — `200s` → Sublimates (34,074 us/s, 37% of the 91,568 us/s bucket)

Sim-side driver: `Sublimates.Sim200ms(float)` (`harness/decompiled/Assembly-CSharp/Sublimates.cs:157`). `Sublimates : KMonoBehaviour, ISim200ms`, one instance per off-gassing object, dispatched by `SimAndRenderScheduler` every 200 ms (5 Hz). `OnPrefabInit` sets `simRenderLoadBalance = true`, so instances are spread across ticks, but total work is still O(n) in the sublimating-object count. This colony has a large sublimating backlog, so n is large.

Per-call work (blocked-backlog path, the dominant one for a backlog where the destination cell is at max pressure):
- `Grid.PosToCell(transform.GetPosition())`, `HasTag(GameTags.Sealed)`
- `GetComponent<Pickupable>()` (line 165, always runs; used only when Sealed)
- `ElementLoader.FindElementByHash(info.sublimatedElement)` (line 171) — dict `TryGetValue` on an immutable per-instance value, every tick
- `Grid.Mass[cell]` compare → early-out to `RefreshStatusItem(BlockedOnPressure)` (guarded, no-op if unchanged)

No per-call heap allocations (uses `stackalloc` in `SimMightOffcellOverpressure`; `GetComponent` and `FindElementByHash` don't allocate; `SetStatusItem` is guarded by a state-change check). So Sublimates is not a GC-stutter contributor.

Fast Track coverage: ONLY the render/FX side. `VisualPatches.Sublimates_Emit_Patch` (`FastTrack/VisualPatches/VisualPatches.cs:162`) transpiles `Sublimates.Emit` to skip `SpawnFX` when off-screen, gated `RenderTicks`. The SIM side (`Sim200ms`) is untouched. No existing candidate touches it either.

Optimizable? Weakly. The only net-new surface is caching the two immutables re-resolved each tick (the `Element` and the `Pickupable`/`Storage` refs; `UpdateStorage` re-calls `GetComponent<Pickupable>` a second time on the emit path). But per-instance caching needs per-instance side storage (companion component, `ConditionalWeakTable`, or a static dictionary), and accessing that store every tick costs about as much as the `FindElementByHash` + `GetComponent` it replaces. Net result is likely a wash. The remaining cost is inherent O(n)×5 Hz, and the 34k us/s absolute is inflated by the Harmony stopwatch wrapping (32,476 calls/s in this bucket — the per-call instrumentation is a large share of that number, per the baseline caveat). Honest read: lean per-call work, instance-count dominated, not a clean Harmony-mod win.

### Target 2 — `1000s` → ScenePartitioner (9,178 us/s, 20% of bucket)

Sim-side driver: `ScenePartitioner.Sim1000ms(float)` (`harness/decompiled/Assembly-CSharp/ScenePartitioner.cs:239`). Iterates `dirtyNodes`, sweeps each dirty node's entry list, drops handles that no longer resolve via `GameScenePartitioner.Instance.Lookup`, clears the dirty flag. Cost scales with node churn (how many partition nodes were touched by movement/insertion in the last second) — high in a busy colony, but that is inherent to a moving colony. The method is already tight (list walk + Lookup + Remove). This is core spatial-partition infrastructure used by pathing, reachability, sensors — high collision risk with Peter's pathing/sim work and with Klei internals. Not a good target.

### Target 3 — `4000s` → FastReachabilityMonitor (5,925 us/s, 80% of bucket)

This is Fast Track's OWN code (`FastTrack/SensorPatches/FastReachabilityMonitor.cs`), gated `FastReachability`. `Sim4000ms` just calls `UpdateOffsets()` (line 115). `UpdateOffsets`: `Grid.PosToCell`, `smi.master.GetOffsets(cell)` (returns the workable's cached `offsetTracker` array — no per-call alloc, `Workable.GetOffsets` at `Workable.cs:617`), `new Extents` bounding-box over the offsets, a 4-int compare, and it early-outs when unchanged. Already about as tight as it gets. The periodic 4 s poll is a deliberate safety net beside the `CellChanged`/`Landed` event subscriptions (offsets can change without a move event); removing it trades a correctness risk (stale reachability) for a small gain. The 5,925 us/s is instance-count driven (thousands of reachable workables, each polled every 4 s) and already load-balanced across the 4 s window. Not worth touching.

## Predicted impact

- Sublimates: realistic net-new gain ~wash-to-marginal after accounting for side-storage access cost. Not worth building as-is.
- ScenePartitioner: no safe net-new gain; core infra.
- FastReachabilityMonitor: negligible gain, correctness risk; already tight.

## Recommendation

None of the three clears the bar for a clean, net-new, safe win. Sublimates is the largest but its per-call work is already lean and instance-count dominated, and the one cacheable thing needs per-instance side storage whose per-tick access roughly cancels the saving. ScenePartitioner and FastReachabilityMonitor are core/own-code and already tight.

Re-survey a different subsystem. The most promising untouched frontier in the same data is the `1000s` bucket's per-instance StateMachine-monitor cluster:

| Class (`1000s`) | us/s |
|---|---|
| Rottable+Instance | 6,041 |
| RadiationVulnerable+StatesInstance | 5,533 |
| OvercrowdingMonitor+Instance | 5,149 |
| FixedCapturePoint+Instance | 4,772 |

Combined ~21,500 us/s — larger than Sublimates, and each is the same shape: many instances, each running a buffered StateMachine update every 1000 ms. Fast Track has precedent for consolidating exactly this pattern into a single background/batched updater (e.g. `BackgroundRoomProber`, `AsyncAmountsUpdater`, the `*Updater` classes). Grep confirms Fast Track currently touches these classes only for UI string formatting (`FormatStringPatches.3.cs`, `SimpleInfoScreenWrapper*`), never their sim-side update — so the update side is genuinely net-new surface. The 1000 ms cadence also gives more headroom to stagger/batch than Sublimates' 200 ms.

### Concrete next step

Profile-first (same method as prior candidates). Add a temporary Fast Track Debug Metrics / `[FT-BENCH]`-style split on one target (start with `Rottable.Instance` or `OvercrowdingMonitor.Instance`) to separate per-call cost from instance count, and confirm what each iteration actually does (GetComponent, element/attribute lookups, allocations). Only then decide between (a) caching immutables, (b) consolidating into a batched updater, or (c) staggering. Do not build blind — like Sublimates, several of these may turn out instance-count dominated with lean per-call bodies.

- Profile-first: YES (confirm per-call vs instance-count split before building)
- Risk class if it becomes a batched-updater rewrite: static/threading + StateMachine — save-sensitive, higher risk; a caching-only variant would be Transpiler/Prefix, lower risk
- Gating flag: new flag under the existing sensor/sim options group (e.g. alongside `FastReachability` / `SensorOpts`)

## Patch
n/a (survey)

## Unit test
n/a (survey)

## Static gates (Stage 3)
- Compiles: na
- Unit test: na
- IL verify: na
- Thread-safety check: na
- Save-compat: na — flagged that any 1000s-cluster follow-up touching StateMachine monitors is save-sensitive (`[SerializationConfig]` + KSerialization; determinism)

## Review (Stage 4)
pending

## Measurement (Stage 5)
n/a

## Outcome
PARKED (survey) — recommend profile-first probe on the `1000s` StateMachine-monitor cluster (Rottable / OvercrowdingMonitor / RadiationVulnerable / FixedCapturePoint) as the next candidate; the three surveyed targets are inherent/already-tight.
