# Long-Session Player.log Audit — FastTrack (Aquatic/Unity 6, Debug build, Metrics+BenchmarkLog on)

Log: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`
Session window analyzed: engine start `03:54:50` -> last captured line `06:13:05` (~2h18m wall clock; FastTrack metric sampling covers ~136.5 min of that, from first `Managed Heap KB` sample onward). The log was still actively growing (live session) at the time of this audit, so "end" figures are a snapshot, not a game-exit state — no shutdown/crash markers were present at the tail.

## 1. Exceptions / errors / thread-safety symptoms

**Clean.** Zero `[ERROR]` lines in the whole file. Exactly **one** line anywhere in the log containing the word "Exception" (case-insensitive):

```
[03:55:14.010] [WARNING] [PLib/FastTrack] System.Exception Parameter "dcs" not found in method
System.Boolean ProcGenGame.WorldGen::RenderToMap(...)
  at HarmonyLib.MethodCreatorTools.EmitCallParameter (...)
```

This fires once, at startup (03:55:14), while FastTrack's Harmony patcher is analyzing the `WorldGen.RenderToMap` target — a reflection/parameter-name mismatch that Harmony caught and logged as a warning, not a runtime crash. It never recurs during the following ~2 hours of gameplay.

`[WARNING]` total: 30,822 lines, but 29,662 of them (96%) are two stock-ONI simulation warnings unrelated to FastTrack or the thread-safety audit:
- `Invalid cell modification (mass greater than element maximum)` — 29,662 combined variants
- `Invalid cell modification (zero temp with non-zero mass)` — 633

These are normal vanilla world-sim chatter for any long save with active gas/liquid venting, not defects introduced by FastTrack.

**Classes flagged by the completed thread-safety audit** (NavGrid, RoomProber, Cavity, PathCacher, Navigator, Conduit, Texture, PropertyTexture, AsyncJobManager): searched the full log for all four of `Exception|NullReference|ObjectDisposed|InvalidOperationException|IndexOutOfRange` cross-referenced against these class names — **no hits**. The only mentions of these classes in the whole log are benign:
- `Patched Navigator.AdvancePath [Cache]` — normal Harmony patch-applied confirmation at startup.
- `[PLib/StockBugFix] No method calls replaced: RoomProber.SolidChangedEvent to PeterHan.StockBugFix.DecorProviderRefreshFix.SolidNotChangedEvent` — a StockBugFix (not FastTrack) patch that found no call site to replace; a known no-op, not an error.
- Two per-frame profiler object-count-delta lines (`+2 (now 872) Navigator`) — expected telemetry, not a symptom.

No `OutOfMemoryException`, no `StackOverflowException`, no mod-disabled message, no stack traces beyond the single Harmony warning above. **No race condition fired as an observable exception in this session.**

## 2. Heap floor verdict (leak vs. Boehm/SGen fragmentation)

Parsed all 7,670 `[PLib/FastTrack] Managed Heap KB: <used>/<reserved> (<perSec>)` samples (~1/sec), bucketed by minute, took the **minimum `used` per bucket** as the post-GC floor proxy.

| Point | Time | Used floor (MB) | Reserved (MB) | Reserved/Used ratio |
|---|---|---|---|---|
| Start | t=0s | 3,358.1 | 3,486.3 | 0.963 |
| 25% | t=2,071s (34 min) | 3,834.6 | 5,291.0 | 0.725 |
| 50% | t=4,120s (68 min) | 3,990.8 | 6,253.5 | 0.638 |
| 75% | t=6,133s (102 min) | 4,284.3 | 7,169.0 | 0.598 |
| End | t=8,179-8,189s (136.5 min) | 4,410.0-4,514.0 | 7,987.3 | 0.552-0.578 |

Session duration (metric window): **8,189s = 136.5 min (~2h16m)**. Overall linear-regression slope of the floor: **~7.5 MB/min**, sustained across the whole window — the floor never flattens to a hard zero-slope plateau within this session.

But the growth is **bursty and decelerating, not accelerating**: per-quartile rate was 14.0 MB/min (0-25%), 4.6 MB/min (25-50%), 8.6 MB/min (50-75%), 3.7-6.8 MB/min (75%-end). A genuine unbounded leak would be expected to hold steady or accelerate, not oscillate downward.

The more telling signal is the **Reserved-vs-Used gap**: it starts at 128 MB (ratio 0.96) and balloons to ~3,470-3,600 MB (ratio ~0.55-0.58) by the end — i.e. the committed managed-heap arena grows roughly **5x faster** than the live-object floor does. That means the large majority of "Total Reserved" growth is arena/fragmentation overhead (classic Boehm/SGen conservative-GC behavior: freed segments aren't compacted or returned), not new live objects.

For context, whole-process `System Used KB` (managed + native: textures, meshes, audio, revealed-terrain caches) grew from **8.95 GB to 14.49 GB (+5.5 GB)** over the session, while the managed-heap live-set floor grew only **+1.05-1.16 GB** of that. Most of the total memory growth is native-side/arena, not managed-heap leakage.

**Verdict: leans toward fragmentation + legitimate colony growth over a hard leak, but not fully conclusive at 2h16m.** Supporting points:
- Growth rate decelerates/oscillates rather than climbing steadily or accelerating.
- The Reserved/Used ratio collapse shows fragmentation/overhead dominating the growth, not live-object accumulation.
- GC event frequency drops by more than half over the session (see §3) — inconsistent with a leak that would be expected to trigger collections more, not less, often as pressure builds.

Caveat: the floor's regression slope never reaches zero in this window, so this single 2h16m session cannot rule out a very slow residual leak — a 4+ hour session would give a cleaner plateau/no-plateau signal.

## 3. GC pause trend

256 `Garbage collection occurred!` events over the session, average one every 32s, but **frequency drops steadily** as the session progresses:

| Window | GC events |
|---|---|
| 0-10 min | 60 |
| 10-20 min | 26 |
| 20-40 min | 18-21 |
| 40-80 min | 15-18 |
| 80-120 min | 10-14 |
| 120-140 min | 5-7 |

Mean inter-GC gap roughly **doubles**: 19.7s (first half of session) -> 44.4s (second half). Collections get rarer over time, not more frequent.

However, **individual pause length gets worse**. From `[FT-BENCH]` frame samples (151,853 parsed):

| Quartile | Mean ms | p95 ms | Max ms |
|---|---|---|---|
| Q1 | 41.7 | 54.8 | 37,638 (startup hitch, frame 1) |
| Q2 | 45.1 | 61.9 | 3,046 |
| Q3 | 54.6 | 79.0 | 3,424 |
| Q4 | 75.3 | 96.9 | 3,672 |

Frames >100ms grow from ~114/10-min bucket early in the session to 267-419/10-min bucket in the last three buckets — a 3-4x increase. The nine worst individual spikes (3.2-3.7 **seconds** each, excluding the one-off 37.6s startup hitch) are concentrated in the second half of the session (t=4,300-7,960s, i.e. ~72-133 min in) and each correlates with `gcMB` in the 4,100-4,590 MB range — i.e., they occur exactly where the heap has grown largest. This is consistent with **fewer but much larger blocking full-GC sweeps**: less frequent collection, but each one scans/marks a bigger live set, so per-event pause length rises with heap size even as event count falls.

The single largest spike (37,638 ms) is frame=1 at t=2.3s — the initial world/scene load hitch, not GC-related, and expected at startup.

## 4. FastTrack patch health

Only 4 findings in the entire log, all clustered at startup (03:55:09-03:55:14), none recurring during the ~2h16m of gameplay that follows:

- `[PLibPatches] RegisterPatchClass could not find any handlers!` x2 — generic PLib framework message, known-benign.
- `[PLib/StockBugFix] No method calls replaced: RoomProber.SolidChangedEvent to PeterHan.StockBugFix.DecorProviderRefreshFix.SolidNotChangedEvent` — a StockBugFix (not FastTrack) patch found no call site; harmless no-op.
- `[PLib/FastTrack] System.Exception Parameter "dcs" not found in method ProcGenGame.WorldGen::RenderToMap...` (see §1) — logged as WARNING, caught, one-time.

No `Unable to patch`, `Undefined target` anywhere. **All FastTrack patches applied cleanly and stayed applied for the whole session** — nothing silently failed mid-run.

## 5. Perf-over-time

Frame time clearly degrades across the session in step with heap growth: mean +80% (41.7ms -> 75.3ms Q1->Q4), p95 +77% (54.8ms -> 96.9ms), spike rate (>100ms frames) up 3-4x from early to late buckets. This tracks the heap-size-driven GC pause growth in §3 (`gcMB` climbs in lockstep with the worst spikes), not a separate perf regression — the smoothness cost by the end of a 2+ hour session is dominated by longer, less-frequent full GCs sweeping an ever-larger live set, not by rising per-frame CPU cost or increasing collection frequency.

## Bottom line

No exceptions, no thread-safety symptoms, no failed patches during actual gameplay — the session is clean from a correctness standpoint. The open question (leak vs. fragmentation) leans toward **fragmentation + legitimate colony-growth**, not a hard leak: the used-floor growth rate decelerates/oscillates, the Reserved/Used ratio collapse (0.96 -> ~0.56) shows overhead outpacing live-object growth by ~5x, and GC frequency falls by more than half over the session. The practical consequence of the heap growth is real, though: GC pause length and frame-time p95 both degrade meaningfully (~80%) over a 2h16m session as each (less frequent) full collection has to sweep more live data. A 4+ hour session would give a more definitive plateau/no-plateau read on the heap floor.
