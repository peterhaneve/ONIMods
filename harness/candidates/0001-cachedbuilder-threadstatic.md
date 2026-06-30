# Candidate 0001: CACHED_BUILDER thread-local

- Status: PARKED (target already applied on baseline)
- Target: `FormatStringPatches.CACHED_BUILDER` — `FastTrack/UIPatches/FormatStringPatches.cs:42`
- Risk class: static/threading
- Gating flag: none (always on)
- Collision with Peter's rewrites: n/a — already applied on this baseline

## What and why
A single shared mutable `StringBuilder` reused across the format-replacement patches is not re-entrant. Making it `[ThreadStatic]` removes the re-entrancy hazard while preserving the zero-alloc win on the hot main-thread path (these format methods fire on nearly every tooltip / hover / info-panel update).

## Predicted impact
Near-zero frame-time delta; the value is correctness/hygiene, not speed.

## Patch
None applied. The target is **already** thread-local on this branch:
```csharp
[System.ThreadStatic] private static StringBuilder _CACHED_BUILDER;
private static StringBuilder CACHED_BUILDER => _CACHED_BUILDER ?? (_CACHED_BUILDER = new StringBuilder(32));
```
Introduced by commit `28d7864` ("Thread-safety hardening for concurrent access on Aquatic", Cringely, 2026-06-19), an ancestor of `fasttrack-aquatic-optim` (the PR #712 work). `git diff` on the file is empty.

## Unit test
Not unit-testable (game-coupled UI formatting).

## Static gates (Stage 3)
- Compiles: y (no change; baseline builds clean, 0 errors)
- Unit test: na
- IL verify: na (no transpiler)
- Thread-safety check: already per-thread via `[ThreadStatic]`
- Save-compat: na (no serialized-layout change)

## Review (Stage 4)
Not needed — there is no change to review.

## Measurement (Stage 5)
Not run — there is no change to measure (would compare HEAD against itself).

## Outcome
PARKED — the optimization is already applied on the baseline. The genuinely multi-threaded builder
(`FormatStringPatches.CACHED_BUILDER`, the actual crash site) plus its same-file siblings in
`FormatStringPatches.3.cs`, and the geyser `CACHE`/`HOTKEY_LOOKUP` dictionaries, were all hardened in
commit `28d7864`.

Tracked observation, deliberately NOT changed: 9 sibling shared-static `StringBuilder` fields remain
plain `static readonly` in main-thread-only UI panel/wrapper code —
`AdditionalDetailsPanelWrapper.cs:36`, `DetailsPanelWrapper.cs:35`, `HarvestSideScreenWrapper.cs:43`,
`MeterScreenPatches.cs:84`, `MinionPersonalityPanelWrapper.cs:40`, `SelectedRecipeQueuePatches.cs:36`,
`SimpleInfoScreenWrapper.cs:39`, `VirtualScrollPatches.cs:164`, and `DescriptorAllocPatches.cs:45` (BUFFER).
These run on the main thread only, so `[ThreadStatic]` would add per-thread allocation for no benefit.
Not a defect; do not "fix" without evidence of off-main-thread access.

Harness note: this candidate validates the loop's collision / already-applied detection — Stages 2/3
correctly refused to fabricate a diff for a change already present. The audit's seed-target list (read from
the stale root `FastTrack/` copy) is behind the real `aquatic-compat` baseline, where the easy seeds
(compile break, this thread-safety fix, the geyser cache race) are already done. Loop 1 needs a fresh
Stage-1 survey against the decompile cache + current source to find genuinely-unapplied optimizations.
