/*
 * Copyright 2026 Peter Han
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

using System.Collections.Generic;
using System.Text;
using Klei.AI;
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
		/// If set to true, runs the base game's rocket refresh method anyways even though it
		/// is very slow, for mod compatibility.
		/// </summary>
		internal static bool AllowBaseRocketPanel = false;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static SimpleInfoScreenWrapper instance;

		/// <summary>
		/// Collects the stress change reasons and displays them in the UI.
		/// </summary>
		/// <param name="stressEntries">The stress change entries in the report entry.</param>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <returns>The total stress change.</returns>
		private static float CompileNotes(Dictionary<NoteEntryKey, float> stressEntries,
				CollapsibleDetailContentPanel panel) {
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
			stressNotes.Sort(StressNoteComparer.INSTANCE);
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
				panel.SetLabel("stressNotes_" + i, text.ToString(), "");
				total += stressDelta;
			}
			stressNotes.Recycle();
			return total;
		}
		
		/// <summary>
		/// Generates the required tooltip text for Move To errands. Populates the cached
		/// string builder with the output text.
		/// </summary>
		/// <param name="target">The item that is being moved.</param>
		/// <param name="pe">The primary element of the target item.</param>
		/// <returns>The tooltip to display.</returns>
		private static string DescribeMovable(GameObject target, PrimaryElement pe) {
			var smi = target.GetSMI<Rottable.Instance>();
			var text = CACHED_BUILDER;
			bool hasHEP = target.TryGetComponent(out HighEnergyParticleStorage hep);
			string tooltip = "", itemName = "";
			if (target.TryGetComponent(out KSelectable selectable))
				itemName = selectable.GetName();
			text.Clear();
			if (pe != null && !hasHEP) {
				if (target.TryGetComponent(out KPrefabID kpid) && Assets.IsTagCountable(
						kpid.PrefabTag))
					itemName = GameUtil.GetUnitFormattedName(itemName, pe.Units);
				text.AppendFormat(DETAILTABS.DETAILS.CONTENTS_MASS, itemName, "");
				FormatStringPatches.GetFormattedMass(text, pe.Mass);
				string what = text.ToString();
				text.Clear().AppendFormat(DETAILTABS.DETAILS.CONTENTS_TEMPERATURE, what,
					"");
				FormatStringPatches.GetFormattedTemperature(text, pe.Temperature);
			} else if (hasHEP) {
				itemName = STRINGS.ITEMS.RADIATION.HIGHENERGYPARITCLE.NAME;
				text.AppendFormat(DETAILTABS.DETAILS.CONTENTS_MASS, itemName,
					GameUtil.GetFormattedHighEnergyParticles(hep.Particles));
			}
			if (smi != null) {
				string str = smi.StateString();
				if (!string.IsNullOrEmpty(str))
					text.Append(DETAILTABS.DETAILS.CONTENTS_ROTTABLE.Format(str));
				tooltip = smi.GetToolTip();
			}
			if (!FastTrackOptions.Instance.NoDisease && pe.DiseaseIdx != Sim.InvalidDiseaseIdx)
			{
				text.Append(DETAILTABS.DETAILS.CONTENTS_DISEASED.Format(GameUtil.
					GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount)));
				tooltip += GameUtil.GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount, true);
			}
			return tooltip;
		}

		/// <summary>
		/// Adds text for the room constraints used on a building.
		/// </summary>
		/// <param name="target">The building to check.</param>
		/// <param name="infoPanel">The location where the constraints will be populated.</param>
		/// <returns>The number of matching constraints found.</returns>
		private static int GetRoomConstraints(GameObject target,
				CollapsibleDetailContentPanel infoPanel) {
			int constraints = 0;
			if (target.TryGetComponent(out KPrefabID kp)) {
				var constraintTags = RoomConstraints.ConstraintTags.AllTags;
				var sb = CACHED_BUILDER;
				sb.Clear().Append('\n').Append(STRINGS.CODEX.HEADERS.BUILDINGTYPE).Append(':');
				foreach (var tag in kp.Tags)
					if (constraintTags.Contains(tag)) {
						sb.AppendLine().Append(Constants.TABBULLETSTRING).Append(
							RoomConstraints.ConstraintTags.GetRoomConstraintLabelText(tag));
						constraints++;
					}
				if (constraints > 0)
					infoPanel.SetLabel("RoomClass", sb.ToString(), "");
			}
			return constraints;
		}

		/// <summary>
		/// Lists all geysers in the world.
		/// </summary>
		private Geyser[] allGeysers;

		private GameObject conditionParent;

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
		/// The cached meteor shower list. Only valid if a planetoid is selected.
		/// </summary>
		private readonly IDictionary<string, MeteorShowerEvent> meteors;

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
		private readonly List<IStorage> storages;

		private bool statusActive;

		private bool vitalsActive;

		internal SimpleInfoScreenWrapper() {
			allGeysers = null;
			conditionParent = null;
			lastReport = null;
			lastSelection = default;
			lastStressEntry = null;
			meteors = new Dictionary<string, MeteorShowerEvent>(8);
			processHeaders = new List<ProcessConditionRow>(8);
			processRows = new List<ProcessConditionRow>(24);
			processVisible = new List<ProcessConditionRow>(32);
			statusActive = false;
			storages = new List<IStorage>(8);
			vitalsActive = false;
			instance = this;
		}
		
		public override void OnCleanUp() {
			int n = processHeaders.Count;
			allGeysers = null;
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
				var found = ListPool<IStorage, SimpleInfoScreen>.Allocate();
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
				allGeysers = lastSelection.world != null ? FindObjectsOfType<Geyser>() : null;
				// If planetoid, check meteor showers
				if (lastSelection.isAsteroid)
					UpdateMeteors();
			}
			// Vanilla method force shows it
			statusActive = true;
		}

		public override void OnSpawn() {
			string atTemperature = DETAILTABS.DETAILS.CONTENTS_TEMPERATURE;
			base.OnSpawn();
			sis.stressPanel.SetTitle(DETAILTABS.STATS.GROUPNAME_STRESS);
			if (sis.processConditionContainer != null)
				conditionParent = sis.processConditionContainer.Content.gameObject;
			// Check for the localization fast path
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
			if (conditionParent != null) {
				if (pendingProcessFreeze) {
					// Freeze the condition rows
					int n = processVisible.Count;
					for (int i = 0; i < n; i++)
						processVisible[i].Freeze();
					pendingProcessFreeze = false;
				}
				if (sis.lastTarget != target || force) {
					sis.lastTarget = target;
					if (target != null)
						SetPanels(target);
				}
				if (target != null) {
					int count = statusItems.Count;
					bool showStatus = count > 0;
					if (force)
						UpdatePanels();
					if (showStatus != statusActive || force) {
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
		private void RefreshFertility() {
			var smi = lastSelection.fertility;
			var fertilityPanel = sis.fertilityPanel;
			if (smi != null && fertilityPanel != null) {
				var chances = smi.breedingChances;
				var fertModifiers = Db.Get().FertilityModifiers.resources;
				var text = CACHED_BUILDER;
				int total = 0, n = chances.Count, nm = fertModifiers.Count;
				for (int i = 0; i < n; i++) {
					var chance = chances[i];
					var eggTag = chance.egg;
					string eggName = TagManager.GetProperName(eggTag), weight =
						GameUtil.GetFormattedPercent(chance.weight * 100.0f);
					int found = 0;
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
					} else
						text.Append(DETAILTABS.EGG_CHANCES.CHANCE_FORMAT_TOOLTIP_NOMOD).
							Replace("{0}", eggName).Replace("{1}", weight);
					string tooltip = text.ToString();
					text.Clear().Append(DETAILTABS.EGG_CHANCES.CHANCE_FORMAT).Replace("{0}",
						eggName).Replace("{1}", weight);
					fertilityPanel.SetLabel("breeding_" + (total++), text.ToString(), tooltip);
				}
				fertilityPanel.Commit();
			}
		}

		/// <summary>
		/// Refreshes the Info panel.
		/// </summary>
		private void RefreshInfo() {
			var targetEntity = sis.selectedTarget;
			var infoPanel = sis.infoPanel;
			var text = CACHED_BUILDER;
			string desc = "", effect = "";
			if (lastSelection.identity == null) {
				var id = lastSelection.description;
				var buildingComplete = lastSelection.complete;
				var buildingConstruction = lastSelection.underConstruction;
				var pe = lastSelection.primaryElement;
				var edible = lastSelection.edible;
				var cso = lastSelection.cso;
				if (id != null) {
					desc = id.description;
					effect = id.effect;
				} else if (buildingComplete != null) {
					desc = buildingComplete.DescFlavour;
					effect = buildingComplete.Desc;
				} else if (buildingConstruction != null) {
					desc = buildingConstruction.Def.Effect;
					effect = buildingConstruction.Desc;
				} else if (edible != null)
					desc = STRINGS.UI.GAMEOBJECTEFFECTS.CALORIES.Format(GameUtil.
						GetFormattedCalories(edible.FoodInfo.CaloriesPerUnit));
				else if (cso != null)
					desc = cso.element.FullDescription(false);
				else if (pe != null) {
					var element = ElementLoader.FindElementByHash(pe.ElementID);
					desc = element == null ? "" : element.FullDescription(false);
				}
				if (!string.IsNullOrEmpty(desc))
					infoPanel.SetLabel("Description", desc, "");
				if (!string.IsNullOrWhiteSpace(effect))
					infoPanel.SetLabel("Flavour", "\n" + effect, "");
				var roomClass = CodexEntryGenerator.GetRoomClassForObject(targetEntity);
				if (roomClass != null) {
					int n = roomClass.Length;
					text.Clear().AppendLine().Append(STRINGS.CODEX.HEADERS.BUILDINGTYPE).
						Append(':');
					for (int i = 0; i < n; i++)
						text.AppendLine().Append(Constants.TABBULLETSTRING).Append(
							roomClass[i]);
					infoPanel.SetLabel("RoomClass", text.ToString(), "");
				}
			}
			infoPanel.Commit();
		}

		/// <summary>
		/// Refreshes the Move To panel.
		/// </summary>
		private void RefreshMove() {
			var moveTo = lastSelection.moveTo;
			var movePanel = sis.movePanel;
			var text = CACHED_BUILDER;
			var canMove = lastSelection.canMove;
			if (moveTo != null) {
				var movingObjects = moveTo.movingObjects;
				int n = movingObjects.Count;
				for (int i = 0; i < n; i++) {
					var movable = movingObjects[i].Get();
					if (movable != null && (!movable.TryGetComponent(out PrimaryElement pe) ||
							pe.Mass != 0.0f)) {
						var go = movable.gameObject;
						string tooltip = DescribeMovable(go, pe);
						movePanel.SetLabelWithButton("move_" + i, text.ToString(), tooltip,
							new SelectMovable(movable).Select);
					}
				}
			} else if (canMove != null && canMove.IsMarkedForMove)
				movePanel.SetLabelWithButton("moveplacer",
					STRINGS.MISC.PLACERS.MOVEPICKUPABLEPLACER.PLACER_STATUS,
					STRINGS.MISC.PLACERS.MOVEPICKUPABLEPLACER.PLACER_STATUS_TOOLTIP,
					new SelectMovable(canMove.StorageProxy).Select);
			movePanel.Commit();
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
				string conditionName = StringFormatter.ToUpper(conditionType.ToString());
				var seen = HashSetPool<ProcessCondition, SimpleInfoScreen>.Allocate();
				ProcessConditionRow row;
				// Grab a cached header if possible
				if (nHeaders >= processHeaders.Count)
					processHeaders.Add(row = new ProcessConditionRow(Util.KInstantiateUI(sis.
						processConditionHeader.gameObject, conditionParent, true), true));
				else {
					row = processHeaders[nHeaders];
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
				string properName = lastSelection.selectable.GetProperName();
				var stressEntries = stressEntry.contextEntries;
				int n = stressEntries.Count;
				// Look for this Duplicant in the report
				for (int i = 0; i < n; i++) {
					var reportEntry = stressEntries[i];
					int nodeID = reportEntry.noteStorageId;
					// The IterateNotes callback allocates a delegate on the heap :/
					if (reportEntry.context == properName && allNoteEntries.entries.
							TryGetValue(nodeID, out var nodeEntries)) {
						var text = CACHED_BUILDER;
						float total = CompileNotes(nodeEntries, stressPanel);
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
						stressPanel.SetLabel("net_stress", text.ToString(), "");
						break;
					}
				}
			}
			stressPanel.Commit();
		}

		/// <summary>
		/// Updates the requirements and effects for non-Duplicant selections.
		/// </summary>
		/// <param name="target">The selected target object.</param>
		/// <param name="hasAmounts">true if there are Amounts, or false if there are none.</param>
		private void SetEffects(GameObject target, bool hasAmounts) {
			var descriptors = ListPool<Descriptor, SimpleInfoScreenWrapper>.Allocate();
			DescriptorAllocPatches.GetAllDescriptors(target, true, descriptors);
			var effects = DescriptorAllocPatches.GetGameObjectEffects(descriptors, true);
			bool hasEffects = effects.Count > 0;
			// Effects
			var effectsContent = sis.effectsContent;
			if (hasEffects) {
				effectsContent.gameObject.SetActive(true);
				effectsContent.SetDescriptors(effects);
			}
			sis.effectsPanel.gameObject.SetActive(hasEffects);
			effectsContent.gameObject.SetActive(hasEffects);
			// Requirements
			var requirements = DescriptorAllocPatches.GetRequirements(descriptors);
			bool showReq = requirements.Count > 0 && !hasAmounts;
			var requirementContent = sis.requirementContent;
			if (showReq) {
				requirementContent.gameObject.SetActive(true);
				requirementContent.SetDescriptors(requirements);
			}
			descriptors.Recycle();
			sis.requirementsPanel.gameObject.SetActive(showReq);
			requirementContent.gameObject.SetActive(showReq);
		}

		/// <summary>
		/// Updates the description and flavor text.
		/// </summary>
		/// <param name="target">The selected target object.</param>
		private void SetFlavor(GameObject target) {
			bool isDuplicant = lastSelection.identity != null;
			var infoPanel = sis.infoPanel;
			if (isDuplicant)
				infoPanel.gameObject.SetActive(false);
			else {
				string descText = "", flavorText = "";
				if (target.TryGetComponent(out InfoDescription description)) {
					descText = description.description;
					flavorText = description.effect;
				} else if (target.TryGetComponent(out Building building)) {
					descText = building.Def.Effect;
					flavorText = building.Desc;
				} else if (target.TryGetComponent(out Edible edible))
					descText = STRINGS.UI.GAMEOBJECTEFFECTS.CALORIES.Format(GameUtil.
						GetFormattedCalories(edible.FoodInfo.CaloriesPerUnit));
				else if (target.TryGetComponent(out CellSelectionObject cso))
					descText = cso.element.FullDescription(false);
				else if (target.TryGetComponent(out PrimaryElement pe)) {
					var element = ElementLoader.FindElementByHash(pe.ElementID);
					descText = element != null ? element.FullDescription(false) : "";
				}
				bool hasFlavor = !string.IsNullOrWhiteSpace(flavorText);
				if (descText != null)
					infoPanel.SetLabel("Description", descText, "");
				if (hasFlavor)
					infoPanel.SetLabel("Flavour", '\n' + flavorText, "");
				int constraints = GetRoomConstraints(target, infoPanel);
				infoPanel.Commit();
				infoPanel.gameObject.SetActive(descText != null || hasFlavor ||
					constraints > 0);
			}
		}

		/// <summary>
		/// Shows or hides panels depending on the active object.
		/// </summary>
		/// <param name="target">The selected target object.</param>
		private void SetPanels(GameObject target) {
			var modifiers = lastSelection.modifiers;
			bool isDuplicant = lastSelection.identity != null;
			Amounts amounts;
			bool hasAmounts = modifiers != null && (amounts = modifiers.amounts) != null &&
				amounts.Count > 0, hasProcess = lastSelection.conditions != null;
			if (hasAmounts && target.TryGetComponent(out Uprootable plant) && !target.
					TryGetComponent(out WiltCondition _))
				hasAmounts = plant.GetPlanterStorage != null;
			vitalsActive = hasAmounts;
			sis.vitalsPanel.gameObject.SetActive(hasAmounts);
			sis.vitalsPanel.lastSelectedEntity = hasAmounts ? target : null;
			sis.processConditionContainer.SetActive(hasProcess);
			if (hasProcess)
				sis.RefreshProcessConditionsPanel();
			// Effects and requirements
			if (isDuplicant) {
				sis.effectsPanel.gameObject.SetActive(false);
				sis.requirementsPanel.gameObject.SetActive(false);
			} else
				SetEffects(target, hasAmounts);
			SetFlavor(target);
			// Other headers
			sis.StoragePanel.SetTitle(isDuplicant ? DETAILTABS.DETAILS.
				GROUPNAME_MINION_CONTENTS : DETAILTABS.DETAILS.GROUPNAME_CONTENTS);
			if (lastSelection.fertility == null)
				sis.fertilityPanel.gameObject.SetActive(false);
			sis.rocketStatusContainer.gameObject.SetActive(lastSelection.
				rocketInterface != null || lastSelection.rocketModule != null);
		}

		public void Sim200ms(float _) {
			if (sis.lastTarget != null && isActiveAndEnabled)
				UpdatePanels();
		}

		/// <summary>
		/// Updates the meteors on the current planetoid.
		/// </summary>
		private void UpdateMeteors() {
			var worldContainer = lastSelection.world;
			var seasons = Db.Get().GameplaySeasons;
			meteors.Clear();
			foreach (string id in worldContainer.GetSeasonIds()) {
				var season = seasons.TryGet(id);
				if (season != null)
					foreach (var evt in season.events)
						if (evt is MeteorShowerEvent ms && evt.tags.Contains(GameTags.
								SpaceDanger))
							meteors[ms.Id] = ms;
			}
		}

		/// <summary>
		/// Updates the panels that should be updated every 200ms or when the selected object
		/// changes.
		/// </summary>
		private void UpdatePanels() {
			var vitalsPanel = sis.vitalsPanel;
			RefreshStress();
			RefreshStorage();
			RefreshMove();
			RefreshFertility();
			if (vitalsActive) {
				var vi = VitalsPanelWrapper.Instance;
				if (vi == null)
					vitalsPanel.Refresh(sis.selectedTarget);
				else
					vi.Update(vitalsPanel);
			}
			RefreshInfo();
			if (AllowBaseRocketPanel)
				sis.rocketSimpleInfoPanel.Refresh(sis.rocketStatusContainer,
					sis.selectedTarget);
			else
				RefreshRocket();
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// 
		/// Do I really need to repeat my spiel about big structs again?
		/// </summary>
		private readonly struct LastSelectionDetails {
			internal readonly IProcessConditionSet conditions;

			internal readonly BuildingComplete complete;
			
			internal readonly CellSelectionObject cso;

			internal readonly InfoDescription description;
			
			internal readonly Edible edible;

			internal readonly FertilityMonitor.Instance fertility;

			internal readonly MinionIdentity identity;

			internal readonly bool isAsteroid;

			internal readonly bool isRocket;

			internal readonly Modifiers modifiers;

			internal readonly CancellableMove moveTo;

			internal readonly Movable canMove;
			
			internal readonly PrimaryElement primaryElement;

			internal readonly CraftModuleInterface rocketInterface;

			internal readonly RocketModuleCluster rocketModule;

			internal readonly KSelectable selectable;

			internal readonly BuildingUnderConstruction underConstruction;

			internal readonly WorldContainer world;

			internal LastSelectionDetails(GameObject target) {
				ClusterGridEntity gridEntity;
				target.TryGetComponent(out canMove);
				target.TryGetComponent(out complete);
				target.TryGetComponent(out canMove);
				target.TryGetComponent(out conditions);
				target.TryGetComponent(out cso);
				target.TryGetComponent(out description);
				target.TryGetComponent(out edible);
				target.TryGetComponent(out identity);
				target.TryGetComponent(out modifiers);
				target.TryGetComponent(out moveTo);
				target.TryGetComponent(out primaryElement);
				target.TryGetComponent(out rocketModule);
				target.TryGetComponent(out selectable);
				target.TryGetComponent(out underConstruction);
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
		/// A wrapper class used to allow selecting movable items from the tooltip.
		/// </summary>
		private sealed class SelectMovable {
			private readonly Component target;

			public SelectMovable(Component target) {
				this.target = target;
			}

			public void Select() {
				if (target != null) {
					target.TryGetComponent(out KSelectable selectable);
					SelectTool.Instance.SelectAndFocus(target.transform.position, selectable,
						new Vector3(5f, 0.0f, 0.0f));
				}
			}
		}
	}
}
