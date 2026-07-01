# Candidate 0005: MinionTodoSideScreen recomputes colony-wide chore list every frame

- Status: QUEUED (built, Stage-4 ADVANCE; pending in-game A/B)
- Target: `MinionTodoSideScreen.ScreenUpdate` / `MinionTodoSideScreen.PopulateElements` — decompile `harness/decompiled/Assembly-CSharp/MinionTodoSideScreen.cs` (~:125-129 ScreenUpdate, ~:159 the 0.1s UIScheduler tick)
- Risk class: Prefix (low — removes a redundant per-frame call; no state mutation, no widget lifecycle change)
- Gating flag: `SideScreenOpts` (existing UI-optimization flag)
- Collision with Peter's rewrites: none — no Fast Track patch touches `MinionTodoSideScreen` (grep-confirmed). Unclaimed.

## What and why
The Errands tab (`MinionTodoSideScreen`) auto-opens for every selected Duplicant (`DetailsScreen.OnSelectObject`). While it is open, `PopulateElements` runs on TWO paths: unconditionally every frame via `ScreenUpdate(topLevel)` (which ignores its `topLevel` arg), PLUS a redundant self-rescheduling 0.1s `UIScheduler` tick. Each call rebuilds a list from `ChoreConsumer.GetLastPreconditionSnapshot()`, whose contexts come from `ChoreConsumer.FindNextChore` aggregating every provider including `GlobalChoreProvider.Instance` — so `GlobalChoreProvider.CollectChores` tests **every pending global chore in the dupe's world** (dig, build, empty-pipe, harvest, deduped fetch/sweep) against this one dupe's preconditions.

The waste is a frequency mismatch: the sim only refreshes this snapshot on an occasional brain tick, but the UI re-iterates and re-renders the whole colony-scaled list ~60×/sec (+10×/sec redundant) for as long as the window stays open. Cost scales with the colony's open-errand backlog — matching the user's "seems infinitely long" and "only while the window is open, on any dupe."

Not a leak: `PreconditionSnapshot.Clear()` runs at the top of every `FindNextChore`, so the list is bounded — it is just colony-scaled and re-rendered far too often.

## Fix
Drop the per-frame `PopulateElements()` call in `ScreenUpdate` and rely on the existing 0.1s `UIScheduler` cadence (already present, already correct — the per-frame call is purely redundant with it). Net effect: ~6× fewer full colony-chore-list rebuilds while the Errands tab is open, no visible behavior change (10Hz refresh is imperceptible). Gate under `SideScreenOpts`.

VERIFY BEFORE MERGE:
1. Confirm the 0.1s `UIScheduler` tick actually calls `PopulateElements` (so removing the per-frame call still keeps the panel fresh). If the 0.1s tick does something else, do NOT remove the per-frame call blindly — instead throttle it (e.g., populate at most every 0.1s).
2. `ScreenUpdate(topLevel)` engine semantics (base `KScreen`/`KScreenManager` not in decompile): the "every frame" claim is inferred from standard Klei screen-stack behavior. The Profile hook will confirm the call frequency empirically.

## Static gates (Stage 3)
Confirmed from decompile (`MinionTodoSideScreen.cs`):
- (a) `PopulateElements` (private, `void PopulateElements(object data = null)`, line 156) is the expensive rebuild: pulls `ChoreConsumer.GetLastPreconditionSnapshot()`, sorts/merges `failedContexts` + `succeededContexts` into a pooled list, then iterates the whole list to update/instantiate `MinionTodoChoreEntry` widgets per priority group. Confirmed expensive/colony-scaled per the design doc's mechanism trace.
- (b) Called on both paths: `ScreenUpdate(bool topLevel)` (line 125-129) calls `PopulateElements()` unconditionally every frame (ignores `topLevel`), and `PopulateElements` itself re-arms a 0.1s `UIScheduler.Instance.Schedule("RefreshToDoList", 0.1f, PopulateElements)` at its own top (line 158-159) - so the 0.1s tick target is the *same* private method, confirmed.
- (c) Idempotent: full rebuild from the snapshot each call (`ChoreConsumer.PreconditionSnapshot`, cleared/rebuilt in `FindNextChore` elsewhere), no per-call side effect required beyond the widget sync it performs and the scheduler re-arm at its own top.

Extra finding not in the original design doc: because `PopulateElements` re-arms its own 0.1s handle via `refreshHandle.ClearScheduler()` + `Schedule(...)` at the *top* of its body, the per-frame `ScreenUpdate` call was continuously canceling and re-arming that handle every frame - meaning the "0.1s tick" essentially never got to fire on its own while `ScreenUpdate` ran every frame. Because of this, dropping only the per-frame `ScreenUpdate` call (the design doc's first suggested fix) is riskier to reason about than throttling `PopulateElements` itself.

**Approach chosen: throttle `PopulateElements` directly via Harmony Prefix (not skip ScreenUpdate).** Rationale: throttling the method itself is robust regardless of what else `ScreenUpdate` does (nothing else here, but this is more defensive), and it has a self-correcting side effect: skipped Prefix calls return `false` and never reach the original body, so they never call `ClearScheduler()`/`Schedule()` either. That means the *first* real call arms the 0.1s handle once, then subsequent same-frame/same-window calls are skipped without re-arming, and the previously-armed 0.1s handle is left alone to fire on its own schedule - naturally collapsing both redundant call paths into a single ~10 Hz cadence instead of ~60 Hz.

Patch: `FastTrack/UIPatches/MinionTodoSideScreenPatches.cs`, namespace `PeterHan.FastTrack.UIPatches`.
- `[HarmonyPatch(typeof(MinionTodoSideScreen), "PopulateElements")]` (string form - target is private, so `nameof` is not usable from outside the assembly).
- `internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;` (existing UI-optimization flag).
- `internal static bool Prefix()`: no target params needed (throttle is purely time-based, doesn't need `__instance` or the `data` arg). Compares `UnityEngine.Time.unscaledTime` against a static `lastPopulate` float; if `now - lastPopulate >= THROTTLE` (`const float THROTTLE = 0.1f`, matches the existing scheduler cadence), updates `lastPopulate = now` and returns `true` (run original); otherwise returns `false` (skip - the original's rebuild and its scheduler re-arm both don't run, letting the already-armed 0.1s handle fire on time instead).

Also added the DEBUG profiling hook in `FastTrack/FastTrackMod.cs`: `harmony.Profile(typeof(MinionTodoSideScreen), "PopulateElements");` in the existing `#if DEBUG` `Profile(Harmony harmony)` block, alongside the candidate 0003 hooks.

Build: `dotnet build FastTrack/FastTrack.csproj -c Debug -p:Platform=Mergedown` -> 0 errors, only pre-existing unrelated warnings (obsolete API warnings in PLibUI/SimpleInfoScreenWrapper, MSB3245/3277 assembly-version-conflict warnings in the PLib netstandard2.1 sub-projects - none touch the new file).

## Review (Stage 4)
**ADVANCE** (Opus, live-source verified). Key verifications:
- Self-sustaining: `UIScheduler` clock is `Time.unscaledTime` — the SAME clock the Prefix reads. Body runs → arms one-shot handle for `now+0.1`; handle fires when `unscaledTime >= now+0.1` → Prefix necessarily allows (same frame-constant clock, no float-epsilon stall) → re-arms. Loop closes through the scheduler alone; `ScreenUpdate` not required to keep it alive.
- Single instance: DetailsScreen creates `MinionTodoSideScreen` once and reuses it; only one selected target at a time → the static `lastPopulate` cannot be starved by a second panel.
- Paused: strict win (10 Hz vs vanilla 60 Hz); monotonic non-negative time, no NaN; post-load always allows.
- Handle lifecycle unchanged: `OnShow(false)`/`ClearTarget`/`SetTarget` all `ClearScheduler()` independent of the Prefix, so closing still cancels the tick (no fire-forever); one-handle invariant preserved on both skip and run.
- Prefix semantics: `PopulateElements` is void, idempotent full rebuild, no return consumers; skipped frames drop nothing. Gated `SideScreenOpts`; no collision (grep-clean).
- No Critical/Important. **Minor (verify in A/B, no code change):** shared `lastPopulate` can show the previous dupe's Errands list for ≤100 ms on a fast dupe-switch (<0.1 s) before it snaps — bounded, self-healing.

## Measurement (Stage 5)
Pending. Add `harmony.Profile(typeof(MinionTodoSideScreen), nameof(PopulateElements))` (DEBUG) to size before/after: calls/sec and total us while a dupe Errands tab is open, plus the reduction in `Game.Update` cost while open. User A/B: does the while-open slowness disappear?

## Outcome
Open — DESIGN, fix specced (low risk, unambiguous mechanism). Build + Opus review + in-game A/B.
