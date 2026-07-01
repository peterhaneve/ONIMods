# Candidate 0004: Periodic ~15s hitch = Mono Gen0 GC pause over a chronically multi-GB managed heap

- Status: MEASURED
- Target: not a single method — this is a runtime/GC characteristic, not a hot-path bug. Nearest code lever: whatever is holding the managed heap at ~3.2-3.8 GB baseline (steady-state live set) and whatever is generating the steady per-tick allocation that fills the Gen0 nursery. See "Next step" for how to find it.
- Risk class: none (no patch here — this candidate is an attribution writeup)
- Gating flag: none
- Collision with Peter's rewrites: n/a

## What and why

The user's periodic ~15s (±3s) hitch, unpaused, with no window open and no interaction, is a Mono Gen0 garbage-collection pause. It is not any ONI sim system (`Game.UnsafeSim200ms`, `ConduitFlow.Sim200ms`, `EnergySim.EnergySim200ms`) running long — those buckets show *fewer* calls/us on a hitch tick, because the whole main thread is stalled inside the collector before `Update`/sim methods even get to run that frame.

## Evidence (idle phase, `02:26:33` - `02:28:5x`, ~2 min, no windows/interaction)

Parsed all `[FT-BENCH]` lines (`frame`, `ms`, `gcMB`, `gen0`) and every `[PLib/FastTrack] Garbage collection occurred!` line from the capture into scratch files and cross-referenced by timestamp.

**Spike-to-GC correlation is exact.** Every `[FT-BENCH]` frame-time spike above baseline lines up, within tens of ms, with a `[PLib/FastTrack] Garbage collection occurred!` log line, and every spike frame's `gen0` counter is exactly +1 over the previous logged frame (never +2 or more — these are individual Gen0 collections, not batched). Examples from the idle window (timestamp, spike ms, gen0 before->after):

- `02:26:48.954` ms=778.6, gen0 178->179 — GC line at `02:26:48.932`
- `02:27:01.443` ms=1473.6, gen0 180->181
- `02:27:21.178` ms=793.5, gen0 183->184
- `02:27:56.301` ms=801.2, gen0 188->189
- `02:28:34.346` ms=799.4, gen0 193->194

Baseline (non-spike) frame time in the same window is ~16-45ms. Spike frames run **~640ms-1.5s**, i.e. roughly **20-90x baseline**. One later spike (`02:30:55.113`, outside the strict idle window, mixed into the Errands-tab phase) hit **2714ms** with a visible heap-size jump (`gcMB` 3532->3818) — almost certainly a bigger (gen1/segment-growth) collection, distinct from the routine ~700-900ms Gen0 pauses.

**Periodicity is not fixed — it's a growing interval that approaches ~15s.** Measuring the gap between successive `gen0`-incrementing spikes across the first ~4 minutes of the capture:

```
5.33s, 5.68s, 6.81s, 6.37s, 6.69s, 6.68s, 6.68s, 6.61s, 7.18s, 7.21s,
7.44s, 7.08s, 7.73s, 7.62s, 7.71s, 7.91s, 8.23s, 8.59s, ... (dupe-tab phase starts)
...9.29s, 10.74s, 11.49s, 12.86s, 13.02s, 13.82s  <- then a bigger collection resets it (3.96s, 11.97s, 5.92s, 5.68s)
```

The interval climbs smoothly and monotonically from ~5.3s toward ~13.8s over the capture, then a larger collection event resets it to a shorter interval before the climb resumes. This is the textbook signature of a generational GC's adaptive nursery/Gen0 budget: each Gen0 collection with a low survival rate grows the allocation budget before the next one fires, so collections space out further apart until either the budget hits its ceiling or a bigger (gen1/heap-growth) collection intervenes and resets the cycle. The user's observed "~15s ±3s" almost certainly is this climb's near-plateau — they're seeing the steady-state cadence after the ramp has been running a while, not a fixed 15s timer anywhere in ONI or Fast Track code.

**The heap itself is enormous and roughly stable, not obviously leaking within this window.** `gcMB` oscillates in the 3.25-3.8 GB band across the whole idle phase (e.g. 3250 at frame 0, 3438 at frame 400, 3608 at frame 2400, 3538 at frame 3600) with mild net growth (~3.40GB -> ~3.74GB over the full ~4.5 min capture, ~80MB/min). A live managed heap in the multi-GB range means even a "minor" Gen0 collection has a large root set / surviving-object graph to trace, which is consistent with Mono's collector (SGen or Boehm, per `oni-modding-environment` memory — this build runs the Mono backend, no IL2CPP) taking hundreds of ms to over a second per cycle, versus the single-digit ms a Gen0 pause costs in a healthy .NET workstation-GC process. This also explains the monster startup GC: `frame=1` at `02:26:36.238` logged `ms=38757.339` (38.7 **seconds**) — a full/compacting collection immediately after save load, over the same class of huge heap.

## Attribution

**Primary cause: Mono Gen0 GC pause, not a specific sim system.** The `[PLib/FastTrack] Garbage collection occurred!` line (FastTrack's own GC-notification hook) fires in lockstep with every hitch. `Game.UnsafeSim200ms`/`ConduitFlow.Sim200ms`/`EnergySim.EnergySim200ms` show reduced work on hitch ticks (the thread was stuck in GC, not doing sim work slowly). Ruled out: autosave (far less frequent, cycle-scale), ColonyDiagnostics/reachability rebuild, WorldInventory scan, `MinionTodoSideScreen.PopulateElements` (that's candidate 0005, already attributed to the dupe-Errands-tab phase and absent here) — none of these appear elevated on the spike ticks, and none would produce an interval that visibly *grows* over time the way this one does. A growing-then-resetting interval is a GC-budget artifact, not an ONI game-logic timer.

**Secondary/root cause (why the pauses are ~700ms-1.5s instead of a few ms): the managed heap is chronically ~3.2-3.8 GB.** That's the thing worth fixing. The hitch length scales with the size of the live set the collector must trace, so the actual lever is reducing steady-state managed memory (long-lived caches, retained large arrays/dictionaries, boxing, or anything else accumulating and being kept alive), not chasing a specific allocation call site tied to a 15s clock — there isn't one.

## Next step (this candidate is attribution, not a fix — needs one more instrumentation pass before a patch is proposable)

1. **Distinguish collection generation and get a real pause duration.** The current `Garbage collection occurred!` hook only logs a boolean event with no duration and no generation. Wrap it (or add alongside it) with:
   - `GC.CollectionCount(0)`, `GC.CollectionCount(1)`, `GC.CollectionCount(2)` sampled every tick, logged as deltas, so gen0-only vs gen1/full collections are distinguishable (the one 2714ms/heap-growth outlier strongly looks like a different generation/segment-growth event than the routine ~700-900ms ones).
   - A `Stopwatch` bracket around the actual GC (e.g., via `GC.RegisterForFullGCApproach`/`GC.RegisterForFullGCComplete` if available on this Mono version, or by timing the `Update` call that observes the collection count change) to get a measured pause duration instead of inferring it from the enclosing frame's `ms`.
2. **Find what's holding ~3.3 GB live.** Add a periodic (every ~30s) log of `GC.GetTotalMemory(false)` alongside a coarse breakdown if available (Fast Track already tracks per-system inclusive time — extend that instrumentation, or take a Mono heap snapshot via `Mono.Profiler`/`heapshot` at two points ~1 minute apart in an idle session) to identify what's actually large and growing. Likely suspects given this codebase's known hot paths: path-cache/grid pools (candidate 0002 territory), any per-cell or per-frame `List<>`/`Dictionary<>` allocation that isn't pooled, or long-lived UI state.
3. **Check the Mono GC mode/params.** Confirm whether the Unity player is running Boehm (conservative, non-generational, pause scales with whole-heap size) or SGen (generational, pause should mostly scale with nursery + surviving set, not full heap) via the Unity Player Settings / `MONO_GC_PARAMS` env. If it's Boehm, the ~3.3 GB heap alone is sufficient explanation for 700ms+ pauses and the fix is squarely "reduce total live heap," not "find one bad allocation site." If it's SGen, nursery-size tuning (`nursery-size=` in `MONO_GC_PARAMS`) could trade pause frequency for pause length as a stopgap, independent of finding the leak.

## Measurement (Stage 5)

Idle-phase capture, `Player.log`, `02:26:33`-`02:28:5x` (~2 min, unpaused, no windows/interaction), cross-referenced `[FT-BENCH]` and `[PLib/FastTrack] Garbage collection occurred!` lines by timestamp (see method above). Baseline ~16-45ms/frame; hitch frames ~640ms-1.5s (20-90x baseline), each coincident with a `gen0` counter +1 and a `Garbage collection occurred!` log line. Interval between hitches climbs smoothly from ~5.3s to ~13.8s across the capture before a larger collection resets it — consistent with adaptive Gen0/nursery budget growth, not a fixed timer. No `[PLib/FastTrack]` sim-system bucket (`Game.UnsafeSim200ms`, `ConduitFlow.Sim200ms`, `EnergySim.EnergySim200ms`) shows elevated inclusive time on hitch ticks.

## Outcome

Not applicable yet — this is an attribution writeup, not a patch. Recommend opening a follow-up candidate once the Stage-1 instrumentation above (GC generation split + real pause duration + heap-growth source) narrows the ~3.3 GB baseline to a specific retained structure.
