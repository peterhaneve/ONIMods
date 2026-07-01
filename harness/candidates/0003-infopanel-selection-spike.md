# Candidate 0003: Duplicant-selection info-panel population spike

- Status: MEASURED (spike attributed to DetailsScreen.Refresh, intermittent 253 ms; hypothesis corrected — see Measurement)
- Target (instrument first): `SimpleInfoScreen.OnSelectTarget` / `SimpleInfoScreen.OnDeselectTarget` / `DetailsScreen.Refresh` — decompile `harness/decompiled/Assembly-CSharp/SimpleInfoScreen.cs:417,457` and `DetailsScreen.cs:657`
- Risk class (this candidate): Prefix/Postfix Stopwatch instrumentation only (DEBUG + Metrics gated). A follow-up *fix* (status-item pooling) would be Prefix/state + Unity widget-lifecycle — higher risk, deferred until numbers exist.
- Gating flag: instrumentation = `Metrics` (DEBUG only). Any eventual fix would live under `SideScreenOpts` (the flag that already gates `SimpleInfoScreenWrapper`).
- Collision with Peter's rewrites: partial — `SimpleInfoScreenWrapper` already rewrites the *refresh* path under `SideScreenOpts`; it does **not** touch the *selection* widget lifecycle. A status-item pooling change would sit next to his wrapper and could collide with a future upstream rewrite of `SimpleInfoScreen` status handling. Check upstream before building.

## What and why

The recurring `Update -> Game` spike (~3.5x a normal tick, every ~30-90s, no GC, no elevated
sim/pathing buckets — see `profiling-after-fix.md` §"The mid-capture spike") lands when a new
Duplicant is selected and the info panel repopulates. Fast Track does not break this out into its
own profiled class, so it rolls up as undifferentiated `Game` time. This candidate's job is to
**attribute** that time to a concrete method, because a confidently-wrong fix wastes a game-capture
cycle.

### Selection -> populate flow (verified in decompile)

`SelectTool.Select` (`SelectTool.cs:111`) fires the SelectObject event -> `RootMenu.OnSelectObject`
-> `RootMenu.Refresh` -> `DetailsScreen.Refresh(go)` (`DetailsScreen.cs:657`) and, via the active
tab, `TargetPanel.SetTarget` -> `SimpleInfoScreen.OnSelectTarget` (`SimpleInfoScreen.cs:417`). All of
this runs synchronously inside the click-input frame, which is driven from `Game.Update()`, so it is
attributed to `Game`. The default tab for a dupe is Errands/SimpleInfo, so the visible populated
panel is `SimpleInfoScreen` (plus the side-screen bar).

### The genuinely expensive per-selection operations

1. **Status-item widget teardown + rebuild — leading hypothesis.**
   `SimpleInfoScreen.OnDeselectTarget` (`:457-490`) destroys every prior status widget
   (`statusItem.Destroy(immediate:true)` in two loops), then `OnSelectTarget` (`:417-455`) iterates
   the status-item group **twice** (Main, then non-Main) calling `DoAddStatusItem` -> `new
   StatusItemEntry(...)`. Each entry ctor (`SimpleInfoScreen.cs:57-73`) does one
   `Util.KInstantiateUI(status_item_prefab)` plus **four** `GetComponentInChildren` walks (LocText,
   ToolTip, Image, KButton) plus a `SimAndRenderScheduler.instance.Add`. A dupe carries ~10-20 status
   items, so a single selection is ~N `Destroy` + ~M `KInstantiateUI` + ~4M hierarchy walks. Unity
   GameObject Instantiate/Destroy is the most expensive primitive on the path. Then
   `statusItemPanel.scalerMask.UpdateSize()` + `Refresh(force:true)` re-lays-out the panel.

2. **`DetailsScreen.Refresh` side-screen pass** (`:657-741`). Clears `sortedSideScreens`, loops all
   registered side screens (`IsValidForTarget`, lazy `Util.KInstantiateUI<SideScreenContent>` on
   first use, `SetTarget`+`Show` on each valid one), then a delegate `Sort` and per-screen
   `SetSiblingIndex` (each reorder dirties layout). Runs once per selection.

3. **`DetailsScreen.UpdateTitle` -> `PinResourceButton_Refresh`** (`:611,565`) runs a triple nested
   loop over `GameTags.MaterialCategories`/`CalorieCategories`/`UnitCategories`, each calling
   `DiscoveredResources.GetDiscoveredResourcesFromTag(...).Contains(tag)` — for a dupe this scans all
   categories to return nothing. Short-circuitable.

### What Fast Track already does vs. does not (this branch)

Already optimized — the **per-frame REFRESH** path (all under `SideScreenOpts`):
- `SimpleInfoScreen.Refresh` is fully **replaced** (`Refresh_Patch.Prefix` returns `false`,
  `SimpleInfoScreenWrapper.cs:316`): reuses the existing `statusItems` list and just calls
  `statusItems[i].Refresh()`; caches `LastSelectionDetails`; freezes/pools process-condition rows;
  Ryu/`FormatStringPatches` string building; pooled descriptor lists under `AllocOpts`.
- `MinionVitalsPanel`, `AdditionalDetailsPanel`, `MinionPersonalityPanel` refreshes are likewise
  change-gated and cached.

**Not** optimized — the **per-SELECTION populate** path:
- `SimpleInfoScreen.OnSelectTarget` is patched only with a **void Prefix**
  (`SimpleInfoScreenWrapper.Patches.cs:54`) that adds storage/geyser caching; the vanilla body
  (status-item destroy+instantiate, forced Refresh, world/process panels) **still runs in full**.
- `OnDeselectTarget`, `DoAddStatusItem`, `StatusItemEntry`, `OnAddStatusItem`, `scalerMask` — **no
  fork patch touches any of them** (grep-confirmed). Status widgets are rebuilt from scratch every
  selection; nothing pools them across selections.
- `DetailsScreen.OnSelectObject`/`Refresh` side-screen loop + sort + `SetSiblingIndex` — vanilla; the
  `DetailsPanelWrapper` caches component refs and rewrites `SortSideScreens` but the per-screen
  `SetTarget`/`Show`/reorder still runs.

Correction to an early assumption: the **MinionPersonalityPanel** (traits/attributes/resume, ~6
`Commit`/layout sub-panels) is **NOT** on the default dupe-selection frame. `DetailTabHeader.ChangeTab`
sets `SetTarget(null)` on non-active tabs, so the Bio panel only populates when the user clicks the
Bio tab (then on a 1s scheduler). Its `Commit`/layout cost is a *tab-click* spike, not the
*selection* spike. Descriptor/effects/requirements row building also early-returns for a
`MinionIdentity`, so it is a *building*-selection cost, not a dupe cost.

### Diagnosis

The spike is category **(a) a per-selection one-time population cost that is largely inherent**
(you must show the new dupe's status items and side screens), **with a concrete category-(b)
component Fast Track could pool but doesn't**: the `StatusItemEntry` destroy+instantiate churn in
`OnDeselectTarget`/`OnSelectTarget`, plus the side-screen `SetTarget`/`Show`/sort/`SetSiblingIndex`
pass. It is **not** category (c) (per-frame-while-open) — that path is already replaced by the
wrapper's `Refresh`. This is consistent with the profiling signature: the cost fires on the
selection frame and disappears afterward, not every frame the panel is open.

Honest status: this is a **strong hypothesis, not a measured attribution.** The after-fix capture
sees only undifferentiated `Game` time. Do not build the pooling fix before instrumenting.

## Predicted impact

If the status-item rebuild dominates (leading hypothesis), pooling `StatusItemEntry` widgets across
selections would remove ~N `Destroy` + ~M `KInstantiateUI` + ~4M `GetComponentInChildren` per
selection — plausibly most of a ~3.5x spike on a status-item-heavy dupe. But magnitude is
unconfirmed; the side-screen pass or the `PinResourceButton` scan could be comparable. Numbers first.

## Patch

**No fix patch in this candidate.** The proposed next step is instrumentation only. Add to the
existing `#if DEBUG` / `RunAt.AfterModsLoad` Profile block in `FastTrack/FastTrackMod.cs:37-40`
(mechanism: `ExtensionMethods.Profile(this Harmony, Type, string)` wraps the method with a
Stopwatch Prefix/Postfix that logs inclusive ticks to `DebugMetrics.TRACKED`, surfaced in the
`Methods Run:` log line every real second — same machinery that already tracks
`Game.UnsafeSim200ms`, `ConduitFlow.Sim200ms`, `EnergySim.EnergySim200ms`):

```csharp
harmony.Profile(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnSelectTarget));
harmony.Profile(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnDeselectTarget));
harmony.Profile(typeof(DetailsScreen),   nameof(DetailsScreen.Refresh));
// optional finer decomposition:
harmony.Profile(typeof(DetailsScreen),   nameof(DetailsScreen.UpdateTitle));
```

On a selection frame these appear as e.g. `SimpleInfoScreen.OnSelectTarget[1/NNN,NNNus|...]` in
`Methods Run:`, directly pulling the cost out of undifferentiated `Game` time. Timings are inclusive,
so `OnSelectTarget` captures the forced `Refresh` + world/process panels; comparing it against
`DetailsScreen.Refresh` and `OnDeselectTarget` decomposes the spike.

Instrumentation notes (BUILD stage):
- All three targets are single overloads (`Refresh(GameObject)` is the only `Refresh`; `RefreshTitle`
  is separate). `Profile` uses `GetMethod(name, ...)` with no arg types, so single overloads are
  unambiguous.
- `OnSelectTarget`/`OnDeselectTarget` are `protected override`; `DoAddStatusItem` is `private`.
  `Profile` uses `PPatchTools.BASE_FLAGS | Instance | Static` (includes NonPublic), so all resolve.
- Requires a DEBUG build with the `Metrics` option enabled — exactly the config the baseline and
  after-fix captures already used.

## Unit test

Not unit-testable: game-coupled UI + Unity Instantiate/Destroy. Attribution is via in-game
Debug-Metrics capture, not a unit harness.

## Static gates (Stage 3)

- Compiles: n/a (no fix code yet; the 3-4 `Profile` lines are trivial DEBUG-only additions)
- Unit test: na
- IL verify: na (no transpiler)
- Thread-safety check: instrumentation is main-thread UI only; `DebugMetrics.TRACKED` is a
  `ConcurrentDictionary` — safe
- Save-compat: na (no serialized-layout change)

## Review (Stage 4)

Pending — send this DESIGN to a fresh reviewer before promoting to BUILD. Key thing to challenge:
is the status-item rebuild really the dominant term, or could the side-screen `SetTarget`/sort pass
or the `PinResourceButton` category scan be comparable? The instrumentation is designed to answer
exactly that, so the review question is whether the 3-4 chosen hooks are sufficient to decompose the
spike (add `DoAddStatusItem` if `OnSelectTarget` needs splitting from its forced `Refresh`).

## Measurement (Stage 5)

DONE (attribution capture, DEBUG + Metrics, several Duplicant selections). `Methods Run:` timings:
- `SimpleInfoScreen.OnSelectTarget`: steady **~1.1 ms** per selection (1,057–1,193 us).
- `SimpleInfoScreen.OnDeselectTarget`: **~0.05 ms** (47–90 us) — negligible.
- **`DetailsScreen.Refresh`: usually ~1.3–1.5 ms, but ONE selection = 253,256 us ≈ 253 ms.**

**Hypothesis CORRECTED:** the status-widget teardown/rebuild in `OnSelectTarget` is NOT the spike — it is a cheap, steady ~1 ms. The spike is `DetailsScreen.Refresh`, and it is **intermittent** (most calls ~1.5 ms, occasional ~253 ms). So the cost is not "every selection is slow" but a specific pathological case (a particular target, a cold side-screen instantiation, or a specific side screen doing heavy `SetTarget`/`Show`/layout). Needs one more attribution level: profile `DetailsScreen.Refresh`'s internals (title/pin scan vs the side-screen `SetTarget`/`Show` loop) OR identify the triggering target.

## Outcome

Open — measured, hypothesis corrected. Next: attribute WITHIN `DetailsScreen.Refresh` (what makes it 253 ms on some targets). The `OnSelectTarget` status-item pooling idea is DROPPED as the primary lever (it is only ~1 ms). Pending: user context on what triggered the 253 ms selection (specific dupe? first click after load? a tab open?), plus a sub-method profiling pass on `Refresh`.

## Job-queue investigation

User's decisive clue ("listing/calculating the job queue on a dupe's info window is slow, queue seems infinitely long, only while the window is open, any dupe") points at a **different** panel than `SimpleInfoScreen`/side-screen-sort: the **Errands tab**, `MinionTodoSideScreen` (`harness/decompiled/Assembly-CSharp/MinionTodoSideScreen.cs`). This is the panel that lists the dupe's current task plus its priority-sorted chore queue, and it is the tab `DetailsScreen.OnSelectObject` (`DetailsScreen.cs:355-374`) auto-selects for **every** Duplicant selection (`SelectSideScreenTab(SidescreenTabTypes.Errands)` when the target has `MinionIdentity`) — this alone explains "happens on any dupe," since it is not an optional tab the user had to click into.

### The mechanism (confirmed in decompile, not FastTrack's doing)

`MinionTodoSideScreen.PopulateElements` (`:156-231`) is invoked two ways, both while the Errands tab is showing:
- **Every frame**, unconditionally: `ScreenUpdate(bool topLevel)` (`:125-129`) calls `PopulateElements()` regardless of the `topLevel` argument — it never gates on whether this screen is actually the frontmost/interactive one.
- **Also every 0.1 s** via a self-rescheduling `UIScheduler.Instance.Schedule("RefreshToDoList", 0.1f, PopulateElements)` (`:159`) that re-arms itself on every call — redundant with the per-frame path, minor next to it.

Each call: allocates a pooled list, then does `pooledList.AddRange(failedContexts); pooledList.AddRange(succeededContexts)` from `choreConsumer.GetLastPreconditionSnapshot()` (`ChoreConsumer.cs:135-138`, returns the live `preconditionSnapshot` field), then iterates the **entire combined list** calling `GameUtil.AreChoresUIMergeable` and a linear `PriorityGroupForPriority` scan (11 priority groups + a dictionary lookup) per entry, and reuses/pools the actual `MinionTodoChoreEntry` widgets (`GetChoreEntry`, `:233-250` — widget reuse itself is fine, not the cost).

**Why the list "seems infinitely long":** `preconditionSnapshot.failedContexts`/`succeededContexts` are populated by `ChoreConsumer.FindNextChore` (`ChoreConsumer.cs:303-329`) from **all of `providers`**, and every dupe has `GlobalChoreProvider.Instance` in that list (`MinionModifiers.cs:55`, `FetchDroneConfig.cs:308`, `SolidTransferArm.cs:196` all call `AddProvider(GlobalChoreProvider.Instance)` — for a normal colonist this comes from `MinionModifiers`, i.e. **every** dupe). `GlobalChoreProvider.CollectChores` (`GlobalChoreProvider.cs:260-282`) tests **every pending global chore in that dupe's world** — every dig, build, empty-pipe, uproot/harvest-type chore via `base.CollectChores`, plus every deduplicated fetch/sweep/storage-fetch (`fetches` list) — against this one dupe's preconditions. So the "queue" the user is watching build is not a bug/leak; it is **colony-wide**: it scales with the base's total open-errand backlog (every dig marker, every unbuilt blueprint, every sweep-able item, every empty-pipe request in that world), re-tested against this dupe. In a large/backlogged base this can genuinely be hundreds of `failedContexts` entries (chore is real but fails preconditions for this specific dupe: skill, permission, reachability, shift) — "infinitely long" is the player's read of "keeps growing as the base grows," not a literal unbounded collection (confirmed: `PreconditionSnapshot.Clear()` runs at the top of every `FindNextChore`, so it does not leak across ticks).

**Verdict on the red flag:** expensive-but-correct, not a leak. The bug (in the sense of "should not cost this much") is that the **UI re-derives and re-renders this colony-scaled list every single frame** the Errands tab is open, even though `preconditionSnapshot` only actually changes once per brain tick (an infrequent, scheduled sim event), not once per render frame. That mismatch — sim updates the snapshot occasionally, UI reprocesses it 60x/sec — is the real waste, and it directly matches the user's "only happens when the window is open" observation (`ScreenUpdate` only fires while this tab is visible) plus "any dupe" (every dupe shares the same `GlobalChoreProvider.Instance`-driven backlog size).

### Fast Track coverage (grep-confirmed on this branch)

Not touched. `FastTrack/UIPatches/` has no patch on `MinionTodoSideScreen`, `PopulateElements`, `GetChoreEntry`, or `PriorityGroupForPriority` (grep across `FastTrack/*.cs` for `MinionTodoSideScreen` returns nothing). Fast Track *does* already optimize the **sim side** of chore-finding — `FastTrack/PathPatches/AsyncBrainGroupUpdater.cs` reads `GlobalChoreProvider.Instance` and runs brain/chore-finding asynchronously/multithreaded — but that only speeds up producing `preconditionSnapshot`; it does nothing about the UI re-consuming that snapshot every frame. `DetailsPanelWrapper.cs` (the fork's side-screen-lifecycle wrapper) doesn't reach into `MinionTodoSideScreen` either — its `SideScreenContent` reference is a generic instantiation helper, unrelated. This is a clean, unclaimed candidate: no collision with existing wrapper code.

### Proposed next step

Mechanism is unambiguous enough to skip a confirm-only instrumentation pass and go straight to a **narrowly scoped Postfix/gate fix**, but add one measurement hook alongside it to size the win (this path was never profiled, unlike `DetailsScreen.Refresh`):

1. **Fix (risk class: Prefix, change-gated skip — low risk, no state mutation):** patch `MinionTodoSideScreen.ScreenUpdate` with a Prefix that skips the `PopulateElements()`-triggering call unless (a) `topLevel` is true (matches existing engine intent — currently ignored) and (b) a cheap dirty check says the snapshot actually changed since last render (e.g. cache `choreConsumer`'s last-seen `preconditionSnapshot` object-reference/count/a version counter bumped in `FindNextChore`, and skip re-population when unchanged). Simplest first cut: just throttle to the existing `UIScheduler` 0.1 s cadence and remove the per-frame call from `ScreenUpdate` entirely (the self-reschedule already provides ~10 Hz refresh, which is plenty for a chore list a human is reading) — this alone should cut the frame cost by ~5-6x (60 Hz -> 10 Hz) with no behavior change since the 0.1s scheduler already exists and runs the identical method.
2. **Gate:** new option under `SideScreenOpts` (matches how `SimpleInfoScreenWrapper` is gated), e.g. `SideScreenOpts.MinionTodoThrottle` or fold into the existing flag if it's a single bool.
3. **Measurement to add alongside:** `harmony.Profile(typeof(MinionTodoSideScreen), nameof(MinionTodoSideScreen.PopulateElements))` in the `#if DEBUG` block (`FastTrack/FastTrackMod.cs:37-40`), plus log `pooledList.Count` once (e.g. via a Postfix `DebugUtil` line gated on a verbosity flag) on a real save with an errand backlog, to confirm the per-call list size and get a before/after ms figure for the throttle change. This is the same `Methods Run:` mechanism already used for `DetailsScreen.Refresh` in this candidate.
4. **Confirm before merging:** verify `ScreenUpdate(topLevel)` really is called every frame regardless of visibility state by the base `KScreen`/`KScreenManager` (that base class lives outside the decompiled `Assembly-CSharp` here — inferred from standard Klei screen-stack behavior, not directly read). If engine semantics differ (e.g., `topLevel` already gates most of the stack and this screen simply never receives `topLevel=false` while shown), the throttle-to-10Hz approach is still valid and simpler to reason about than a dirty-check.

This is a distinct fix from the open `DetailsScreen.Refresh` 253 ms spike (per-selection, intermittent) — this one is a per-frame-while-open steady tax that scales with colony chore backlog, exactly the mechanism the user described. Recommend tracking it as its own candidate (0003b or a new number) since it targets a different method/panel than the rest of 0003's `DetailsScreen.Refresh` work, even though both were discovered investigating the same "dupe info panel is slow" report.
