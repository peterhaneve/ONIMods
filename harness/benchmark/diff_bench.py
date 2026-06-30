#!/usr/bin/env python3
"""Diff two Fast Track benchmark Player.log captures. ponytail: regex + statistics, no deps."""
import re, sys, statistics

LINE = re.compile(r"\[FT-BENCH\] frame=(\d+) ms=([\d.]+) gcMB=([\d.]+) gen0=(\d+)")

def summarize(path):
    ms, gc, gen0 = [], [], []
    with open(path, encoding="utf-8", errors="ignore") as f:
        for line in f:
            m = LINE.search(line)
            if m:
                ms.append(float(m.group(2)))
                gc.append(float(m.group(3)))
                gen0.append(int(m.group(4)))
    if not ms:
        raise ValueError(f"no [FT-BENCH] lines in {path}")
    ms_sorted = sorted(ms)
    return {
        "frames": len(ms),
        "mean_ms": round(statistics.fmean(ms), 4),
        "p95_ms": round(ms_sorted[min(len(ms_sorted) - 1, int(0.95 * len(ms_sorted)))], 4),
        "gc_growth_mb": round(gc[-1] - gc[0], 4),
        "gen0": gen0[-1] - gen0[0],
    }

def main(a, b):
    sa, sb = summarize(a), summarize(b)
    print(f"{'metric':<14}{'A (before)':>14}{'B (after)':>14}{'delta':>14}")
    for k in ("mean_ms", "p95_ms", "gc_growth_mb", "gen0"):
        d = sb[k] - sa[k]
        print(f"{k:<14}{sa[k]:>14}{sb[k]:>14}{d:>+14}")
    verdict = "FASTER" if sb["mean_ms"] < sa["mean_ms"] else "SLOWER/EQUAL"
    print(f"\nverdict: {verdict} (mean frame time)")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: diff_bench.py BEFORE.log AFTER.log")
    main(sys.argv[1], sys.argv[2])
