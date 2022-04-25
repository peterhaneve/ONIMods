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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using DETAILTABS = STRINGS.UI.DETAILTABS;
using NoteEntryKey = ReportManager.NoteStorage.NoteEntries.NoteEntryKey;
using ProcessConditionType = ProcessCondition.ProcessConditionType;
using ReportEntry = ReportManager.ReportEntry;
using StorageTooltip = Tuple<string, TextStyleSetting>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the simple info screen to avoid recalculating so much
	/// every frame.
	/// </summary>
	internal sealed class SimpleInfoScreenWrapper : IDisposable {
		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The time in seconds between status panel updates of the less-important items.
		/// </summary>
		private const double UPDATE_RATE = 0.2;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static SimpleInfoScreenWrapper instance;

		internal static void Cleanup() {
			instance?.Dispose();
			instance = null;
		}

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

		internal static void Init() {
			Cleanup();
			instance = new SimpleInfoScreenWrapper();
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
		/// Update several parts of the panel only every 0.2 s instead of every frame.
		/// </summary>
		private double lastUpdate;

		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		/// <summary>
		/// The storages found in the currently selected object.
		/// </summary>
		private readonly List<Storage> storages;

		/// <summary>
		/// The currently visible storage labels, to avoid iterating the entire cache to set
		/// inactive.
		/// </summary>
		private readonly ISet<CachedStorageLabel> storageLabels;

		private bool storageActive;

		private CollapsibleDetailContentPanel storageParent;

		private bool stressActive;

		private bool vitalsActive;

		private bool wasPaused;

		/// <summary>
		/// A temporary list used to compile tooltips for stored items.
		/// </summary>
		private readonly IList<StorageTooltip> tooltips;

		internal SimpleInfoScreenWrapper() {
			allGeysers = null;
			conditionParent = null;
			labelCache = new Dictionary<string, CachedStorageLabel>(64);
			lastReport = null;
			lastUpdate = 0.0;
			lastSelection = default;
			lastStressEntry = null;
			storages = new List<Storage>(8);
			storageActive = false;
			storageLabels = new HashSet<CachedStorageLabel>();
			storageParent = null;
			stressActive = false;
			// Basically a persistent list pool
			tooltips = new List<StorageTooltip>(32);
			vitalsActive = false;
			wasPaused = false;
		}

		/// <summary>
		/// Displays all geysers present on the given planetoid.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		/// <param name="id">The world ID to filter.</param>
		private void AddGeysers(SimpleInfoScreen sis, int id) {
			var geyserRows = sis.geyserRows;
			var parent = sis.worldGeysersPanel.Content.gameObject;
			var spawnables = SaveGame.Instance.worldGenSpawner.spawnables;
			byte worldIndex;
			var knownGeysers = ListPool<Tag, SimpleInfoScreen>.Allocate();
			// Add all spawned geysers
			int n = allGeysers.Length, unknownGeysers = 0;
			for (int i = 0; i < n; i++) {
				var geyser = allGeysers[i];
				var go = geyser.gameObject;
				if (go.GetMyWorldId() == id)
					knownGeysers.Add(go.PrefabID());
			}
			// All unknown geysers and oil wells
			n = spawnables.Count;
			for (int i = 0; i < n; i++) {
				var candidate = spawnables[i];
				int cell = candidate.cell;
				if (Grid.IsValidCell(cell) && !candidate.isSpawned && (worldIndex = Grid.
						WorldIdx[cell]) != ClusterManager.INVALID_WORLD_IDX &&
						worldIndex == id) {
					string prefabID = candidate.spawnInfo.id;
					Tag prefabTag = new Tag(prefabID);
					if (prefabID == OilWellConfig.ID)
						knownGeysers.Add(prefabTag);
					else if (prefabID == GeyserGenericConfig.ID)
						unknownGeysers++;
					else {
						var prefab = Assets.GetPrefab(prefabTag);
						if (prefab != null && prefab.TryGetComponent(out Geyser _))
							knownGeysers.Add(prefabTag);
					}
				}
			}
			int totalGeysers = knownGeysers.Count;
			foreach (var pair in geyserRows)
				pair.Value.SetActive(false);
			for (int i = 0; i < totalGeysers; i++)
				SimpleInfoScreenStarmap.AddGeyser(sis, knownGeysers[i], parent, null,
					geyserRows);
			if (unknownGeysers > 0)
				SimpleInfoScreenStarmap.AddGeyser(sis, new Tag(GeyserGenericConfig.ID), parent,
					DETAILTABS.SIMPLEINFO.UNKNOWN_GEYSERS.Replace("{num}", unknownGeysers.
					ToString()), geyserRows);
			// No Geysers
			if (totalGeysers == 0)
				SimpleInfoScreenStarmap.AddGeyser(sis, new Tag(SimpleInfoScreenStarmap.
					NO_GEYSERS), parent, DETAILTABS.SIMPLEINFO.NO_GEYSERS, geyserRows);
			knownGeysers.Recycle();
		}

		/// <summary>
		/// Displays an item in storage.
		/// </summary>
		/// <param name="sis">The info screen to use.</param>
		/// <param name="item">The item to be displayed.</param>
		/// <param name="storage">The parent storage of this item.</param>
		/// <param name="parent">The parent object for the displayed item.</param>
		/// <param name="total">The total number of items displayed so far.</param>
		private void AddStorageItem(SimpleInfoScreen sis, GameObject item, Storage storage,
				GameObject parent, ref int total) {
			bool added = !item.TryGetComponent(out PrimaryElement pe) || pe.Mass > 0.0f;
			if (added) {
				int t = total, n;
				var storeLabel = GetStorageLabel(sis, parent, "storage_" + t.ToString());
				var tooltip = storeLabel.tooltip;
				storageLabels.Add(storeLabel);
				storeLabel.text.text = GetItemDescription(item, pe);
				n = tooltips.Count;
				tooltip.ClearMultiStringTooltip();
				for (int i = 0; i < n; i++) {
					var itemTooltips = tooltips[i];
					tooltip.AddMultiStringTooltip(itemTooltips.first, itemTooltips.second);
				}
				storeLabel.SetAllowDrop(storage.allowUIItemRemoval, storage, item);
				tooltips.Clear();
				total = t + 1;
			}
		}

		public void Dispose() {
			allGeysers = null;
			foreach (var pair in labelCache)
				pair.Value.Dispose();
			labelCache.Clear();
			// Avoid leaking the report
			lastReport = null;
			lastStressEntry = null;
			lastSelection = default;
			storages.Clear();
			// All of these were in the label cache so they should already be disposed
			storageLabels.Clear();
			storageParent = null;
			conditionParent = null;
		}

		/// <summary>
		/// Gets the text to be displayed for a single stored item.
		/// </summary>
		/// <param name="item">The item to be displayed.</param>
		/// <param name="pe">The item's primary element, or null if it has none.</param>
		private string GetItemDescription(GameObject item, PrimaryElement pe) {
			var defaultStyle = PluginAssets.Instance.defaultTextStyleSetting;
			var text = CACHED_BUILDER;
			var rottable = item.GetSMI<Rottable.Instance>();
			text.Clear();
			if (item.TryGetComponent(out HighEnergyParticleStorage hepStorage))
				// Radbolts
				text.Append(DETAILTABS.DETAILS.CONTENTS_MASS).Replace("{0}", STRINGS.ITEMS.
					RADIATION.HIGHENERGYPARITCLE.NAME).Replace("{1}", GameUtil.
					GetFormattedHighEnergyParticles(hepStorage.Particles));
			else if (pe != null)
				// Element
				text.Append(DETAILTABS.DETAILS.CONTENTS_TEMPERATURE).Replace("{1}",
					GameUtil.GetFormattedTemperature(pe.Temperature)).Replace("{0}",
					DETAILTABS.DETAILS.CONTENTS_MASS).Replace("{0}", GameUtil.
					GetUnitFormattedName(item)).Replace("{1}", GameUtil.GetFormattedMass(
					pe.Mass));
			if (rottable != null) {
				string rotText = rottable.StateString();
				if (!string.IsNullOrEmpty(rotText))
					text.Append(DETAILTABS.DETAILS.CONTENTS_ROTTABLE).Replace("{0}",
						rotText);
				tooltips.Add(new StorageTooltip(rottable.GetToolTip(), defaultStyle));
			}
			if (pe.DiseaseIdx != Klei.SimUtil.DiseaseInfo.Invalid.idx) {
				text.Append(DETAILTABS.DETAILS.CONTENTS_DISEASED).Replace("{0}",
					GameUtil.GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount, false));
				tooltips.Add(new StorageTooltip(GameUtil.GetFormattedDisease(pe.DiseaseIdx,
					pe.DiseaseCount, true), defaultStyle));
			}
			return text.ToString();
		}

		/// <summary>
		/// Retrieves a pooled label used for displaying stored objects.
		/// </summary>
		/// <param name="sis">The info screen to use.</param>
		/// <param name="parent">The parent panel to add new labels.</param>
		/// <param name="id">The name of the label to be added or created.</param>
		/// <returns>A label which can be used to display stored items, pooled if possible.</returns>
		private CachedStorageLabel GetStorageLabel(SimpleInfoScreen sis, GameObject parent,
				string id) {
			if (labelCache.TryGetValue(id, out CachedStorageLabel result))
				result.Reset();
			else {
				result = new CachedStorageLabel(sis, parent, id);
				labelCache[id] = result;
			}
			result.SetActive(true);
			return result;
		}

		/// <summary>
		/// Initializes references that only change when the selected target is different.
		/// </summary>
		/// <param name="sis">The parent info screen.</param>
		/// <param name="target">The selected target object.</param>
		private void InitInstance(SimpleInfoScreen sis, GameObject target) {
			CollapsibleDetailContentPanel panel;
			if (storageParent == null && sis.StoragePanel.TryGetComponent(out storageParent)) {
				if (sis.stressPanel.TryGetComponent(out panel))
					panel.HeaderLabel.text = DETAILTABS.STATS.GROUPNAME_STRESS;
				allGeysers = UnityEngine.Object.FindObjectsOfType<Geyser>();
			}
			if (conditionParent == null && sis.processConditionContainer.TryGetComponent(
					out panel))
				conditionParent = panel.Content.gameObject;
			storageParent.HeaderLabel.text = (lastSelection.identity != null) ? DETAILTABS.
				DETAILS.GROUPNAME_MINION_CONTENTS : DETAILTABS.DETAILS.GROUPNAME_CONTENTS;
			if (lastSelection.fertility == null)
				sis.fertilityPanel.gameObject.SetActive(false);
			SetPanels(sis, target);
			sis.SetStamps(target);
		}

		/// <summary>
		/// Refreshes the parts of the info screen that are only updated when a different item
		/// is selected.
		/// </summary>
		/// <param name="sis">The info screen to refresh.</param>
		/// <param name="target">The selected target object.</param>
		private void OnSelectTarget(SimpleInfoScreen sis, GameObject target) {
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
			}
		}

		/// <summary>
		/// Refreshes the entire info screen. Called every frame, make it fast!
		/// </summary>
		/// <param name="sis">The info screen to refresh.</param>
		/// <param name="force">Whether the screen should be forcefully updated, even if the
		/// target appears to be the same.</param>
		internal void Refresh(SimpleInfoScreen sis, bool force) {
			var target = sis.selectedTarget;
			var statusItems = sis.statusItems;
			double now = Time.timeAsDouble;
			bool paused = SpeedControlScreen.Instance.IsPaused;
			// OnSelectTarget gets called before the first Init, so the UI is not ready then
			if (sis.lastTarget != target || force) {
				sis.lastTarget = target;
				if (target != null)
					InitInstance(sis, target);
			}
			if (target != null) {
				if (now - lastUpdate > UPDATE_RATE || force) {
					var vitalsContainer = sis.vitalsContainer;
					lastUpdate = now;
					RefreshStress(sis);
					if (vitalsActive) {
						var vi = VitalsPanelWrapper.Instance;
						if (vi == null)
							vitalsContainer.Refresh();
						else
							vi.Update(vitalsContainer);
					}
				}
				int count = statusItems.Count;
				sis.statusItemPanel.gameObject.SetActive(count > 0);
				for (int i = 0; i < count; i++)
					statusItems[i].Refresh();
				if (force || !paused || !wasPaused)
					RefreshStorage(sis);
				sis.rocketSimpleInfoPanel.Refresh(sis.rocketStatusContainer, target);
			}
			wasPaused = paused;
		}

		/// <summary>
		/// Refreshes the egg chances.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		private void RefreshBreedingChance(SimpleInfoScreen sis) {
			var smi = lastSelection.fertility;
			var fertilityPanel = sis.fertilityPanel;
			if (smi != null) {
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
						var modifier = fertModifiers[i];
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
		/// <param name="sis">The info screen to update.</param>
		private void RefreshProcess(SimpleInfoScreen sis) {
			var rows = sis.processConditionRows;
			bool rocket = lastSelection.isRocket;
			// As it is a mix of headers and body, reusing rows is not as easy as it looks
			foreach (var original in rows)
				Util.KDestroyGameObject(original);
			rows.Clear();
			if (rocket) {
				if (DlcManager.FeatureClusterSpaceEnabled())
					RefreshProcessConditionsForType(sis, ProcessConditionType.RocketFlight);
				RefreshProcessConditionsForType(sis, ProcessConditionType.RocketPrep);
				RefreshProcessConditionsForType(sis, ProcessConditionType.RocketStorage);
				RefreshProcessConditionsForType(sis, ProcessConditionType.RocketBoard);
			} else
				RefreshProcessConditionsForType(sis, ProcessConditionType.All);
		}

		/// <summary>
		/// Refreshes one set of required conditions.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		/// <param name="conditionType">The condition type to refresh.</param>
		private void RefreshProcessConditionsForType(SimpleInfoScreen sis,
				ProcessConditionType conditionType) {
			var conditions = lastSelection.conditions.GetConditionSet(conditionType);
			int n = conditions.Count;
			if (n > 0) {
				string conditionName = StringFormatter.ToUpper(conditionType.ToString());
				var seen = HashSetPool<ProcessCondition, SimpleInfoScreen>.Allocate();
				var hr = Util.KInstantiateUI<HierarchyReferences>(sis.processConditionHeader.
					gameObject, conditionParent, true);
				hr.GetReference<LocText>("Label").text = Strings.Get(
					"STRINGS.UI.DETAILTABS.PROCESS_CONDITIONS." + conditionName);
				hr.GetComponent<ToolTip>().toolTip = Strings.Get(
					"STRINGS.UI.DETAILTABS.PROCESS_CONDITIONS." + conditionName + "_TOOLTIP");
				sis.processConditionRows.Add(hr.gameObject);
				for (int i = 0; i < n; i++) {
					var condition = conditions[i];
					if (condition.ShowInUI() && (condition is RequireAttachedComponent || seen.
							Add(condition))) {
						var go = Util.KInstantiateUI(sis.processConditionRow, conditionParent,
							true);
						sis.processConditionRows.Add(go);
						ConditionListSideScreen.SetRowState(go, condition);
					}
				}
				seen.Recycle();
			}
		}

		/// <summary>
		/// Refreshes the storage objects on this object (and its children?)
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		private void RefreshStorage(SimpleInfoScreen sis) {
			var panel = sis.StoragePanel;
			int n = storages.Count, total = 0, nitems;
			if (n > 0) {
				var setInactive = HashSetPool<CachedStorageLabel, SimpleInfoScreen>.Allocate();
				var parent = storageParent.Content.gameObject;
				setInactive.UnionWith(storageLabels);
				storageLabels.Clear();
				for (int i = 0; i < n; i++) {
					var storage = storages[i];
					// Storage could have been destroyed along the way
					if (storage != null) {
						var items = storage.GetItems();
						nitems = items.Count;
						for (int j = 0; j < nitems; j++) {
							var item = items[j];
							if (item != null)
								AddStorageItem(sis, item, storage, parent, ref total);
						}
					}
				}
				if (total == 0)
					GetStorageLabel(sis, parent, CachedStorageLabel.EMPTY_ITEM);
				// Only turn off the things that are gone
				setInactive.ExceptWith(storageLabels);
				foreach (var inactive in setInactive)
					inactive.SetActive(false);
				setInactive.Recycle();
				if (!storageActive) {
					panel.gameObject.SetActive(true);
					storageActive = true;
				}
			} else if (storageActive) {
				panel.gameObject.SetActive(false);
				storageActive = false;
			}
		}

		/// <summary>
		/// Refreshes the Stress readout of the info screen.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		private void RefreshStress(SimpleInfoScreen sis) {
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
		/// Refreshes the asteroid description side screen.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		private void RefreshWorld(SimpleInfoScreen sis) {
			var world = lastSelection.world;
			var gridEntity = lastSelection.asteroid;
			bool isPlanetoid = world != null && gridEntity != null;
			sis.worldTraitsPanel.gameObject.SetActive(isPlanetoid);
			sis.worldGeysersPanel.gameObject.SetActive(isPlanetoid);
			if (isPlanetoid) {
				var biomes = world.Biomes;
				var biomeRows = sis.biomeRows;
				var traitIDs = world.WorldTraitIds;
				if (biomes == null)
					foreach (var pair in biomeRows)
						pair.Value.SetActive(false);
				else
					SimpleInfoScreenStarmap.AddBiomes(sis, biomes, biomeRows);
				sis.worldBiomesPanel.gameObject.SetActive(biomes != null);
				AddGeysers(sis, world.id);
				if (traitIDs != null)
					SimpleInfoScreenStarmap.AddWorldTraits(sis, traitIDs);
				SimpleInfoScreenStarmap.AddSurfaceConditions(sis, world);
			} else
				sis.worldBiomesPanel.gameObject.SetActive(false);
		}

		/// <summary>
		/// Shows or hides panels depending on the active object.
		/// </summary>
		/// <param name="sis">The info screen to update.</param>
		/// <param name="target">The selected target object.</param>
		private void SetPanels(SimpleInfoScreen sis, GameObject target) {
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
				UnityEngine.Object.Destroy(attributeLabels[i]);
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
			descriptionContainer.description.text = descText;
			descriptionContainer.flavour.text = flavorText;
			sis.infoPanel.gameObject.SetActive(showInfo);
			descriptionContainer.gameObject.SetActive(showInfo);
			descriptionContainer.flavour.gameObject.SetActive(!string.IsNullOrWhiteSpace(
				flavorText));
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// 
		/// Do I really need to repeat my spiel about big structs again?
		/// </summary>
		private struct LastSelectionDetails {
			internal readonly AsteroidGridEntity asteroid;

			internal readonly IProcessConditionSet conditions;

			internal readonly FertilityMonitor.Instance fertility;

			internal readonly MinionIdentity identity;

			internal readonly bool isRocket;

			internal readonly Klei.AI.Modifiers modifiers;

			internal readonly KSelectable selectable;

			internal readonly WorldContainer world;

			internal LastSelectionDetails(GameObject target) {
				target.TryGetComponent(out asteroid);
				target.TryGetComponent(out conditions);
				target.TryGetComponent(out identity);
				target.TryGetComponent(out modifiers);
				target.TryGetComponent(out selectable);
				target.TryGetComponent(out world);
				fertility = target.GetSMI<FertilityMonitor.Instance>();
				if (DlcManager.FeatureClusterSpaceEnabled())
					isRocket = target.TryGetComponent(out LaunchPad _) || target.
						TryGetComponent(out RocketProcessConditionDisplayTarget _);
				else
					isRocket = target.TryGetComponent(out LaunchableRocket _);
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
					instance?.OnSelectTarget(__instance, target);
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
			internal static bool Prefix(SimpleInfoScreen __instance, bool force) {
				instance?.Refresh(__instance, force);
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
			internal static bool Prefix(SimpleInfoScreen __instance) {
				instance?.RefreshBreedingChance(__instance);
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
			internal static bool Prefix(SimpleInfoScreen __instance) {
				instance?.RefreshProcess(__instance);
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
				instance?.RefreshStorage(__instance);
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
				instance?.RefreshWorld(__instance);
				return false;
			}
		}
	}
}
