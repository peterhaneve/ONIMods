# Fast Track Debug Metrics — Path-Cache Fix, Post-Deploy Capture

Comparison of a fresh Debug Metrics capture (precise dirty-cell path-cache invalidation deployed) against the baseline documented in `profiling-baseline.md` (union-bbox wipe). Same save, ~1 in-game cycle, Debug Metrics ON in both runs.

## Source log

`%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`, save `Friends.sav` loaded `23:24:45`/loaded-complete `23:25:05.905`. `[FT-BENCH]` lines: 0 (none present, nothing to exclude). `[PLib/FastTrack]` Debug Metrics lines: 3,090 raw matches, fully parsed (not naive-grepped — see Parsing method below).

**Capture window:** 270 `Render1000ms` samples, `23:25:27.423` – `23:30:22.035` = **294.6s** (vs baseline's 262 samples / ~281s). Comparable order of magnitude.

**Build type:** DEBUG. Managed Heap / Total Reserved / System Used present on all 270 samples. 35 `Garbage collection occurred!` lines attributed inside the sample window (38 raw matches in the log; 3 land outside any closed sample's window and don't affect the rate below).

## Parsing method

Re-derived a structured parser (`scratchpad/parse2.py`) rather than reusing the prior session's `parse_fasttrack.py` (not present in this checkout). Confirmed against `FastTrack/Metrics/DebugMetrics.cs`: each `Render1000ms` tick logs one multi-line `Sim/Render:` block (8 ordered buckets: `*r, 200r, 1000r, *s, 33s, 200s, 1000s, 4000s`) and one `Update:`/`LateUpdate:` block, where the bucket/Update/LateUpdate header line (`name[calls/totalUSus|min/maxUS us]`) is followed by un-prefixed per-class detail lines (`" ClassName: NNNus"`) and an `"  and N more..."` line, terminated by the next bucket header or the next timestamped `[PLib/FastTrack]` line. Sample count from this parser (270) matches the independent `Managed Heap KB:` line count exactly (270 = 270) — no samples lost or merged.

## Path Cache hit ratio — same bimodal shape, but the "active" regime is now almost the whole capture

| Regime | Samples (idx) | Span | Avg hit% (simple) | Weighted (Σhits/Σtotal) | Min/Max |
|---|---|---|---|---|---|
| **Active/steady** | 0–258 | `23:25:27.423` – `23:30:07.247` (~280s, **96% of the capture**) | **3.7%** | 4.2% | 0.0% / 47.8% |
| Tail (recovering, near manual save) | 259–269 | `23:30:08.240` – `23:30:22.035` (~14s) | 88.3% | 88.5% | 34.2% / 100.0% |

Unlike the baseline, **there is no extended high-hit warm-up regime** in this capture. Baseline had idx 5-40 (~36s) sitting at 99.8% before collapsing; here, sample 0 is already low (10.8%) and the series is noisy-low (0–48%, frequently exactly 0%) from the very first sample through idx 258. (Two early samples, idx 1–2, show a brief residual bump to ~47% — gone by idx 3 — too short to call a warm-up regime.) The first metric sample lands 21.5s after "Loaded" (`23:25:05.905`→`23:25:27.423`); whatever warm-cache period existed, if any, happened before metrics started recording.

The tail recovery (idx 259-269, climbing 34%→100% over ~8s and holding) starts at `23:30:08.240`, **before** the manual save fires (`Saved to [...]` at `23:30:19.086`, screenshot at `23:30:20.686`) — consistent with the same pre-save/quiescent-period pattern noted in the baseline's autosave tail, not autosave itself this time (no autosave events in this window; this is a manual save near session end). Excluded from the active-window average for the same reason the baseline excluded its autosave window.

## VERDICT: Path Cache hit% — NOT a win

**Active-window hit% (the number comparable to baseline's 4.1%): 4.1% → 3.7%** (simple average, same method as baseline; weighted-by-call-volume gives 4.2%, also flat). This is statistically indistinguishable from the pre-fix baseline — within the same noisy 0–48% per-sample range, same order of magnitude. The fix did not move the needle on hit rate in this capture.

## Hot-path deltas — also did not improve

| Hot path | Baseline avg us/s | After avg us/s | Δ |
|---|---|---|---|
| `*r` → BrainScheduler | 24,609 | 26,379 | **+7.2%** (worse) |
| `*s` → Navigator+StatesInstance | 22,468 | 24,850 | **+10.6%** (worse) |

Both dominant hot paths got slightly *more* expensive, not less — consistent with the flat-to-nonexistent path-cache improvement above (pathing is still running mostly uncached).

**Caveat — broad context, not cache-specific:** nearly every Sim/Render bucket in this capture runs ~5-10% hotter than baseline across the board (e.g. `200s` 91,568→100,178 us/s, +9.4%; `1000s` 46,024→48,452 us/s, +5.3%; `*r` total 33,447→35,415 us/s, +5.9%), suggesting this cycle's colony was somewhat busier overall (more creatures/events), not that the fix actively regressed things. But that general uplift cannot explain away the core finding: path-cache hit% itself, the metric the fix directly targets, did not improve at all.

**This warrants checking whether the fix actually took effect in the deployed build** (DLL timestamp/version actually loaded, not a stale copy in the mods folder) before concluding the dirty-cell invalidation approach itself is ineffective.

## The mid-capture spike (dupe-click candidate)

Capture midpoint is `23:27:54.7` (span `23:25:27.4`–`23:30:22.0`). The single largest `Update` bucket spike in the entire active window lands at **sample idx 134, `23:27:54.930`** — i.e. almost exactly the capture midpoint.

- `Update` total: **1,560,715 us** vs a ~440K us/s median for the window — **~3.5x** a typical tick.
- Nearly all of the excess is in the **`Game`** class specifically: 1,503,282 us this tick vs baseline's 345,168 us/s *average* (this single tick alone is ~4.4x the baseline's per-second average for `Game`).
- **Not GC:** `gc_count` is 0 for this sample; heap is *falling* (-125,952 KB/s), the opposite signature of an allocation/collection spike.
- **Not sim/pathing:** every other bucket is at or *below* its typical level this same tick (`*s` 12,056 us vs ~27,126 us/s avg; `200s` 40,661 us vs ~100,178 us/s avg; `BrainScheduler` and `Navigator+StatesInstance` both unremarkable). The colony simulation was quiet that second — the cost is isolated to `Update`→`Game`.

Four more spikes of the same shape (`Update`→`Game` dominating, 85-95% of an inflated `Update` total, no GC, no elevated sim buckets) recur through the capture at roughly 30-90s intervals (idx 49 `23:26:23`, 76 `23:26:51`, 169 `23:28:31`, 254 `23:30:03`), each with `Game` itself at ~1.0-1.1M us. This is consistent with a periodic UI-side cost — most plausibly duplicant selection / info-panel population — that runs inline inside Klei's `Game.Update()` and isn't broken out into its own profiled class by FastTrack's patches (FastTrack profiles `Game`'s own `Update` method as one bucket; anything Klei runs directly in that method body, rather than delegating to a separately-patched MonoBehaviour, rolls up into `Game` undifferentiated).

**Characterization:** not a GC spike, not a sim/pathing spike — an `Update`→`Game` cost spike, magnitude ~3.5x a typical tick (~2.3-3.5x across the 5 occurrences), recurring roughly every 30-90s, with the clearest single instance landing within fractions of a second of the capture's midpoint (matches the user's report). Likely subsystem: duplicant selection / info-panel (`SelectTool`/details-screen) population running un-instrumented inside `Game.Update()`. This is a reasonable candidate for a future FastTrack patch target — but it would need a dedicated profiled hook on whatever Klei calls from `Game.Update()` during selection to pin down the exact method, since FastTrack currently only sees it as undifferentiated `Game` time.

## Sanity check — no pathing errors

`grep -ni "exception|nullreference|stale.path|pathcacher|navgrid"` against the full log (excluding the `[PLib/FastTrack]` metrics lines themselves): **0 matches**. No exceptions, NullReferenceExceptions, or stale-path warnings around `NavGrid`/`PathCacher`/`Navigator` in this capture. GC rate: 35 events / 294.6s ≈ 7.1/min, comparable to baseline's 6.4/min (not a regression).
