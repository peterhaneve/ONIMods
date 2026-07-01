# Fast Track — whole-codebase thread-safety & efficiency audit (2026)

Scope: all of `FastTrack/` (~117 files), audited in 5 parallel passes (PathPatches+concurrency core, GamePatches, UIPatches, VisualPatches+ConduitPatches, Sensor/Critter/Ryu/Metrics/root). Lens: thread-safety (primary) + efficiency (secondary), value-filtered, with the perf-mod caveat (intentional zero-alloc/pooling/hand-rolled fast code is NOT flagged).

## Verdict

The codebase is well-engineered and thread-safe. **No Critical defects.** The hot cross-thread state is almost entirely routed through `ConcurrentDictionary`/`ConcurrentQueue`, `Interlocked`, per-node locks, or a strict enqueue→barrier→dispatch discipline. **Efficiency headroom is essentially nil** — every audit independently found the per-frame/per-tick paths already pooled and tight (confirming the earlier candidate hunt).

The one recurring *real* pattern is **disposal/timeout racing an in-flight worker.** One instance is a steady-state race (worth fixing); the rest are teardown- or 5s-timeout-gated (robustness, abnormal-path).

## Findings (ranked, deduplicated across the 5 passes)

### Tier 1 — fix now (steady-state, active, clear, low-risk)

1. **`BackgroundRoomProber.recycled` is a plain `Queue<CavityInfo>` mutated from both threads.** (GamePatches)
   Foreground enqueues (`Postprocess`, main-thread `Sim200ms`); the background room-prober thread dequeues (`CreateCavityFrom`). They are decoupled, so when a prober pass outlasts a 200 ms sim tick (the heavy-colony case this feature exists for) the two threads hit the non-thread-safe `Queue` concurrently → corruption / throw / a cavity handed out while being reused. **The tell:** every *other* cross-thread queue in the same class (`destroyed`, `buildingChanges`, `releasedCritters`, `solidChanges`) is already a `ConcurrentQueue`; `recycled` is the one that got missed. **Active in the profiled config (`BackgroundRoomRebuild: true`).** Fix: make it a `ConcurrentQueue<CavityInfo>` (`TryDequeue`), matching its siblings. One-line-shaped, low-risk, consistent with existing code.

### Tier 2 — real but abnormal-path (timeout/teardown); robustness fixes

2. **`PropertyTextureUpdater` (`:298-301`) — on `onComplete.WaitOne(5000ms)` timeout, falls through to `DisposeAll()` which `Unlock`s native texture buffers a worker may still be writing.** (VisualPatches) Highest *severity* (native memory corruption, not just a handle) but only triggers on a 5 s stall (GC pause / debugger / stuck worker). Gated by `MeshRendererOptions` (active — user config = All). Fix: on timeout, don't unconditionally dispose — keep blocking, or track per-task completion and only dispose collections whose `TriggerComplete`/`TriggerAbort` fired. The same shortcut exists in `AsyncAmountsUpdater:134` and `BackgroundWorldInventory:225` — worth a single shared fix.

3. **`BackgroundConduitUpdater.Dispose` (`:96-98`) disposes its `AutoResetEvent` without guaranteeing the job finished.** (ConduitPatches) On the timeout path a later `TriggerComplete/Abort` calls `Set()` on a disposed handle → `ObjectDisposedException`, unhandled on the worker thread (the `ReportInactive` path is outside the worker try/catch) → possible crash-on-exit. Teardown + timeout gated (low blast radius). Fix: join before dispose, or make `Trigger*` tolerate a disposed handle.

4. **`AsyncJobManager.Dispose` teardown edges** — `currentJob` nulled outside the lock (worker can null-ref between two reads; caught → dropped work), and `semaphore.Release` can race a worker's `AdvanceNext` release → `SemaphoreFullException` (kills that worker, outside its try/catch). (root/core) Teardown-only; the specialist pass verified the *steady-state* job hand-off is race-free. Low value; fix alongside the disposal-race class if touched.

### Tier 3 — latent / narrow / documented caveat (validate before touching)

5. **`AsyncBrainGroupUpdater.GetNavigationCost` (`:71-83`) calls `offsetTracker.UpdateOffsets` unlocked from parallel `FinishFetchesWork` workers.** Two workers can hit the same tracker when `offsets == null`; the main-thread path locks, this one doesn't. Probably unreachable in steady state (offsets are pre-populated the prior frame; it's a safety-net branch). Inherited core design — validate against upstream before changing. Fix if desired: `lock (offsetTracker)`.

6. **`LoadModPatches.patched_methods` (plain `HashSet`) is populated on a background thread during mod compile; the main-thread crash handler reads it unsynchronized.** Narrow window, load-time only, but it degrades crash *diagnostics* exactly when they matter. Fix: lock / concurrent / honor the existing `compilingList` flag.

7. **`FastCmps.GetWorldItems_Patch.POOL` (plain `Dictionary<Type,IList>`) returns a shared cleared list** — non-reentrant and not thread-safe. No worker caller exists in the fork today (gated `AllocOpts`, main-thread); base-game call sites are outside scope. Documented caveat; make `[ThreadStatic]`/lock only if a concurrent caller is confirmed.

### Minor / cosmetic
- `PathProbeJobManager.OnPathComplete` adds to `totalRuntime` after `Set()` (metrics-only undercount).
- `UpdateOffsetTablesWork` derefs `DeferredTriggers.Instance` without `?.` on a worker (caught by job try/catch).
- `RatioProfiler.ToString` non-atomic `hits`/`total` read (log-only, can print hits>total).
- `AchievementPatches.Navigator_OnSpawn_Patch` `tag.ToString().Contains("Minion")` allocs per spawn (compare the tag directly).
- Stale/misleading comments (`PropertyTextureUpdater.TriggerAbort` says "background thread"; it's main-thread).

## Confirmed correct (worth recording — do NOT touch)
- Ryu (pure, reentrant), `AsyncJobManager` steady-state work-stealing, job serialization, `PriorityBrainScheduler`, `CacheHitNavigators`, `PathCacher` (+ candidate 0002 code), `ThreadsafePartitionerLayer`, `ConcurrentHandleVector`, `AsyncAmountsUpdater`, `FetchManagerFastUpdate`, `BackgroundConduitUpdater.InternalDoWorkItem` (disjoint slots), `PropertyTextureUpdater.outstanding` sequencing, `BackgroundInventoryUpdater`/`BackgroundWorldInventory` handoff, all 9 UIPatches sibling shared-static builders (main-thread-only → correctly left plain, NOT `[ThreadStatic]`), the entire tile/terrain/KAnim mesh renderer group, Metrics (`Interlocked`/`ConcurrentDictionary`), CritterPatches.

## Efficiency
No findings with real headroom in any subsystem. Pooling (`ListPool`/`HashSetPool`/reused scratch), zero-alloc formatting, dirty-chunk-scoped mesh rebuilds, and cached visibility guards are applied where they matter. `Texture2D.Apply()` main-thread cost is structural (out of the mod's reach), not a missed optimization.

## Recommendation
Fix Tier 1 (#1 `recycled` → `ConcurrentQueue`) — clear value, low risk, active. Optionally address the Tier 2 disposal-race class (#2 highest severity) for robustness. Document/leave Tier 3 (some is inherited core design). Efficiency: nothing to do — the mod is already tight.
