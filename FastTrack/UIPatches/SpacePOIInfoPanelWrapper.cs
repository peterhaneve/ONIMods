/*
 * Copyright 2023 Peter Han
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
using UnityEngine;
using UnityEngine.UI;

using ElementWeight = System.Collections.Generic.KeyValuePair<SimHashes, float>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches used for the Harvestable POI (Spaced Out asteroid fields) info screen.
	/// </summary>
	public sealed class SpacePOIInfoPanelWrapper : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static SpacePOIInfoPanelWrapper instance;
		
		/// <summary>
		/// Initializes the available mass by element display.
		/// </summary>
		/// <param name="panel">The parent info panel.</param>
		/// <param name="harvestable">The POI to be harvested.</param>
		/// <param name="details">The panel to refresh.</param>
		private static void InitElements(SpacePOISimpleInfoPanel panel, HarvestablePOIStates.
				Instance harvestable, CollapsibleDetailContentPanel details) {
			var elementRows = panel.elementRows;
			var existingRows = DictionaryPool<Tag, GameObject, SpacePOISimpleInfoPanel>.
				Allocate();
			foreach (var pair in panel.elementRows)
				existingRows[pair.Key] = pair.Value;
			if (harvestable != null) {
				// Shared dictionary, does not allocate
				var sortedElements = ListPool<ElementWeight, SpacePOISimpleInfoPanel>.
					Allocate();
				sortedElements.AddRange(harvestable.configuration.GetElementsWithWeights());
				sortedElements.Sort(SortElementsByMassComparer.Instance);
				int n = sortedElements.Count;
				for (int i = 0; i < n; i++) {
					var pair = sortedElements[i];
					var element = pair.Key;
					var elementTag = new Tag(element.ToString());
					if (!elementRows.TryGetValue(elementTag, out GameObject row)) {
						row = Util.KInstantiateUI(panel.simpleInfoRoot.iconLabelRow, details.
							Content.gameObject, true);
						elementRows[elementTag] = row;
					} else
						row.SetActive(true);
					if (row.TryGetComponent(out HierarchyReferences hr)) {
						var uiSprite = Def.GetUISprite(elementTag);
						var icon = hr.GetReference<Image>("Icon");
						if (uiSprite != null) {
							icon.sprite = uiSprite.first;
							icon.color = uiSprite.second;
						}
						hr.GetReference<LocText>("NameLabel").SetText(ElementLoader.
							FindElementByHash(element).name);
						hr.GetReference<LocText>("ValueLabel").alignment = TMPro.
							TextAlignmentOptions.MidlineRight;
					}
					existingRows.Remove(elementTag);
				}
				sortedElements.Recycle();
			}
			// Turn off all rows that were not turned on
			foreach (var pair in existingRows)
				pair.Value.SetActive(false);
			existingRows.Recycle();
		}
		
		/// <summary>
		/// Refreshes the available artifact.
		/// </summary>
		/// <param name="panel">The parent info panel.</param>
		/// <param name="artifact">The artifact to be harvested.</param>
		private static void RefreshArtifacts(SpacePOISimpleInfoPanel panel, ArtifactPOIStates.
				Instance artifact) {
			if (panel.artifactRow.TryGetComponent(out HierarchyReferences hr)) {
				string text;
				var label = hr.GetReference<LocText>("ValueLabel");
				// Triggers an Init if necessary
				artifact.configuration.GetArtifactID();
				if (artifact.CanHarvestArtifact())
					text = STRINGS.UI.CLUSTERMAP.POI.ARTIFACTS_AVAILABLE;
				else
					text = STRINGS.UI.CLUSTERMAP.POI.ARTIFACTS_DEPLETED.Format(GameUtil.
						GetFormattedCycles(artifact.RechargeTimeRemaining(), "F1", true));
				if (label.text != text)
					label.SetText(text);
			}
		}

		/// <summary>
		/// Refreshes the available mass of each element in the POI.
		/// </summary>
		/// <param name="panel">The parent info panel.</param>
		/// <param name="harvestable">The POI to be harvested.</param>
		private static void RefreshElements(SpacePOISimpleInfoPanel panel, HarvestablePOIStates.
				Instance harvestable) {
			var elementRows = panel.elementRows;
			if (harvestable != null) {
				// Shared dictionary, does not allocate
				var weightedElements = harvestable.configuration.GetElementsWithWeights();
				float total = 0.0f;
				foreach (var pair in weightedElements) {
					float mass = pair.Value;
					total += mass;
				}
				// Avoid NaN
				total = Math.Max(total, 0.01f);
				foreach (var pair in weightedElements) {
					var elementTag = new Tag(pair.Key.ToString());
					// It is already visible, and in the correct order
					if (elementRows.TryGetValue(elementTag, out GameObject row) && row.
							TryGetComponent(out HierarchyReferences hr)) {
						string text = GameUtil.GetFormattedPercent(pair.Value * 100.0f /
							total);
						var label = hr.GetReference<LocText>("ValueLabel");
						if (label.text != text)
							label.SetText(text);
					}
				}
			}
		}
		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		private LocText massLabel;

		private bool wasActive;

		internal static void Cleanup() {
			instance?.Dispose();
			instance = null;
		}

		/// <summary>
		/// Find the asteroid field on the same tile as the selected rocket, if any.
		/// </summary>
		/// <param name="rocket">The rocket that is harvesting the tile.</param>
		/// <returns>The asteroid field occupying the same tile, or null if no asteroid fields
		/// occupy the same cluster map tile.</returns>
		private static HarvestablePOIClusterGridEntity FindFieldForRocket(
				ClusterGridEntity rocket) {
			HarvestablePOIClusterGridEntity asteroid = null;
			// List is preallocated, not new per call
			var shared = ClusterGrid.Instance.GetEntitiesOnCell(rocket.Location);
			int n = shared.Count;
			for (int i = 0; i < n; i++)
				if (shared[i] is HarvestablePOIClusterGridEntity asteroidEntity) {
					asteroid = asteroidEntity;
					break;
				}
			return asteroid;
		}

		internal static void Init() {
			Cleanup();
			instance = new SpacePOIInfoPanelWrapper();
		}

		internal SpacePOIInfoPanelWrapper() {
			lastSelection = default;
			massLabel = null;
			wasActive = false;
		}

		public void Dispose() {
			lastSelection = default;
			massLabel = null;
		}

		/// <summary>
		/// Performs one-time initialization.
		/// </summary>
		/// <param name="panel">The parent info panel.</param>
		/// <param name="details">The panel to initialize.</param>
		private void Init(SpacePOISimpleInfoPanel panel, CollapsibleDetailContentPanel details)
		{
			var root = panel.simpleInfoRoot;
			var parent = details.Content.gameObject;
			var header = Util.KInstantiateUI(root.iconLabelRow, parent, true);
			panel.massHeader = header;
			details.SetTitle(STRINGS.UI.CLUSTERMAP.POI.TITLE);
			// Set the icon, name and alignment once
			if (header.TryGetComponent(out HierarchyReferences hr)) {
				var sprite = Assets.GetSprite("icon_asteroid_type");
				if (sprite != null)
					hr.GetReference<Image>("Icon").sprite = sprite;
				hr.GetReference<LocText>("NameLabel").SetText(STRINGS.UI.CLUSTERMAP.POI.
					MASS_REMAINING);
				massLabel = hr.GetReference<LocText>("ValueLabel");
				massLabel.alignment = TMPro.TextAlignmentOptions.MidlineRight;
			} else
				massLabel = null;
			if (panel.artifactsSpacer == null) {
				var ar = Util.KInstantiateUI(root.iconLabelRow, parent, true);
				panel.artifactsSpacer = Util.KInstantiateUI(root.spacerRow, parent, true);
				panel.artifactRow = ar;
				if (ar.TryGetComponent(out hr)) {
					var icon = hr.GetReference<Image>("Icon");
					// Triggers an Init if necessary
					hr.GetReference<LocText>("NameLabel").SetText(STRINGS.UI.CLUSTERMAP.POI.
						ARTIFACTS);
					hr.GetReference<LocText>("ValueLabel").alignment = TMPro.
						TextAlignmentOptions.MidlineRight;
					icon.sprite = Assets.GetSprite("ic_artifacts");
					icon.color = Color.black;
				}
			}
		}

		/// <summary>
		/// Refreshes the space POI info panel.
		/// </summary>
		/// <param name="panel">The parent info panel.</param>
		/// <param name="details">The panel to refresh.</param>
		/// <param name="target">The currently selected object.</param>
		public void Refresh(SpacePOISimpleInfoPanel panel,
				CollapsibleDetailContentPanel details, GameObject target) {
			bool active = false;
			if (target != null) {
				bool changed = lastSelection.lastTarget != target;
				if (changed) {
					lastSelection = new LastSelectionDetails(target);
					if (panel.massHeader == null)
						Init(panel, details);
				}
				var asteroidField = lastSelection.asteroidField;
				var artifact = lastSelection.artifact;
				var harvestable = lastSelection.harvestable;
				if (asteroidField != null || artifact != null) {
					if (changed) {
						InitElements(panel, harvestable, details);
						panel.artifactsSpacer.transform.SetAsLastSibling();
						panel.artifactRow.transform.SetAsLastSibling();
					}
					if (harvestable != null && massLabel != null)
						RefreshMassHeader(harvestable);
					RefreshElements(panel, harvestable);
					if (artifact != null)
						RefreshArtifacts(panel, artifact);
					if (!wasActive)
						details.gameObject.SetActive(true);
					active = true;
					wasActive = true;
				}
			}
			if (!active && wasActive) {
				details.gameObject.SetActive(false);
				wasActive = false;
			}
		}


		/// <summary>
		/// Refreshes the maximum available mass heading.
		/// </summary>
		/// <param name="harvestable">The POI to be harvested.</param>
		private void RefreshMassHeader(HarvestablePOIStates.Instance harvestable) {
			string mass = GameUtil.GetFormattedMass(harvestable.poiCapacity);
			if (massLabel.text != mass)
				massLabel.SetText(mass);
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// </summary>
		private struct LastSelectionDetails {
			internal readonly ArtifactPOIStates.Instance artifact;

			internal readonly HarvestablePOIClusterGridEntity asteroidField;

			internal readonly GameObject lastTarget;

			internal readonly HarvestablePOIStates.Instance harvestable;

			internal LastSelectionDetails(GameObject target) {
				if (target.TryGetComponent(out ClusterGridEntity gridEntity) && gridEntity is
						HarvestablePOIClusterGridEntity asteroid) {
					asteroidField = asteroid;
					harvestable = target.GetSMI<HarvestablePOIStates.Instance>();
					artifact = target.GetSMI<ArtifactPOIStates.Instance>();
				} else if (gridEntity != null) {
					asteroidField = FindFieldForRocket(gridEntity);
					if (asteroidField != null) {
						var go = asteroidField.gameObject;
						harvestable = go.GetSMI<HarvestablePOIStates.Instance>();
						artifact = go.GetSMI<ArtifactPOIStates.Instance>();
					} else {
						artifact = target.GetSMI<ArtifactPOIStates.Instance>();
						harvestable = null;
					}
				} else {
					asteroidField = null;
					artifact = null;
					harvestable = null;
				}
				lastTarget = target;
			}
		}

		/// <summary>
		/// Applied to SpacePOISimpleInfoPanel to speed up rendering of space POI objects.
		/// </summary>
		[HarmonyPatch(typeof(SpacePOISimpleInfoPanel), nameof(SpacePOISimpleInfoPanel.
			Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(SpacePOISimpleInfoPanel __instance,
					CollapsibleDetailContentPanel spacePOIPanel, GameObject selectedTarget) {
				var inst = instance;
				bool run = inst == null;
				if (!run)
					inst.Refresh(__instance, spacePOIPanel, selectedTarget);
				return run;
			}
		}
	}

	/// <summary>
	/// A simple comparator that sorts lists of POI elements by mass remaining, descending.
	/// </summary>
	internal sealed class SortElementsByMassComparer : IComparer<ElementWeight> {
		internal static readonly SortElementsByMassComparer Instance =
			new SortElementsByMassComparer();

		private SortElementsByMassComparer() { }

		public int Compare(ElementWeight x, ElementWeight y) {
			return y.Value.CompareTo(x.Value);
		}
	}
}
