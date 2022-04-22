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
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using CheckboxLineDisplayType = MinionVitalsPanel.CheckboxLineDisplayType;
using CONDITIONS_GROWING = STRINGS.UI.VITALSSCREEN.CONDITIONS_GROWING;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the vitals panel to avoid recalculating so much every
	/// frame.
	/// </summary>
	public sealed class VitalsPanelWrapper {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static VitalsPanelWrapper instance;

		/// <summary>
		/// The color used for conditions that are not met. Could not find a const reference
		/// for this hardcoded base game color.
		/// </summary>
		private static readonly Color UNMET_CONDITION = new Color(0.99215686f, 0f,
			0.101960786f);

		/// <summary>
		/// Called at shutdown to avoid leaking references.
		/// </summary>
		internal static void Cleanup() {
			instance = null;
		}

		/// <summary>
		/// Initializes and resets the last selected item.
		/// </summary>
		internal static void Init() {
			Cleanup();
			instance = new VitalsPanelWrapper();
		}

		/// <summary>
		/// Updates the displayed plant growth information. It is invariant per plant so it
		/// only needs to be done once per selection.
		/// </summary>
		/// <param name="panel">The vitals panel wrapper to update.</param>
		/// <param name="plant">The plant to display.</param>
		/// <param name="instance">The vitals panel to update.</param>
		/// <param name="isDecor">Whether the plant is a decor plant.</param>
		/// <param name="hasAdditional">Whether the plant has additional requirements.</param>
		private static void UpdatePlantGrowth(ref VitalsPanelState panel, GameObject plant,
				bool isDecor, bool hasAdditional) {
			var amountLines = panel.amountLines;
			var attributeLines = panel.attributeLines;
			var aLabel = panel.plantAdditionalLabel;
			int n = amountLines.Length;
			if (plant.TryGetComponent(out ReceptacleMonitor rm)) {
				// Update the invariant text for plants
				if (plant.TryGetComponent(out Growing growing)) {
					string wildGrowth = GameUtil.GetFormattedCycles(growing.WildGrowthTime());
					string tameGrowth = GameUtil.GetFormattedCycles(growing.
						DomesticGrowthTime());
					panel.plantNormalLabel.text = CONDITIONS_GROWING.WILD.BASE.Format(
						wildGrowth);
					panel.plantNormalTooltip.SetSimpleTooltip(CONDITIONS_GROWING.WILD.TOOLTIP.
						Format(wildGrowth));
					aLabel.color = rm.Replanted ? Color.black : Color.grey;
					aLabel.text = hasAdditional ? CONDITIONS_GROWING.ADDITIONAL_DOMESTIC.BASE.
						Format(tameGrowth) : CONDITIONS_GROWING.DOMESTIC.BASE.
						Format(tameGrowth);
					panel.plantAdditionalTooltip.SetSimpleTooltip(CONDITIONS_GROWING.
						ADDITIONAL_DOMESTIC.TOOLTIP.Format(tameGrowth));
				} else {
					string wildGrowth = Util.FormatTwoDecimalPlace(100.0f * TUNING.CROPS.
						WILD_GROWTH_RATE_MODIFIER);
					string tameGrowth = Util.FormatTwoDecimalPlace(100.0f);
					panel.plantNormalLabel.text = isDecor ? CONDITIONS_GROWING.WILD_DECOR.BASE.
						ToString() : CONDITIONS_GROWING.WILD_INSTANT.BASE.Format(wildGrowth);
					panel.plantNormalTooltip.SetSimpleTooltip(CONDITIONS_GROWING.WILD_INSTANT.
						TOOLTIP);
					aLabel.color = (rm == null || rm.Replanted) ? Color.black : Color.grey;
					aLabel.text = CONDITIONS_GROWING.ADDITIONAL_DOMESTIC_INSTANT.BASE.
						Format(tameGrowth);
					panel.plantAdditionalTooltip.SetSimpleTooltip(CONDITIONS_GROWING.
						ADDITIONAL_DOMESTIC_INSTANT.TOOLTIP);
				}
			}
			// Turn off the attributes and amounts
			for (int i = 0; i < n; i++)
				amountLines[i].go.SetActive(false);
			n = attributeLines.Length;
			for (int i = 0; i < n; i++)
				attributeLines[i].go.SetActive(false);
		}

		/// <summary>
		/// A temporary lookup dictionary used to expedite amounts updating.
		/// </summary>
		private readonly IDictionary<string, AmountInstance> amountLookup;

		/// <summary>
		/// A temporary lookup dictionary used to expedite attributes updating.
		/// </summary>
		private readonly IDictionary<string, AttributeInstance> attributeLookup;

		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		/// <summary>
		/// Stores components in the vitals panel that will not change until the next load.
		/// </summary>
		private VitalsPanelState panel;

		private VitalsPanelWrapper() {
			amountLookup = new Dictionary<string, AmountInstance>(64);
			attributeLookup = new Dictionary<string, AttributeInstance>(64);
			lastSelection = default;
			panel = default;
		}

		/// <summary>
		/// Populates the lookup tables for amounts and attributes.
		/// </summary>
		/// <param name="modifiers">The modifiers of the currently selected object.</param>
		private void FillLookup(Modifiers modifiers) {
			var amounts = modifiers.amounts.ModifierList;
			var attributes = modifiers.attributes.AttributeTable;
			int n = amounts.Count;
			if (amountLookup.Count < 1)
				for (int i = 0; i < n; i++) {
					var amountInstance = amounts[i];
					amountLookup[amountInstance.amount.Id] = amountInstance;
				}
			n = attributes.Count;
			if (attributeLookup.Count < 1)
				for (int i = 0; i < n; i++) {
					var attributeInstance = attributes[i];
					attributeLookup[attributeInstance.modifier.Id] = attributeInstance;
				}
		}

		/// <summary>
		/// Checks to see if there are additional requirements for plant domestic growth.
		/// </summary>
		/// <param name="target">The currently selected object.</param>
		/// <param name="modifiers">The modifiers of the currently selected object.</param>
		/// <returns>Whether the plant has additional domestic growth requirements.</returns>
		private bool HasAdditionalPlantRequirements(GameObject target, Modifiers modifiers) {
			var additional = panel.vitals.conditionsContainerAdditional;
			bool hasAdditional = false;
			// Check for additional conditions that exist
			var checkboxLines = panel.checkboxLines;
			int n = checkboxLines.Length;
			FillLookup(modifiers);
			for (int i = 0; i < n; i++) {
				ref var checkboxLine = ref checkboxLines[i];
				string amountID = checkboxLine.amountID;
				// All of the ones that matter have loop invariant conditions
				if (checkboxLine.parentContainer == additional && (amountID == null ||
						amountLookup.ContainsKey(amountID)) && checkboxLine.displayCondition(
						target) != CheckboxLineDisplayType.Hidden) {
					hasAdditional = true;
					break;
				}
			}
			return hasAdditional;
		}

		/// <summary>
		/// Updates the vitals panel.
		/// </summary>
		/// <param name="instance">The panel to update.</param>
		internal void Update(MinionVitalsPanel instance) {
			var entity = instance.selectedEntity;
			GameObject go;
			// Update the wrapper if necessary
			if (instance != panel.vitals)
				panel = new VitalsPanelState(instance);
			if (entity != null && (go = entity.gameObject) != null && panel.Populate()) {
				Modifiers modifiers;
				if (go != lastSelection.target)
					lastSelection = new LastSelectionDetails(go, this);
				modifiers = lastSelection.modifiers;
				if (modifiers != null) {
					FillLookup(modifiers);
					if (!lastSelection.hasWilting)
						// If it is not a plant, update amounts
						UpdateAmounts();
					UpdateChecks(go);
				}
				amountLookup.Clear();
				attributeLookup.Clear();
			}
		}

		/// <summary>
		/// Updates the displayed Amounts and Attributes of the selected object.
		/// </summary>
		private void UpdateAmounts() {
			var amountLines = panel.amountLines;
			var attributeLines = panel.attributeLines;
			int n = amountLines.Length;
			for (int i = 0; i < n; i++) {
				ref var amountLine = ref amountLines[i];
				var gameObject = amountLine.go;
				var amount = amountLine.amount;
				bool visible = amountLookup.TryGetValue(amount.Id, out AmountInstance ai) &&
					!ai.hide;
				if (visible) {
					amountLine.locText.SetText(amount.GetDescription(ai));
					amountLine.toolTip.toolTip = amountLine.toolTipFunc.Invoke(ai);
					amountLine.imageToggle.SetValue(ai);
				}
				if (gameObject.activeSelf != visible)
					gameObject.SetActive(visible);
			}
			n = attributeLines.Length;
			for (int i = 0; i < n; i++) {
				ref var attributeLine = ref attributeLines[i];
				var gameObject = attributeLine.go;
				var attribute = attributeLine.attribute;
				bool visible = attributeLookup.TryGetValue(attribute.Id,
					out AttributeInstance ai) && !ai.hide;
				if (visible) {
					attributeLine.locText.SetText(attribute.GetDescription(ai));
					attributeLine.toolTip.toolTip = attributeLine.toolTipFunc.Invoke(ai);
				}
				if (gameObject.activeSelf != visible)
					gameObject.SetActive(visible);
			}
		}

		/// <summary>
		/// Updates the checkbox lines shown in the vitals panel for boolean conditions.
		/// </summary>
		/// <param name="target">The currently selected object.</param>
		private void UpdateChecks(GameObject target) {
			var additional = panel.vitals.conditionsContainerAdditional;
			var checkboxLines = panel.checkboxLines;
			int n = checkboxLines.Length;
			CheckboxLineDisplayType displayType;
			for (int i = 0; i < n; i++) {
				ref var checkboxLine = ref checkboxLines[i];
				var checkboxGO = checkboxLine.gameObject;
				string amountID = checkboxLine.amountID;
				bool visible = checkboxLine.visible;
				// Display it if there is no amount required, or the amount is present
				if (amountID == null || amountLookup.ContainsKey(amountID))
					displayType = checkboxLine.displayCondition(target);
				else
					displayType = CheckboxLineDisplayType.Hidden;
				if (displayType != CheckboxLineDisplayType.Hidden) {
					var transform = checkboxLine.checkTransform;
					bool isSatisfied = checkboxLine.getValue(target);
					var textField = checkboxLine.textField;
					var img = checkboxLine.checkImage;
					var parent = checkboxLine.parentContainer;
					textField.SetText(checkboxLine.getLabelText(target));
					checkboxLine.checkGO.SetActive(isSatisfied);
					// Reparent it if necessary
					if (transform.parent != parent) {
						transform.SetParent(parent);
						transform.localScale = Vector3.one;
					}
					// Set the correct color
					if (displayType == CheckboxLineDisplayType.Normal) {
						if (isSatisfied) {
							textField.color = Color.black;
							img.color = Color.black;
						} else {
							textField.color = UNMET_CONDITION;
							img.color = UNMET_CONDITION;
						}
					} else {
						textField.color = Color.grey;
						img.color = Color.grey;
					}
					if (!visible) {
						checkboxGO.SetActive(true);
						checkboxLine.visible = true;
					}
				} else if (visible) {
					checkboxGO.SetActive(false);
					checkboxLine.visible = false;
				}
			}
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// </summary>
		private struct LastSelectionDetails {
			internal readonly bool hasWilting;

			internal readonly bool isDecorPlant;

			internal readonly Modifiers modifiers;

			/// <summary>
			/// The selected target.
			/// </summary>
			public readonly GameObject target;

			internal LastSelectionDetails(GameObject go, VitalsPanelWrapper parent) {
				ref var panel = ref parent.panel;
				bool isPlant = go.TryGetComponent(out WiltCondition _), isDecor = go.
					HasTag(GameTags.Decoration);
				hasWilting = isPlant;
				isDecorPlant = isDecor;
				target = go;
				if (go.TryGetComponent(out modifiers)) {
					panel.plantNormalGO.SetActive(isPlant);
					panel.plantAdditionalGO.gameObject.SetActive(isPlant && !isDecor);
					if (isPlant)
						UpdatePlantGrowth(ref panel, go, isDecor, parent.
							HasAdditionalPlantRequirements(go, modifiers));
				}
			}
		}

		/// <summary>
		/// Stores component references to the last used vitals panel.
		/// 
		/// Big structs are normally a no-no because copying them is expensive. However, this
		/// is mostly a container, and should never be copied.
		/// </summary>
		private struct VitalsPanelState {
			internal MinionVitalsPanel.AmountLine[] amountLines;

			internal MinionVitalsPanel.AttributeLine[] attributeLines;

			internal CheckboxLineExpanded[] checkboxLines;

			internal readonly GameObject plantAdditionalGO;

			internal readonly LocText plantAdditionalLabel;

			internal readonly ToolTip plantAdditionalTooltip;

			internal readonly GameObject plantNormalGO;

			internal readonly LocText plantNormalLabel;

			internal readonly ToolTip plantNormalTooltip;

			/// <summary>
			/// The last used vitals panel. Should never change per load...
			/// </summary>
			public readonly MinionVitalsPanel vitals;

			internal VitalsPanelState(MinionVitalsPanel instance) {
				var normal = instance.conditionsContainerNormal;
				var additional = instance.conditionsContainerAdditional;
				LocText nLabel = null, aLabel = null;
				if (normal.TryGetComponent(out HierarchyReferences hr))
					nLabel = hr.GetReference<LocText>("Label");
				if (additional.TryGetComponent(out hr))
					aLabel = hr.GetReference<LocText>("Label");
				plantAdditionalGO = additional.gameObject;
				plantAdditionalLabel = aLabel;
				aLabel.TryGetComponent(out plantAdditionalTooltip);
				plantNormalGO = normal.gameObject;
				plantNormalLabel = nLabel;
				nLabel.TryGetComponent(out plantNormalTooltip);
				vitals = instance;
				amountLines = new MinionVitalsPanel.AmountLine[0];
				attributeLines = new MinionVitalsPanel.AttributeLine[0];
				checkboxLines = new CheckboxLineExpanded[0];
			}

			/// <summary>
			/// Fills in the cached arrays if necessary.
			/// </summary>
			/// <returns>true if the arrays are ready, or false if they are empty.</returns>
			internal bool Populate() {
				var instance = vitals;
				var oldAmountLines = instance.amountsLines;
				int n = oldAmountLines.Count;
				bool ready = n > 0;
				if (ready) {
					var oldAttributeLines = instance.attributesLines;
					var oldCheckLines = instance.checkboxLines;
					// Lots less struct copying with ref vars!
					if (amountLines.Length != n)
						amountLines = oldAmountLines.ToArray();
					n = oldAttributeLines.Count;
					if (attributeLines.Length != n)
						attributeLines = instance.attributesLines.ToArray();
					n = oldCheckLines.Count;
					if (checkboxLines.Length != n) {
						var newLines = new CheckboxLineExpanded[n];
						for (int i = 0; i < n; i++) {
							var line = oldCheckLines[i];
							newLines[i] = new CheckboxLineExpanded(ref line);
						}
						checkboxLines = newLines;
					}
				}
				return ready;
			}
		}

		/// <summary>
		/// Expands the MinionVitalsPanel.CheckboxLine structure to include additional
		/// information that is needed each frame.
		/// </summary>
		private struct CheckboxLineExpanded {
			internal readonly string amountID;

			internal readonly GameObject checkGO;

			internal readonly Image checkImage;

			internal readonly Transform checkTransform;

			internal readonly GameObject gameObject;

			internal readonly Func<GameObject, CheckboxLineDisplayType> displayCondition;

			internal readonly Func<GameObject, string> getLabelText;

			internal readonly Func<GameObject, string> getTooltip;

			internal readonly Func<GameObject, bool> getValue;

			internal readonly Transform parentContainer;

			internal readonly LocText textField;

			internal bool visible;

			internal CheckboxLineExpanded(ref MinionVitalsPanel.CheckboxLine original) {
				amountID = original.amount?.Id;
				displayCondition = original.display_condition;
				gameObject = original.go;
				getLabelText = original.label_text_func;
				getTooltip = original.tooltip;
				getValue = original.get_value;
				parentContainer = original.parentContainer;
				textField = original.locText;
				// Computed fields
				if (gameObject.TryGetComponent(out HierarchyReferences hr)) {
					checkGO = hr.GetReference("Check").gameObject;
					checkGO.transform.parent.TryGetComponent(out checkImage);
				} else {
					checkGO = null;
					checkImage = null;
				}
				checkTransform = gameObject.transform;
				visible = false;
				gameObject.SetActive(false);
			}
		}
	}

	/// <summary>
	/// Applied to MinionVitalsPanel to rewrite the very slow and memory hungry method that is
	/// run every frame.
	/// </summary>
	[HarmonyPatch(typeof(MinionVitalsPanel), nameof(MinionVitalsPanel.Refresh))]
	public static class MinionVitalsPanel_Refresh_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before Refresh runs.
		/// </summary>
		internal static bool Prefix(MinionVitalsPanel __instance) {
			var inst = VitalsPanelWrapper.instance;
			bool run = inst == null;
			if (!run)
				inst.Update(__instance);
			return run;
		}
	}
}
