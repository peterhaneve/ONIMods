/*
 * Copyright 2022 Peter Han
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using DETAILTABS = STRINGS.UI.DETAILTABS;
using NoteEntryKey = ReportManager.NoteStorage.NoteEntries.NoteEntryKey;
using ProcessConditionType = ProcessCondition.ProcessConditionType;
using ReportEntry = ReportManager.ReportEntry;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the simple info screen to avoid recalculating so much
	/// every frame.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed partial class SimpleInfoScreenWrapper : KMonoBehaviour, ISim200ms {
		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(128);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static SimpleInfoScreenWrapper instance;

		/// <summary>
		/// Collects the stress change reasons and displays them in the UI.
		/// </summary>
		/// <param name="stressEntries">The stress change entries in the report entry.</param>
		/// <param name="stressDrawer">The renderer for this info screen.</param>
		/// <returns>The total stress change.</returns>
		private static float CompileNotes(Dictionary<NoteEntryKey, float> stressEntries,
				DetailsPanelDrawer stressDrawer) {
			var stringTable = ReportManager.Instance.noteStorage.stringTable;
			var stressNotes = ListPool<ReportEntry.Note, SimpleInfoScreen>.Allocate();
			string pct = STRINGS.UI.UNITSUFFIXES.PERCENT;
			var text = CACHED_BUILDER;
			int n = stressEntries.Count;
			float total = 0.0f;
			foreach (var pair in stressEntries) {
				string hash = stringTable.GetStringByHash(pair.Key.noteHash);
				stressNotes.Add(new ReportEntry.Note(pair.Value, hash));
			}
			stressNotes.Sort(StressNoteComparer.Instance);
			for (int i = 0; i < n; i++) {
				var note = stressNotes[i];
				float stressDelta = note.value;
				text.Clear();
				if (stressDelta > 0.0f)
					text.Append(UIConstants.ColorPrefixRed);
				text.Append(note.note).Append(": ");
				stressDelta.ToRyuHardString(text, 2);
				text.Append(pct);
				if (stressDelta > 0.0f)
					text.Append(UIConstants.ColorSuffix);
				stressDrawer.NewLabel(text.ToString());
				total += stressDelta;
			}
			stressNotes.Recycle();
			return total;
		}

		/// <summary>
		/// Lists all geysers in the world.
		/// </summary>
		private Geyser[] allGeysers;

		private GameObject conditionParent;

		/// <summary>
		/// Caches labels for storage items.
		/// </summary>
		private readonly IDictionary<string, CachedStorageLabel> labelCache;

		/// <summary>
		/// The last daily report when stress was updated.
		/// </summary>
		private ReportManager.DailyReport lastReport;

		private ReportEntry lastStressEntry;

		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		/// <summary>
		/// If non-null, allows a much faster path in getting storage item temperatures.
		/// Not sure if every localization will allow this.
		/// </summary>
		private string optimizedStorageTemp;

		/// <summary>
		/// If true, the process condition rows will be mass frozen next frame.
		/// </summary>
		private volatile bool pendingProcessFreeze;

		/// <summary>
		/// Pooled headers that can be used for the process conditions.
		/// </summary>
		private readonly IList<ProcessConditionRow> processHeaders;

		/// <summary>
		/// Pooled rows that can be used for the process conditions.
		/// </summary>
		private readonly IList<ProcessConditionRow> processRows;

		/// <summary>
		/// The process conditions currently visible.
		/// </summary>
		private readonly IList<ProcessConditionRow> processVisible;

		/// <summary>
		/// The currently visible rocket labels, to avoid iterating the entire cache to set
		/// inactive.
		/// </summary>
		private readonly ISet<CachedStorageLabel> rocketLabels;

#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private SimpleInfoScreen sis;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// The storages found in the currently selected object.
		/// </summary>
		private readonly List<Storage> storages;

		/// <summary>
		/// A temporary set for determine which labels need to be hidden.
		/// </summary>
		private readonly ISet<CachedStorageLabel> setInactive;

		private bool statusActive;

		/// <summary>
		/// The currently visible storage labels, to avoid iterating the entire cache to set
		/// inactive.
		/// </summary>
		private readonly ISet<CachedStorageLabel> storageLabels;

		private bool storageActive;

		private CollapsibleDetailContentPanel storageParent;

		private bool stressActive;

		private bool vitalsActive;

		internal SimpleInfoScreenWrapper() {
			allGeysers = null;
			conditionParent = null;
			labelCache = new Dictionary<string, CachedStorageLabel>(64);
			lastReport = null;
			lastSelection = default;
			lastStressEntry = null;
			processHeaders = new List<ProcessConditionRow>(8);
			processRows = new List<ProcessConditionRow>(24);
			processVisible = new List<ProcessConditionRow>(32);
			rocketLabels = new HashSet<CachedStorageLabel>();
			setInactive = new HashSet<CachedStorageLabel>();
			statusActive = false;
			storages = new List<Storage>(8);
			storageActive = false;
			storageLabels = new HashSet<CachedStorageLabel>();
			storageParent = null;
			stressActive = false;
			vitalsActive = false;
			instance = this;
		}

		public override void OnCleanUp() {
			int n = processHeaders.Count;
			allGeysers = null;
			foreach (var pair in labelCache)
				pair.Value.Dispose();
			labelCache.Clear();
			// Avoid leaking the report
			lastReport = null;
			lastStressEntry = null;
			lastSelection = default;
			// Destroy all process rows
			for (int i = 0; i < n; i++)
				processHeaders[i].Dispose();
			n = processRows.Count;
			for (int i = 0; i < n; i++)
				processRows[i].Dispose();
			processHeaders.Clear();
			processRows.Clear();
			processVisible.Clear();
			storages.Clear();
			// All of these were in the label cache so they should already be disposed
			rocketLabels.Clear();
			storageLabels.Clear();
			storageParent = null;
			conditionParent = null;
			instance = null;
			base.OnCleanUp();
		}

		/// <summary>
		/// Refreshes the parts of the info screen that are only updated when a different item
		/// is selected.
		/// </summary>
		/// <param name="target">The selected target object.</param>
		private void OnSelectTarget(GameObject target) {
			lastReport = null;
			storages.Clear();
			if (target == null) {
				lastSelection = default;
				vitalsActive = false;
			} else {
				var found = ListPool<Storage, SimpleInfoScreen>.Allocate();
				lastSelection = new LastSelectionDetails(target);
				target.GetComponentsInChildren(found);
				// Add only storages that should be shown
				int n = found.Count;
				for (int i = 0; i < n; i++) {
					var storage = found[i];
					if (storage != null && storage.ShouldShowInUI())
						storages.Add(storage);
				}
				found.Recycle();
				// Geysers can be uncovered over time
				if (lastSelection.world != null)
					allGeysers = FindObjectsOfType<Geyser>();
				else
					allGeysers = null;
			}
		}

		public override void OnSpawn() {
			string atTemperature = DETAILTABS.DETAILS.CONTENTS_TEMPERATURE;
			base.OnSpawn();
			sis.StoragePanel.TryGetComponent(out storageParent);
			if (sis.stressPanel.TryGetComponent(out CollapsibleDetailContentPanel panel))
				panel.HeaderLabel.SetText(DETAILTABS.STATS.GROUPNAME_STRESS);
			if (sis.processConditionContainer.TryGetComponent(out panel))
				conditionParent = panel.Content.gameObject;
			// CHeck for the localization fast path
			if (atTemperature.StartsWith("{0}") && atTemperature.EndsWith("{1}"))
				optimizedStorageTemp = atTemperature.Substring(3, atTemperature.Length - 6);
			else
				optimizedStorageTemp = null;
			Refresh(true);
		}

		/// <summary>
		/// Refreshes the entire info screen. Called every frame, make it fast!
		/// </summary>
		/// <param name="force">Whether the screen should be forcefully updated, even if the
		/// target appears to be the same.</param>
		internal void Refresh(bool force) {
			var target = sis.selectedTarget;
			var statusItems = sis.statusItems;
			// OnSelectTarget gets called before the first Init, so the UI is not ready then
			if (storageParent != null) {
				if (pendingProcessFreeze) {
					// Freeze the condition rows
					int n = processVisible.Count;
					for (int i = 0; i < n; i++)
						processVisible[i].Freeze();
					pendingProcessFreeze = false;
				}
				if (sis.lastTarget != target || force) {
					sis.lastTarget = target;
					if (target != null) {
						SetPanels(target);
						sis.SetStamps(target);
					}
				}
				if (target != null) {
					int count = statusItems.Count;
					bool showStatus = count > 0;
					if (force)
						Update200ms();
					if (showStatus != statusActive) {
						sis.statusItemPanel.gameObject.SetActive(showStatus);
						statusActive = showStatus;
					}
					for (int i = 0; i < count; i++)
						statusItems[i].Refresh();
				}
			}
		}

		/// <summary>
		/// Refreshes the egg chances.
		/// </summary>
		private void RefreshBreedingChance() {
			var smi = lastSelection.fertility;
			var fertilityPanel = sis.fertilityPanel;
			if (smi != null && fertilityPanel != null) {
				var chances = smi.breedingChances;
				var fertModifiers = Db.Get().FertilityModifiers.resources;
				var text = CACHED_BUILDER;
				int total = 0, n = chances.Count, found, nm = fertModifiers.Count;
				for (int i = 0; i < n; i++) {
					var chance = chances[i];
					var eggTag = chance.egg;
					string tooltip, eggName = TagManager.GetProperName(eggTag), weight =
						GameUtil.GetFormattedPercent(chance.weight * 100.0f);
					found = 0;
					text.Clear();
					for (int j = 0; j < nm; j++) {
						var modifier = fertModifiers[j];
						if (modifier.TargetTag == eggTag) {
							text.Append(Constants.BULLETSTRING).AppendLine(modifier.
								GetTooltip());
							found++;
						}
					}
					// You shall not allocate memory!
					if (found > 0) {
						string modifierText = text.ToString();
						text.Clear().Append(DETAILTABS.EGG_CHANCES.CHANCE_FORMAT_TOOLTIP).
							Replace("{0}", eggName).Replace("{1}", weight).Replace("{2}",
							modifierText);
						tooltip = text.ToString();
					} else {
						text.Append(DETAILTABS.EGG_CHANCES.CHANCE_FORMAT_TOOLTIP_NOMOD).
							Replace("{0}", eggName).Replace("{1}", weight);
						tooltip = text.ToString();
					}
					text.Clear().Append(DETAILTABS.EGG_CHANCES.CHANCE_FORMAT).Replace("{0}",
						eggName).Replace("{1}", weight);
					fertilityPanel.SetLabel("breeding_" + (total++).ToString(), text.
						ToString(), tooltip);
				}
				fertilityPanel.Commit();
			}
		}

		/// <summary>
		/// Refreshes the required conditions for processing, mostly for rockets.
		/// </summary>
		private void RefreshProcess() {
			bool rocket = lastSelection.isRocket;
			int nh = 0, nr = 0, n = processVisible.Count;
			// Thaw and turn off all existing rows
			for (int i = 0; i < n; i++)
				processVisible[i].SetActive(false);
			processVisible.Clear();
			if (rocket) {
				if (DlcManager.FeatureClusterSpaceEnabled())
					RefreshProcessConditions(ProcessConditionType.RocketFlight, ref nh,
						ref nr);
				RefreshProcessConditions(ProcessConditionType.RocketPrep, ref nh, ref nr);
				RefreshProcessConditions(ProcessConditionType.RocketStorage, ref nh, ref nr);
				RefreshProcessConditions(ProcessConditionType.RocketBoard, ref nh, ref nr);
			} else
				RefreshProcessConditions(ProcessConditionType.All, ref nh, ref nr);
			pendingProcessFreeze = true;
		}

		/// <summary>
		/// Refreshes one set of required conditions.
		/// </summary>
		/// <param name="conditionType">The condition type to refresh.</param>
		/// <param name="nh">The number of header rows allocated.</param>
		/// <param name="nr">The number of content rows allocated.</param>
		private void RefreshProcessConditions(ProcessConditionType conditionType, ref int nh,
				ref int nr) {
			var conditions = lastSelection.conditions.GetConditionSet(conditionType);
			int n = conditions.Count;
			if (n > 0) {
				int nHeaders = nh, nRows = nr;
				var pr = processRows;
				var ph = processHeaders;
				string conditionName = StringFormatter.ToUpper(conditionType.ToString());
				var seen = HashSetPool<ProcessCondition, SimpleInfoScreen>.Allocate();
				ProcessConditionRow row;
				// Grab a cached header if possible
				if (nHeaders >= ph.Count)
					ph.Add(row = new ProcessConditionRow(Util.KInstantiateUI(sis.
						processConditionHeader.gameObject, conditionParent, true), true));
				else {
					row = ph[nHeaders];
					row.SetActive(true);
				}
				row.SetTitle(Strings.Get("STRINGS.UI.DETAILTABS.PROCESS_CONDITIONS." +
					conditionName), Strings.Get("STRINGS.UI.DETAILTABS.PROCESS_CONDITIONS." +
					conditionName + "_TOOLTIP"));
				processVisible.Add(row);
				nh = nHeaders + 1;
				for (int i = 0; i < n; i++) {
					var condition = conditions[i];
					if (condition.ShowInUI() && (condition is RequireAttachedComponent || seen.
							Add(condition))) {
						if (nRows >= pr.Count) {
							row = new ProcessConditionRow(Util.KInstantiateUI(sis.
								processConditionRow, conditionParent, true), false);
							pr.Add(row);
						} else {
							row = pr[nRows];
							row.SetActive(true);
						}
						processVisible.Add(row);
						row.SetCondition(condition);
						nRows++;
					}
				}
				nr = nRows;
				seen.Recycle();
			}
		}

		/// <summary>
		/// Refreshes the Stress readout of the info screen.
		/// </summary>
		private void RefreshStress() {
			var stressDrawer = sis.stressDrawer;
			var ri = ReportManager.Instance;
			var allNoteEntries = ri.noteStorage.noteEntries;
			var report = ri.TodaysReport;
			var stressEntry = lastStressEntry;
			var stressPanel = sis.stressPanel;
			if (lastSelection.identity != null) {
				// If new report, look up entry again
				if (report != lastReport || stressEntry == null) {
					lastReport = report;
					lastStressEntry = stressEntry = report.GetEntry(ReportManager.ReportType.
						StressDelta);
				}
				string name = lastSelection.selectable.GetProperName();
				var stressEntries = stressEntry.contextEntries;
				int n = stressEntries.Count;
				stressDrawer.BeginDrawing();
				// Look for this Duplicant in the report
				for (int i = 0; i < n; i++) {
					var reportEntry = stressEntries[i];
					int nodeID = reportEntry.noteStorageId;
					// The IterateNotes callback allocates a delegate on the heap :/
					if (reportEntry.context == name && allNoteEntries.entries.TryGetValue(
							nodeID, out Dictionary<NoteEntryKey, float> nodeEntries)) {
						var text = CACHED_BUILDER;
						float total = CompileNotes(nodeEntries, stressDrawer);
						// Ryu to the rescue again!
						text.Clear();
						total.ToRyuHardString(text, 2);
						string totalText = text.ToString();
						text.Clear();
						if (total > 0.0f)
							text.Append(UIConstants.ColorPrefixRed);
						text.Append(DETAILTABS.DETAILS.NET_STRESS).Replace("{0}", totalText);
						if (total > 0.0f)
							text.Append(UIConstants.ColorSuffix);
						stressDrawer.NewLabel(text.ToString());
						break;
					}
				}
				stressDrawer.EndDrawing();
				if (!stressActive) {
					sis.stressPanel.SetActive(true);
					stressActive = true;
				}
			} else if (stressActive) {
				stressPanel.SetActive(false);
				stressActive = false;
			}
		}

		/// <summary>
		/// Shows or hides panels depending on the active object.
		/// </summary>
		/// <param name="target">The selected target object.</param>
		private void SetPanels(GameObject target) {
			var modifiers = lastSelection.modifiers;
			var descriptionContainer = sis.descriptionContainer;
			var id = lastSelection.identity;
			var attributeLabels = sis.attributeLabels;
			int n = attributeLabels.Count;
			string descText = "", flavorText = "";
			var effects = DescriptorAllocPatches.GetGameObjectEffects(target, true);
			Klei.AI.Amounts amounts;
			bool hasAmounts = modifiers != null && (amounts = modifiers.amounts) != null &&
				amounts.Count > 0, hasProcess = lastSelection.conditions != null,
				hasEffects = effects.Count > 0;
			for (int i = 0; i < n; i++)
				Destroy(attributeLabels[i]);
			attributeLabels.Clear();
			if (hasAmounts) {
				sis.vitalsContainer.selectedEntity = target;
				if (target.TryGetComponent(out Uprootable plant) && !target.TryGetComponent(
						out WiltCondition _))
					hasAmounts = plant.GetPlanterStorage != null;
			}
			vitalsActive = hasAmounts;
			sis.vitalsPanel.gameObject.SetActive(hasAmounts);
			sis.processConditionContainer.SetActive(hasProcess);
			if (hasProcess)
				sis.RefreshProcessConditions();
			if (id != null)
				descText = "";
			else if (target.TryGetComponent(out InfoDescription description))
				descText = description.description;
			else if (target.TryGetComponent(out Building building)) {
				descText = building.Def.Effect;
				flavorText = building.Def.Desc;
			} else if (target.TryGetComponent(out Edible edible))
				descText = STRINGS.UI.GAMEOBJECTEFFECTS.CALORIES.Format(GameUtil.
					GetFormattedCalories(edible.FoodInfo.CaloriesPerUnit));
			else if (target.TryGetComponent(out CellSelectionObject cso))
				descText = cso.element.FullDescription(false);
			else if (target.TryGetComponent(out PrimaryElement pe)) {
				var element = ElementLoader.FindElementByHash(pe.ElementID);
				descText = (element != null) ? element.FullDescription(false) : "";
			}
			bool showInfo = id == null && (!descText.IsNullOrWhiteSpace() || !flavorText.
				IsNullOrWhiteSpace() || hasEffects);
			descriptionContainer.descriptors.gameObject.SetActive(hasEffects);
			if (hasEffects)
				descriptionContainer.descriptors.SetDescriptors(effects);
			descriptionContainer.gameObject.SetActive(showInfo);
			if (descText != null)
				descriptionContainer.description.SetText(descText);
			if (flavorText != null)
				descriptionContainer.flavour.SetText(flavorText);
			sis.infoPanel.gameObject.SetActive(showInfo);
			descriptionContainer.flavour.gameObject.SetActive(!string.IsNullOrWhiteSpace(
				flavorText));
			storageParent.HeaderLabel.SetText((id != null) ? DETAILTABS.DETAILS.
				GROUPNAME_MINION_CONTENTS : DETAILTABS.DETAILS.GROUPNAME_CONTENTS);
			if (lastSelection.fertility == null)
				sis.fertilityPanel.gameObject.SetActive(false);
			sis.rocketStatusContainer.gameObject.SetActive(lastSelection.
				rocketInterface != null || lastSelection.rocketModule != null);
		}

		public void Sim200ms(float _) {
			if (sis.lastTarget != null && storageParent != null && isActiveAndEnabled) {
				Update200ms();
			}
		}

		/// <summary>
		/// Updates the panels that should be updated every 200ms or when the selected object
		/// changes.
		/// </summary>
		private void Update200ms() {
			var vitalsContainer = sis.vitalsContainer;
			RefreshStress();
			if (vitalsActive) {
				var vi = VitalsPanelWrapper.Instance;
				if (vi == null)
					vitalsContainer.Refresh();
				else
					vi.Update(vitalsContainer);
			}
			RefreshRocket();
			RefreshStorage();
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// 
		/// Do I really need to repeat my spiel about big structs again?
		/// </summary>
		private struct LastSelectionDetails {
			internal readonly IProcessConditionSet conditions;

			internal readonly FertilityMonitor.Instance fertility;

			internal readonly ClusterGridEntity gridEntity;

			internal readonly MinionIdentity identity;

			internal readonly bool isAsteroid;

			internal readonly bool isRocket;

			internal readonly Klei.AI.Modifiers modifiers;

			internal readonly CraftModuleInterface rocketInterface;

			internal readonly RocketModuleCluster rocketModule;

			internal readonly KSelectable selectable;

			internal readonly WorldContainer world;

			internal LastSelectionDetails(GameObject target) {
				target.TryGetComponent(out conditions);
				target.TryGetComponent(out identity);
				target.TryGetComponent(out modifiers);
				target.TryGetComponent(out rocketModule);
				target.TryGetComponent(out selectable);
				target.TryGetComponent(out world);
				if (rocketModule != null) {
					rocketInterface = rocketModule.CraftInterface;
					// Clustercraft can be pulled from the rocket-to-module interface
					gridEntity = rocketInterface.m_clustercraft;
				} else if (target.TryGetComponent(out gridEntity) && gridEntity is
						Clustercraft craft)
					rocketInterface = craft.ModuleInterface;
				else
					rocketInterface = null;
				fertility = target.GetSMI<FertilityMonitor.Instance>();
				if (DlcManager.FeatureClusterSpaceEnabled())
					isRocket = target.TryGetComponent(out LaunchPad _) || target.
						TryGetComponent(out RocketProcessConditionDisplayTarget _);
				else
					isRocket = target.TryGetComponent(out LaunchableRocket _);
				isAsteroid = gridEntity != null && gridEntity is AsteroidGridEntity;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to add our component to its game object.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnPrefabInit))]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(SimpleInfoScreen __instance) {
				if (__instance != null)
					__instance.gameObject.AddOrGet<SimpleInfoScreenWrapper>();
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to update the selected target.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnSelectTarget))]
		internal static class OnSelectTarget_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before OnSelectTarget runs.
			/// </summary>
			internal static bool Prefix(SimpleInfoScreen __instance, GameObject target) {
				if (__instance.lastTarget != target)
					instance?.OnSelectTarget(target);
				return true;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to speed up refreshing it.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix(bool force) {
				instance?.Refresh(force);
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the egg chances when they change.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.
			RefreshBreedingChance))]
		internal static class RefreshBreedingChance_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshBreedingChance runs.
			/// </summary>
			internal static bool Prefix() {
				instance?.RefreshBreedingChance();
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the checklist of conditions for operation.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.
			RefreshProcessConditions))]
		internal static class RefreshProcessConditions_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshProcessConditions runs.
			/// </summary>
			internal static bool Prefix() {
				instance?.RefreshProcess();
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the storage when storage changes.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.RefreshStorage))]
		internal static class RefreshStorage_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshStorage runs.
			/// </summary>
			internal static bool Prefix(SimpleInfoScreen __instance) {
				var inst = instance;
				if (inst != null && __instance.selectedTarget != null)
					inst.RefreshStorage();
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the cluster map info when the refresh is
		/// triggered.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.RefreshWorld))]
		internal static class RefreshWorld_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshWorld runs.
			/// </summary>
			internal static bool Prefix(SimpleInfoScreen __instance) {
				instance?.RefreshWorld();
				return false;
			}
		}
	}
}
