﻿/*
 * Copyright 2024 Peter Han
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
		/// A faster and lower-allocation version of CodexCache.FormatLinkID.
		/// </summary>
		/// <param name="id">The ID to link.</param>
		/// <param name="find">If specified, replaces the specified text if found in the link...</param>
		/// <param name="replace">... with the value of the replace parameter.</param>
		/// <returns>The link ID.</returns>
		private static string FormatLinkID(string id, string find = null,
				string replace = null) {
			var text = CACHED_BUILDER;
			text.Clear();
			text.Append(id);
			text.Replace("_", "");
			int n = text.Length;
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
		/// Sorts the side screens using their sort key.
		/// </summary>
		/// <param name="ds">The details screen to sort.</param>
		/// <param name="target">The selected target.</param>
		/// <param name="force">Forces the side screen to be shown even if no config.</param>
		private static void SortSideScreens(DetailsScreen ds, GameObject target,
				bool force) {
			var sideScreens = ds.sideScreens;
			var sortedScreens = ds.sortedSideScreens;
			int n;
			sortedScreens.Clear();
			if (sideScreens != null && sideScreens.Count > 0) {
				int currentOrder = 0;
				var currentScreen = ds.currentSideScreen;
				var noConfig = ds.noConfigSideScreen;
				var dss = ds.sideScreen;
				bool anyScreens = false;
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
					GameObject instGO;
					if (prefab.IsValidForTarget(target)) {
						if (inst == null) {
							inst = Util.KInstantiateUI<SideScreenContent>(prefab.gameObject,
								ds.sideScreenConfigContentBody);
							screen.screenInstance = inst;
						}
						int sortOrder = inst.GetSideScreenSortOrder();
						if (!dss.activeInHierarchy)
							dss.SetActive(true);
						inst.SetTarget(target);
						inst.Show();
						sortedScreens.Add(new SideScreenPair(inst.gameObject, sortOrder));
						if (currentScreen == null || sortOrder > currentOrder) {
							ds.currentSideScreen = currentScreen = inst;
							ds.sideScreenTitleLabel.SetText(inst.GetTitle());
						}
						anyScreens = true;
					} else if (inst != null && (instGO = inst.gameObject).activeSelf) {
						instGO.SetActive(false);
						// If the current screen was just hidden, allow another one to
						// take its place
						if (inst == currentScreen)
							currentScreen = null;
					}
				}
				if (anyScreens)
					noConfig.SetActive(false);
				else if (force) {
					noConfig.SetActive(true);
					ds.sideScreenTitleLabel.SetText(STRINGS.UI.UISIDESCREENS.NOCONFIG.TITLE);
					dss.SetActive(true);
				} else {
					noConfig.SetActive(false);
					dss.SetActive(false);
				}
			}
			sortedScreens.Sort(SideScreenOrderComparer.INSTANCE);
			n = sortedScreens.Count;
			for (int i = 0; i < n; i++)
				sortedScreens[i].Key.transform.SetSiblingIndex(i);
		}

		/// <summary>
		/// Updates the title and codex buttons.
		/// </summary>
		/// <param name="screen">The details screen to be updated.</param>
		private static void UpdateTitle(DetailsScreen screen) {
			ref var lastSelection = ref instance.lastSelection;
			string codexLink = lastSelection.codexLink;
			bool valid = !string.IsNullOrEmpty(codexLink);
			var commandModule = lastSelection.rocketCommand;
			var rocketDoor = lastSelection.rocketDoor;
			var editTooltip = lastSelection.editTooltip;
			var titleText = screen.TabTitle;
			string name = lastSelection.selectable.GetProperName();
			// The codex button is surprisingly expensive to calculate?
			var button = screen.CodexEntryButton;
			if (button.isInteractable != valid) {
				button.isInteractable = valid;
				if (button.TryGetComponent(out ToolTip tooltip))
					tooltip.SetSimpleTooltip(valid ? STRINGS.UI.TOOLTIPS.OPEN_CODEX_ENTRY :
						STRINGS.UI.TOOLTIPS.NO_CODEX_ENTRY);
			}
			if (titleText != null) {
				var resume = lastSelection.resume;
				titleText.SetTitle(name);
				if (resume != null) {
					titleText.SetSubText(resume.GetSkillsSubtitle());
					titleText.SetUserEditable(true);
				} else if (lastSelection.rename != null) {
					titleText.SetSubText("");
					titleText.SetUserEditable(true);
				} else if (commandModule != null) {
					var sm = SpacecraftManager.instance;
					if (sm != null)
						titleText.SetTitle(sm.GetSpacecraftFromLaunchConditionManager(
							lastSelection.conditions).GetRocketName());
					else
						titleText.SetTitle("");
					titleText.SetSubText(name);
					titleText.SetUserEditable(true);
				} else if (rocketDoor != null)
					screen.TrySetRocketTitle(rocketDoor);
				else {
					titleText.SetSubText("");
					titleText.SetUserEditable(false);
				}
			}
			if (editTooltip != null) {
				string text;
				if (lastSelection.id != null)
					text = STRINGS.UI.TOOLTIPS.EDITNAME;
				else if (commandModule != null || rocketDoor != null)
					text = STRINGS.UI.TOOLTIPS.EDITNAMEROCKET;
				else
					text = STRINGS.UI.TOOLTIPS.EDITNAMEGENERIC.Format(name);
				editTooltip.toolTip = text;
			}
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
		private readonly struct LastSelectionDetails {
			internal readonly string codexLink;

			internal readonly bool forceSideScreen;

			internal readonly LaunchConditionManager conditions;

			internal readonly ToolTip editTooltip;
			
			internal readonly MinionIdentity id;

			internal readonly MinionResume resume;

			internal readonly UserNameable rename;

			internal readonly CommandModule rocketCommand;

			internal readonly ClustercraftExteriorDoor rocketDoor;

			internal readonly KSelectable selectable;

			internal LastSelectionDetails(GameObject go, CellSelectionObject cso,
					DetailsScreen instance) {
				var title = instance.TabTitle;
				string rawLink = GetCodexLink(go, cso);
				if (rawLink != null && (CodexCache.entries.ContainsKey(rawLink) || CodexCache.
						FindSubEntry(rawLink) != null))
					codexLink = rawLink;
				else
					codexLink = "";
				go.TryGetComponent(out selectable);
				go.TryGetComponent(out rename);
				go.TryGetComponent(out resume);
				go.TryGetComponent(out rocketCommand);
				go.TryGetComponent(out conditions);
				go.TryGetComponent(out rocketDoor);
				forceSideScreen = go.TryGetComponent(out id) || (go.TryGetComponent(
					out Reconstructable reconstructable) && reconstructable.
					AllowReconstruct) || go.TryGetComponent(out BuildingFacade _);
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(DetailsScreen __instance, string newName) {
				var inst = instance;
				__instance.isEditing = false;
				if (!string.IsNullOrEmpty(newName) && inst != null) {
					ref var lastSelection = ref inst.lastSelection;
					var editTooltip = lastSelection.editTooltip;
					var commandModule = lastSelection.rocketCommand;
					var rocketDoor = lastSelection.rocketDoor;
					var id = lastSelection.id;
					var rename = lastSelection.rename;
					var sm = SpacecraftManager.instance;
					if (id != null)
						id.SetName(newName);
					else if (commandModule != null && sm != null)
						sm.GetSpacecraftFromLaunchConditionManager(lastSelection.conditions).
							SetRocketName(newName);
					else if (rocketDoor != null && rocketDoor.GetTargetWorld().TryGetComponent(
							out UserNameable worldName))
						worldName.SetName(newName);
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
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.CodexEntryButton_OnClick))]
		internal static class OpenCodexEntry_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before OpenCodexEntry runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(DetailsScreen __instance, GameObject go) {
				var screens = __instance.screens;
				var oldTarget = __instance.target;
				var inst = instance;
				bool force;
				if (screens != null) {
					__instance.target = go;
					if (go.TryGetComponent(out CellSelectionObject cso))
						cso.OnObjectSelected(null);
					if ((oldTarget == null || go != oldTarget) && inst != null) {
						inst.lastSelection = new LastSelectionDetails(go, cso, __instance);
						force = inst.lastSelection.forceSideScreen;
					} else
						force = true;
					UpdateTitle(__instance);
					__instance.tabHeader.RefreshTabDisplayForTarget(go);
					SortSideScreens(__instance, go, force);
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to DetailsScreen to speed up setting the title and break a hard
		/// MinionIdentity dependency (hey Romen!).
		/// </summary>
		[HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.UpdateTitle))]
		internal static class SetTitle_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before UpdateTitle runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(DetailsScreen __instance) {
				if (instance != null)
					UpdateTitle(__instance);
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
			internal static readonly SideScreenOrderComparer INSTANCE =
				new SideScreenOrderComparer();

			private SideScreenOrderComparer() { }

			public int Compare(SideScreenPair x, SideScreenPair y) {
				// This is a grievous violation of the Compare contract, but is necessary to
				// preserve the base game's order...
				return x.Value <= y.Value ? 1 : -1;
			}
		}
	}
}
