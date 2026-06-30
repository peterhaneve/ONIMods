#!/usr/bin/env python3
"""Diff two Fast Track benchmark Player.log captures. ponytail: regex + statistics, no deps."""
import re, sys, statistics

LINE = re.compile(r"\[FT-BENCH\] frame=(\d+) ms=([\d.]+) gcMB=([\d.]+) gen0=(\d+)")

def summarize(path, skip=0):
    ms, gc, gen0 = [], [], []
    with open(path, encoding="utf-8", errors="ignore") as f:
        for line in f:
            m = LINE.search(line)
            if m:
                ms.append(float(m.group(2)))
                gc.append(float(m.group(3)))
                gen0.append(int(m.group(4)))
    # Drop warm-up/load frames: the first frames after a save load are multi-second
    # hitches that would swamp the mean. Skip them before computing any stat.
    ms, gc, gen0 = ms[skip:], gc[skip:], gen0[skip:]
    if not ms:
        raise ValueError(f"no [FT-BENCH] lines in {path} (after skipping {skip})")
    ms_sorted = sorted(ms)
    return {
        "frames": len(ms),
        "mean_ms": round(statistics.fmean(ms), 4),
        "p95_ms": round(ms_sorted[min(len(ms_sorted) - 1, int(0.95 * len(ms_sorted)))], 4),
        "gc_growth_mb": round(gc[-1] - gc[0], 4),
        "gen0": gen0[-1] - gen0[0],
    }

def main(a, b, skip=0):
    sa, sb = summarize(a, skip), summarize(b, skip)
    print(f"(skipped first {skip} frames)\n")
    print(f"{'metric':<14}{'A (before)':>14}{'B (after)':>14}{'delta':>14}")
    for k in ("mean_ms", "p95_ms", "gc_growth_mb", "gen0"):
        d = round(sb[k] - sa[k], 4)
        print(f"{k:<14}{sa[k]:>14}{sb[k]:>14}{d:>+14}")
    verdict = "FASTER" if sb["mean_ms"] < sa["mean_ms"] else "SLOWER/EQUAL"
    print(f"\nverdict: {verdict} (mean frame time)")

if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser(description="Diff two Fast Track benchmark Player.log captures.")
    p.add_argument("before")
    p.add_argument("after")
    p.add_argument("--skip", type=int, default=0,
                   help="discard the first N frames per capture (warm-up / save-load hitches)")
    args = p.parse_args()
    main(args.before, args.after, args.skip)
