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

using SideScreenPair = System.Collections.Generic.KeyValuePair<UnityEngine.GameObject, int>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the details panel to avoid recalculating so much every
	/// time it is refreshed.
	/// </summary>
	public sealed class DetailsPanelWrapper {
		/// <summary>
		/// Avoids allocating a new instance every time a link is formatted.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static DetailsPanelWrapper instance;

		/// <summary>
		/// Called at shutdown to avoid leaking references.
		/// </summary>
		internal static void Cleanup() {
			instance = null;
		}

		/// <summary>
		/// Enables or disables side screens to match the selected object, and selects an
		/// appropriate default tab.
		/// </summary>
		/// <param name="ds">The details screen being spawned.</param>
		/// <param name="target">The selected target.</param>
		/// <param name="screens">The side screens to show or hide.</param>
		/// <returns>The number of tabs that are active.</returns>
		private static int EnableScreens(DetailsScreen ds, GameObject target,
				DetailsScreen.Screens[] screens) {
			int n = screens.Length, activeIndex = -1, enabledTabs = 0, lastActive =
				ds.previouslyActiveTab;
			// Vanilla checks the details screen itself to see if it is dead!?!? But no
			// side screens are set to check this so the bug goes by unnoticed
			bool isDead = target != null && target.TryGetComponent(out KPrefabID id) && id.
				HasTag(GameTags.Dead);
			string lastActiveName = null;
			// Find the last selected active screen
			if (lastActive >= 0 && lastActive < n)
				lastActiveName = screens[lastActive].name;
			for (int i = 0; i < n; i++) {
				ref var screen = ref screens[i];
				bool enabled = screen.screen.IsValidForTarget(target) && !(isDead && screen.
					hideWhenDead);
				ds.SetTabEnabled(screen.tabIdx, enabled);
				if (enabled) {
					enabledTabs++;
					if (activeIndex < 0) {
						var mode = SimDebugView.Instance.GetMode();
						if (mode != OverlayModes.None.ID) {
							// Is it related to the current overlay mode?
							if (mode == screen.focusInViewMode)
								activeIndex = i;
						} else if (lastActiveName != null && screen.name == lastActiveName)
							// Is it the screen that was last selected?
							activeIndex = screen.tabIdx;
					}
				}
			}
			ds.ActivateTab(Math.Max(activeIndex, 0));
			return enabledTabs;
		}

		/// <summary>
		/// A faster and lower-allocation version of CodexCache.FormatLinkID.
		/// </summary>
		/// <param name="id">The ID to link.</param>
		/// <param name="find">If specified, replaces the specified text if found in the link...</param>
		/// <param name="replace">... with the value of the replace parameter.</param>
		/// <returns>The link ID.</returns>
		private static string FormatLinkID(string id, string find = null,
				string replace = null) {
			var text = CACHED_BUILDER;
			int n;
			text.Clear();
			text.Append(id);
			text.Replace("_", "");
			n = text.Length;
			// ToUpper in place, avoid the Turkish test problem
			for (int i = 0; i < n; i++)
				text[i] = char.ToUpperInvariant(text[i]);
			if (find != null && replace != null)
				text.Replace(find, replace);
			return text.ToString();
		}

		/// <summary>
		/// Gets the codex link text for a specified object.
		/// </summary>
		/// <param name="go">The currently selected object.</param>
		/// <param name="cso">The cell selection stand-in for the selected object.</param>
		/// <returns>The codex link text to use.</returns>
		private static string GetCodexLink(GameObject go, CellSelectionObject cso) {
			string codexText;
			if (cso != null)
				codexText = FormatLinkID(cso.element.tag.ToString());
			else if (go.TryGetComponent(out BuildingUnderConstruction building))
				codexText = FormatLinkID(building.Def.PrefabID);
			else if (go.TryGetComponent(out KPrefabID id)) {
				string prefabID = id.PrefabTag.ToString();
				if (id.HasTag(GameTags.Creature))
					codexText = FormatLinkID(prefabID, "BABY", "");
				else if (id.HasTag(GameTags.Plant))
					codexText = FormatLinkID(prefabID, "SEED", "");
				else if (go.TryGetComponent(out BudUprootedMonitor monitor)) {
					KPrefabID parent;
					BuddingTrunk trunk;
					if ((parent = monitor.parentObject.Get()) != null)
						codexText = FormatLinkID(parent.PrefabTag.ToString());
					else if (go.TryGetComponent(out TreeBud bud) && (trunk = bud.buddingTrunk.
							Get()) != null)
						// Special case for trees?
						codexText = FormatLinkID(trunk.PrefabID().ToString());
					else
						codexText = FormatLinkID(prefabID);
				} else
					codexText = FormatLinkID(prefabID);
			} else
				codexText = null;
			return codexText;
		}

		/// <summary>
		/// Initializes and resets the last selected item.
		/// </summary>
		internal static void Init() {
			Cleanup();
			instance = new DetailsPanelWrapper();
		}

		/// <summary>
		/// Creates the built-in game side screens.
		/// </summary>
		/// <param name="ds">The details screen being spawned.</param>
		/// <param name="screens">The side screens to create.</param>
		private static void InstantiateScreens(DetailsScreen ds,
				DetailsScreen.Screens[] screens) {
			var body = ds.body.gameObject;
			int n = screens.Length;
			// First time initialization, ref screen allows reassignment back!
			for (int i = 0; i < n; i++) {
				ref var screen = ref screens[i];
				var targetScreen = screen.screen;
				var screenGO = KScreenManager.Instance.InstantiateScreen(targetScreen.
					gameObject, body).gameObject;
				if (screenGO.TryGetComponent(out targetScreen)) {
					screen.screen = targetScreen;
					screen.tabIdx = ds.AddTab(screen.icon, Strings.Get(screen.
						displayName), targetScreen, Strings.Get(screen.tooltip));
				}
			}
		}

		/// <summary>
		/// Sorts the side screens using their sort key.
		/// </summary>
		/// <param name="ds">The details screen to sort.</param>
		/// <param name="target">The selected target.</param>
		private static void SortSideScreens(DetailsScreen ds, GameObject target) {
			var sideScreens = ds.sideScreens;
			var sortedScreens = ds.sortedSideScreens;
			int n;
			sortedScreens.Clear();
			if (sideScreens != null && sideScreens.Count > 0) {
				int currentOrder = 0;
				var currentScreen = ds.currentSideScreen;
				var dss = ds.sideScreen;
				GameObject instGO;
				n = sideScreens.Count;
				if (currentScreen != null) {
					if (!currentScreen.gameObject.activeSelf)
						currentScreen = null;
					else
						currentOrder = currentScreen.GetSideScreenSortOrder();
				}
				for (int i = 0; i < n; i++) {
					var screen = sideScreens[i];
					var inst = screen.screenInstance;
					var prefab = screen.screenPrefab;
					if (prefab.IsValidForTarget(target)) {
						if (inst == null) {
							inst = Util.KInstantiateUI<SideScreenContent>(prefab.gameObject,
								ds.sideScreenContentBody);
							screen.screenInstance = inst;
						}
						int sortOrder = inst.GetSideScreenSortOrder();
						if (!dss.activeInHierarchy)
							dss.SetActive(true);
						inst.SetTarget(target);
						inst.Show(true);
						sortedScreens.Add(new SideScreenPair(inst.gameObject, sortOrder));
						if (currentScreen == null || sortOrder > currentOrder) {
							ds.currentSideScreen = currentScreen = inst;
							ds.sideScreenTitle.SetText(inst.GetTitle());
						}
					} else if (inst != null && (instGO = inst.gameObject).activeSelf) {
						instGO.SetActive(false);
						// If the current screen was just hidden, allow another one to
						// take its place
						if (inst == currentScreen)
							currentScreen = null;
					}
				};
			}
			sortedScreens.Sort(SideScreenOrderComparer.Instance);
			n = sortedScreens.Count;
			for (int i = 0; i < n; i++)
				sortedScreens[i].Key.transform.SetSiblingIndex(i);
		}

		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		private DetailsPanelWrapper() {
			lastSelection = default;
		}

		/// <summary>
		/// Stores component references to the last selected object. DetailsScreen already
		/// keeps a reference to its current target, so only need the components.
		/// </summary>
		private struct LastSelectionDetails {
			internal readonly string codexLink;

			internal readonly CellSelectionObject cso;

			internal readonly ToolTip editTooltip;

			internal readonly MinionIdentity id;

			internal readonly MinionResume resume;

			internal readonly UserNameable rename;

			internal readonly KSelectable selectable;

			internal LastSelectionDetails(GameObject go, CellSelectionObject cso,
					DetailsScreen instance) {
				var title = instance.TabTitle;
				string rawLink = GetCodexLink(go, cso);
				this.cso = cso;
				if (rawLink != null && (CodexCache.entries.ContainsKey(rawLink) || CodexCache.
						FindSubEntry(rawLink) != null))
					codexLink = rawLink;
				else
					codexLink = "";
				go.TryGetComponent(out selectable);
				go.TryGetComponent(out id);
				go.TryGetComponent(out rename);
				go.TryGetComponent(out resume);
				if (title != null)
					title.editNameButton.TryGetComponent(out editTooltip);
				else
					editTooltip = null;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to free memory when an object is deselected.
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.DeselectAndClose))]
		internal static class DeselectAndClose_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied after DeselectAndClose runs.
			/// </summary>
			internal static void Postfix() {
				var inst = instance;
				if (inst != null)
					inst.lastSelection = default;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to break a hard dependency when renaming Duplicants.
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.OnNameChanged))]
		internal static class OnNameChanged_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before OnNameChanged runs.
			/// </summary>
			internal static bool Prefix(DetailsScreen __instance, string newName) {
				var inst = instance;
				__instance.isEditing = false;
				if (!string.IsNullOrEmpty(newName) && inst != null) {
					ref var lastSelection = ref inst.lastSelection;
					var editTooltip = lastSelection.editTooltip;
					var id = lastSelection.id;
					var rename = lastSelection.rename;
					if (id != null)
						id.SetName(newName);
					else if (rename != null)
						rename.SetName(newName);
					if (editTooltip != null && id == null)
						editTooltip.toolTip = STRINGS.UI.TOOLTIPS.EDITNAMEGENERIC.Format(
							newName);
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to make opening the codex entry much faster!
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.OpenCodexEntry))]
		internal static class OpenCodexEntry_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before OpenCodexEntry runs.
			/// </summary>
			internal static bool Prefix(DetailsScreen __instance) {
				var inst = instance;
				if (inst != null) {
					string codexLink = inst.lastSelection.codexLink;
					if (!string.IsNullOrEmpty(codexLink))
						ManagementMenu.Instance.OpenCodexToEntry(codexLink);
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to optimize its refresh.
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix(DetailsScreen __instance, GameObject go) {
				var screens = __instance.screens;
				var oldTarget = __instance.target;
				var inst = instance;
				if (screens != null) {
					__instance.target = go;
					if (go.TryGetComponent(out CellSelectionObject cso))
						cso.OnObjectSelected(null);
					if ((oldTarget == null || go != oldTarget) && inst != null)
						inst.lastSelection = new LastSelectionDetails(go, cso, __instance);
					if (!__instance.HasActivated) {
						InstantiateScreens(__instance, screens);
						__instance.onTabActivated += __instance.OnTabActivated;
						__instance.HasActivated = true;
					}
					int tabCount = EnableScreens(__instance, go, screens);
					__instance.tabHeaderContainer.gameObject.SetActive(tabCount > 1);
					SortSideScreens(__instance, go);
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to speed up setting the title and break a hard
		/// MinionIdentity dependency (hey Romen!).
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.SetTitle), typeof(int))]
		internal static class SetTitle_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before SetTitle runs.
			/// </summary>
			internal static bool Prefix(DetailsScreen __instance) {
				var inst = instance;
				if (inst != null) {
					ref var lastSelection = ref inst.lastSelection;
					string codexLink = lastSelection.codexLink;
					bool valid = !string.IsNullOrEmpty(codexLink);
					var editTooltip = lastSelection.editTooltip;
					var titleText = __instance.TabTitle;
					string name = lastSelection.selectable.GetProperName();
					// The codex button is surprisingly expensive to calculate?
					var button = __instance.CodexEntryButton;
					if (button.isInteractable != valid) {
						button.isInteractable = valid;
						button.GetComponent<ToolTip>().SetSimpleTooltip(valid ? STRINGS.UI.
							TOOLTIPS.OPEN_CODEX_ENTRY : STRINGS.UI.TOOLTIPS.NO_CODEX_ENTRY);
					}
					if (titleText != null) {
						var resume = lastSelection.resume;
						titleText.SetTitle(name);
						if (resume != null) {
							titleText.SetSubText(resume.GetSkillsSubtitle(), "");
							titleText.SetUserEditable(true);
						} else if (lastSelection.rename != null) {
							titleText.SetSubText("", "");
							titleText.SetUserEditable(true);
						} else {
							titleText.SetSubText("", "");
							titleText.SetUserEditable(false);
						}
					}
					if (editTooltip != null) {
						string text;
						if (lastSelection.id != null)
							text = STRINGS.UI.TOOLTIPS.EDITNAME;
						else
							text = STRINGS.UI.TOOLTIPS.EDITNAMEGENERIC.Format(name);
						editTooltip.toolTip = text;
					}
				}
				return false;
			}
		}

		/// <summary>
		/// Sorts side screens by their sort key.
		/// </summary>
		private sealed class SideScreenOrderComparer : IComparer<SideScreenPair> {
			/// <summary>
			/// The singleton instance of this class.
			/// </summary>
			internal static readonly SideScreenOrderComparer Instance =
				new SideScreenOrderComparer();

			private SideScreenOrderComparer() { }

			public int Compare(SideScreenPair x, SideScreenPair y) {
				// This is a grievous violation of the Compare contract, but is necessary to
				// preserve the base game's order...
				return (x.Value <= y.Value) ? 1 : -1;
			}
		}
	}
}
