# Fast Track Debug Metrics — Late-Game Colony Profiling Baseline

**Source log:** `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log` (32,216 lines, session `22:01:18` – `22:07:17`).

**Colony context:** late-game save, managed heap steady-state ~3.4-4.2 GB (sawtooth), measured mean frame time ~44 ms / p95 ~60 ms after warmup — i.e. this is a real, user-visible stutter problem, not a benchmark artifact.

**Capture window:** ~1 in-game cycle of real time (~4 min 41 s of FastTrack metrics, 262 one-second `Render1000ms` samples), including one autosave.

**Build type:** DEBUG. `Managed Heap KB:`, `Total Reserved KB:`, `System Used KB:` and `Garbage collection occurred!` lines are all present (262/262, 262/262, 262/262, and 30 GC events respectively), so heap/GC visibility is full for this capture — no Release-build caveat needed.

**`[FT-BENCH]` lines (8,026 of them) were excluded** — that is FastTrack's separate per-frame sampler, a different tool measuring different things; mixing it in would double-count and confuse the per-second `[PLib/FastTrack]` Debug Metrics this report is built from.

## Parsing method

`[PLib/FastTrack]` appears 3,002 times, but a naive grep on that string only captures the **first** line of two multi-part log messages (`Sim/Render:` and `Update:`/`LateUpdate:`), which embed `\n` and emit their per-class breakdown as tag-less continuation lines. The actual per-class data — the only data that identifies *which* class is hot — lives in those continuation lines. A sequential block-scanner (`scratchpad/parse_fasttrack.py`) was used instead: it walks the log line-by-line, recognizes the fixed structure of each block (8 ordered Sim/Render buckets: `*r, 200r, 1000r, *s, 33s, 200s, 1000s, 4000s`; then `Update:`/`LateUpdate:`), and skips over interleaved non-FastTrack lines (other game-log activity logged concurrently) without breaking the block. This recovered all 262 samples cleanly — sample count matches the independent `Managed Heap KB:` line count exactly (262 = 262), confirming no samples were lost or double-counted.

## Sample windows

| Window | Samples | Timestamps | Why excluded from steady-state |
|---|---|---|---|
| Total captured | 262 | `22:02:31.187` – `22:07:12.207` | — |
| Warm-up (dropped) | 5 (idx 0-4) | `22:02:31.187` – `22:02:35.430` | Path Cache hit% still climbing 20%→100%; idx 0's `Update` bucket is 2.74M us (vs. ~230-460K steady-state) — startup/JIT cost folded into the first tick. |
| Autosave (flagged, excluded from steady-state, reported separately) | 3 (idx 219-221) | `22:06:29.051` – `22:06:30.742` | Confirmed against plain game-log lines: `Deleting old autosave` at `22:06:28.260`, `[PLib/FastSave] Background save complete` at `22:06:28.908`, `Saving screenshot` at `22:06:30.148`. idx 219 has the single largest real-time gap of the whole session (3.53 s vs. the usual ~1.0-1.1 s cadence) and is the *only* sample where Managed Heap **rises** sharply in one tick (+124,872 KB/s) instead of falling — every other large heap swing in this log is a GC-driven drop. |
| **Steady-state (used for all stats below)** | **254** | — | — |

## Sim/Render buckets (steady-state, per-second averages over 254 samples)

`us` = microseconds measured **inside** the profiled method body for that 1-second window (Stopwatch-wrapped via Harmony). Divide by call count for per-call average; divide by ~22.7 frames/sec (1/44ms) for a rough per-frame figure.

| Bucket | Avg calls/s | Avg total us/s | % of bucket's time in its top class |
|---|---|---|---|
| `*r` (every render tick) | 8,449 | 33,447 | BrainScheduler 74% |
| `200r` | 674 | 16,178 | PeterHan.CritterInventory.CritterInventory 83% |
| `1000r` | 598 | 1,261 | (all classes <1,000us/sample; no single dominant) |
| `*s` (every sim tick) | 405 | 24,753 | Navigator+StatesInstance 91% |
| `33s` | 2,380 | 11,535 | FetchAreaChore+StatesInstance 19% |
| `200s` | 32,476 | **91,568** (largest Sim/Render bucket) | Sublimates 37% |
| `1000s` | 5,146 | 46,024 | ScenePartitioner 20% |
| `4000s` | 6,909 | 7,449 | PeterHan.FastTrack.SensorPatches.FastReachabilityMonitor 80% |

Sum across all 8 Sim/Render buckets: **~232,212 us/s** average.

### Top classes per bucket (sum of us over 254 steady-state samples; avg/s = sum / 254)

**`*r` — every render tick** (total 8,495,420 us over 254s = 33,447 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| BrainScheduler | 6,250,518 | 24,609 |
| OrbitalObject | 696,863 | 2,744 |
| KBoxCollider2D | 578,830 | 2,279 |
| LoopingSoundManager | 257,176 | 1,012 |
| SpriteSheetAnimManager | 48,064 | 189 |
| LightSymbolTracker | 40,588 | 160 |

**`200r`** (total 4,109,119 us = 16,178 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| PeterHan.CritterInventory.CritterInventory | 3,398,762 | 13,381 |
| MaterialSelectionPanel | 94,271 | 371 |
| FetchListStatusItemUpdater | 11,558 | 46 |

**`1000r`** (total 320,352 us = 1,261 us/s avg) — diffuse, no class individually exceeds 1,154 us summed; not a hot path.

**`*s` — every sim tick** (total 6,287,376 us = 24,753 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| Navigator+StatesInstance | 5,706,812 | 22,468 |
| SegmentedCreature+Instance | 457,483 | 1,801 |
| ElectrobankCharger+Instance | 2,233 | 9 |

**`33s`** (total 2,929,938 us = 11,535 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| FetchAreaChore+StatesInstance | 543,616 | 2,140 |
| GasLiquidExposureMonitor+Instance | 422,661 | 1,664 |
| BubbleManager | 306,752 | 1,208 |
| WorkChore`1[ComplexFabricatorWorkable]+StatesInstance | 298,598 | 1,176 |
| SleepChore+StatesInstance | 202,746 | 798 |
| EatChore+StatesInstance | 70,767 | 279 |
| WorkChore`1[Constructable]+StatesInstance | 42,574 | 168 |
| FallMonitor+Instance | 40,325 | 159 |
| ScheduleManager | 35,423 | 139 |
| BionicBedTimeModeChore+Instance | 32,066 | 126 |

**`200s`** (total 23,258,374 us = **91,568 us/s avg, largest Sim/Render bucket**)
| Class | Sum us | Avg us/s |
|---|---|---|
| Sublimates | 8,654,573 | 34,074 |
| CritterTemperatureMonitor+Instance | 1,726,195 | 6,796 |
| DrowningMonitorUpdater | 1,078,862 | 4,248 |
| PeterHan.FastTrack.GamePatches.BackgroundRoomProber | 979,426 | 3,856 |
| TemperatureVulnerableUpdater | 908,879 | 3,578 |
| RequireInputs | 874,067 | 3,441 |
| EggProtectionMonitor+Instance | 767,379 | 3,021 |
| CreatureCalorieMonitor+Instance | 744,688 | 2,932 |
| PressureVulnerableUpdater | 522,726 | 2,058 |
| KComponentsInitializer | 516,804 | 2,035 |
| CritterEmoteMonitor+Instance | 434,840 | 1,712 |
| RanchStation+Instance | 424,250 | 1,670 |

**`1000s`** (total 11,689,993 us = 46,024 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| ScenePartitioner | 2,331,089 | 9,178 |
| Rottable+Instance | 1,534,241 | 6,041 |
| RadiationVulnerable+StatesInstance | 1,405,357 | 5,533 |
| OvercrowdingMonitor+Instance | 1,307,838 | 5,149 |
| FixedCapturePoint+Instance | 1,211,937 | 4,772 |
| BreathingGeyser+Instance | 608,122 | 2,394 |
| FishOvercrowingManager | 403,865 | 1,590 |
| ManualDeliveryKG | 159,011 | 626 |
| SubmergedMonitor+Instance | 137,383 | 541 |
| IlluminationVulnerable+StatesInstance | 84,455 | 333 |

**`4000s`** (total 1,891,984 us = 7,449 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| PeterHan.FastTrack.SensorPatches.FastReachabilityMonitor | 1,504,794 | 5,925 |
| Growing+StatesInstance | 6,785 | 27 |
| AutoSuitDelivery.SuitLockerAutoDelivery | 2,435 | 10 |
| TrappedDuplicantDiagnostic | 2,081 | 8 |

## Update / LateUpdate (steady-state)

| Bucket | Avg calls/s | Avg total us/s | Top class | % of bucket |
|---|---|---|---|---|
| `Update` | 72,569 | **470,270** | Game | 73% |
| `LateUpdate` | 10,113 | **266,539** | World | 55% |

Combined `Update` + `LateUpdate` + all Sim/Render buckets ≈ **969,021 us of measured time per real second** — i.e. essentially the entire 1,000,000 us real-time budget for that second is accounted for by profiled call time. This corroborates the reported ~44 ms mean frame time (~22.7 FPS): the colony is genuinely CPU-bound, not idling.

**`Update`** (total 119,448,602 us over 254s = 470,270 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| Game | 87,672,805 | 345,168 |
| ResearchScreen | 16,720,038 | 65,827 |
| Global | 5,051,968 | 19,889 |
| Infrared | 2,389,323 | 9,406 |
| GameScheduler | 1,849,107 | 7,280 |
| SparkLayer | 1,715,294 | 6,753 |
| AlternateSiblingColor | 770,025 | 3,032 |
| MultiToggle | 726,838 | 2,862 |
| MusicManager | 128,315 | 505 |
| SimDebugView | 107,730 | 424 |

Note: `Game.Update` is Klei's own top-level driver and is expected to wrap nested simulation work — some of the time attributed here may already be double-counted against the Sim/Render breakdown above (FastTrack profiles both layers independently; they are not strictly additive). `ResearchScreen` at 65,827 us/s avg is a surprisingly large, persistent UI cost for a screen that's not necessarily always open — worth checking when/why it's running every Update.

**`LateUpdate`** (total 67,700,897 us over 254s = 266,539 us/s avg)
| Class | Sum us | Avg us/s |
|---|---|---|
| World | 37,003,286 | 145,683 |
| Game | 18,280,693 | 71,970 |
| Global | 8,010,415 | 31,536 |
| SelectTool | 1,635,379 | 6,438 |
| KBatchedAnimTracker | 828,721 | 3,262 |
| AnimEventHandlerManager | 538,401 | 2,119 |
| Rendering.BlockTileRenderer | 385,531 | 1,518 |
| PropertyTextures | 363,743 | 1,432 |

## Path Probes

Steady-state average: **0 us executed, 0 us saved, 0/frame** — every one of the 254 steady-state samples shows `executed 0us, saved 0us (0/frame)`. Async path probing was not exercised during this capture window (no long-distance/cross-room pathing requests were being computed asynchronously at the time). Not a contributor in this run; do not read this as "path probing is cheap" in general — it simply wasn't active.

## Path Cache hit ratio — bimodal, not a single number

Blending the whole steady-state range into one average (18.43%) would be misleading. The hit ratio has three distinct regimes:

| Regime | Samples (idx) | Span | Avg hit% | Min/Max |
|---|---|---|---|---|
| A — warm cache | 5-40 | `22:02:36` – `22:03:12` (~36s) | **99.8%** | 91.7% / 100.0% |
| B — collapsed | 41-258 | `22:03:13` – `22:07:08` (~218s, the bulk of the capture) | **4.1%** | 0.0% / 44.8% |
| C — recovering (tail) | 259-261 | `22:07:09` – `22:07:12` (3 samples) | 77.6% | 51.9% / 93.9% |

The transition from A to B is sharp: `22:03:13.803` drops from 91.7% to 3.3% in a single tick (idx 41). For ~85% of the captured session, almost every path request was a cache miss. This lines up directly with `Navigator+StatesInstance` and `BrainScheduler` being the two largest named hot-path classes above — the colony was continuously generating fresh, uncached path queries, not just running expensive-but-cached navigation.

## Events

Steady-state average: 5,847 calls/s, 35,403 us/s (max single-sample total: 315,634 us). Event hashes aren't human-readable in this log format, so no per-event-type ranking is possible, but the aggregate volume (~35K us/s, comparable in magnitude to the `33s` Sim/Render bucket) is non-trivial and worth keeping in mind as background load.

## Methods Run / Conditions / Brain Stats

- **Methods Run:** present in all 254 steady-state samples, tracking `Game.UnsafeSim200ms` throughout, with `ConduitFlow.Sim200ms` and `EnergySim.EnergySim200ms` appearing starting partway through the session (visible in later samples but not the first few steady-state ones) — method tracking scope appears to have changed mid-capture (likely a FastTrack debug-menu toggle), not a steady-state signal. Typical figures: `Game.UnsafeSim200ms` ~60-70 calls/s at ~50-65us avg/call; one outlier sample shows `Game.UnsafeSim200ms[15/40,138us|1/2,676us]` (a 2.7ms single call) — a one-off spike, not representative.
- **Conditions:** absent in all 262 samples — not used in this run.
- **Brain Stats:** absent (empty body) in all 262 samples — not used/tracked in this run.

## Managed Heap / GC (DEBUG build — full visibility)

- Steady-state Managed Heap **used**: avg 3,656,548 KB (~3.57 GB), range 3,425,092 – 4,204,568 KB (~3.34 – 4.10 GB) — classic sawtooth, consistent with the colony-context note of ~3.4 GB heap.
- Steady-state Managed Heap **reserved**: avg 3,873,099 KB (~3.78 GB).
- Total Reserved KB avg: 6,468,614 KB (~6.32 GB). System Used KB avg: 9,542,932 KB (~9.32 GB).
- **Autosave heap spike:** the 3 autosave-window samples show heap used jumping 3,680,172 → 3,805,044 → 3,842,904 → 3,867,908 KB, i.e. a sustained *rise* of +124,872 KB/s then +37,860 KB/s then +25,004 KB/s — the only stretch in the whole log where heap climbs for 3 consecutive ticks without a GC drop in between. This is autosave-specific (serialization allocates heavily) and is excluded from the steady-state heap average above.
- **Gen0 GC frequency:** 30 collections over 281.0s session span = **6.41/minute** (~once every 9.4s).
- **GC ↔ stutter correlation:** 22 of 262 samples show a real-time gap >1.3s between consecutive 1-second ticks (vs. the expected ~1.0-1.1s cadence) — i.e. 22 visible frame-rate stalls. Cross-referencing timestamps, **every one of the 30 GC events lands inside or adjacent to one of these stall samples**, and the largest non-autosave stalls (1.8-2.3s) consistently coincide with a GC event and a large negative heap delta (heap dropping by 150-360K KB/s as Gen0 garbage is reclaimed) in the same or very next sample. GC pauses are the single most frequent source of visible stutter in this session — far more frequent (6.4/min) than the one observed autosave.

## Hot path ranking — synthesis

Ranked by measured us/s of profiled time, largest contributors first:

1. **`Update` → `Game`**: 345,168 us/s avg (73% of the largest bucket, `Update`'s 470,270 us/s). Klei's top-level driver; likely wraps nested sim work already counted elsewhere — treat as a signpost, not an isolated fix target.
2. **`200s` → Sublimates**: 34,074 us/s avg, 37% of the single largest *named* Sim/Render bucket (200s, 91,568 us/s total). Sublimation/off-gassing runs every 200ms across the whole map.
3. **`*r` → BrainScheduler**: 24,609 us/s avg, 74% of the every-render-tick bucket. Runs on essentially every frame (8,449 calls/s).
4. **`*s` → Navigator+StatesInstance**: 22,468 us/s avg, 91% of the every-sim-tick bucket. Pairs with #3 — both are pathing/brain related and both run at full tick rate, and the Path Cache section above shows the colony was in an 85%-of-session cache-miss regime, meaning this cost is largely uncached, fresh pathfinding work.
5. **`LateUpdate` → World**: 145,683 us/s avg, 55% of LateUpdate (266,539 us/s total).
6. **Gen0 GC pauses**: 6.41/min, correlated with 22 visible >1.3s stalls — a recurring, frequent stutter source independent of and far more common than the autosave.
7. **`1000s` → ScenePartitioner** and **`4000s` → FastReachabilityMonitor**: smaller in absolute terms (9,178 and 5,925 us/s avg respectively) but each dominates its own bucket (20% and 80%) and FastReachabilityMonitor is FastTrack's own instrumentation, notable because the profiling tool itself is a measurable cost.

## Caveats

- **Units and translation:** all `us` figures are SUMS of Stopwatch-measured time inside the profiled method across the ~20-25 frames that occur within each 1-second window (mean frame time ~44ms ⇒ ~22.7 frames/s). To get a rough per-frame figure, divide the avg-us/s figures above by ~22.7. These are not directly a "% of frame budget" without that conversion.
- **Measurement overhead bias:** FastTrack's Debug Metrics instrument every profiled Update/LateUpdate/Sim/Render call with a Harmony prefix/postfix Stopwatch pair. That overhead is itself non-trivial at the call volumes seen here (e.g. 72,569 Update calls/s, 32,476 `200s` calls/s) and is **not present in normal (non-metrics) play**. Absolute us figures in this report are therefore inflated relative to an un-instrumented run; the *relative* ranking between subsystems is the reliable signal, not the absolute numbers.
- **Warm-up dropped:** first 5 samples (idx 0-4, `22:02:31`-`22:02:35`) excluded — Path Cache hit% still ramping, first `Update` tick anomalously large (2.74M us).
- **Autosave isolated:** 3 samples (idx 219-221, `22:06:29.051`-`22:06:30.742`) excluded from steady-state and reported separately — confirmed against plain-text autosave log lines, identified by a 3.53s real-time stall and the only sustained heap-rise-without-GC-drop in the session.
- **Build type:** DEBUG — GC/heap lines were present for all 262 samples, so this report has full GC/heap visibility (no Release-build degradation to caveat).
- **Path Cache bimodal:** the steady-state average (18.43%) blends a 99.8%-hit warm-up tail (idx 5-40) with an 85%-of-session collapse to 4.1% hit rate (idx 41-258) — see the Path Cache section for the regime breakdown; do not quote the blended average as "the" cache hit rate.
- **`Game`/`World` wrapper classes:** time attributed to `Game` and `World` in the `Update`/`LateUpdate` breakdown likely includes nested calls into systems also profiled separately in the Sim/Render buckets (FastTrack profiles multiple layers independently); the two breakdowns are not strictly additive against each other.
- **Path Probes inactive:** 0us executed/saved across all steady-state samples — this capture window happened not to exercise async path probing; absence of evidence here is not evidence of absence in general play.
