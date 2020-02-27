/*
 * Copyright 2020 Peter Han
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

using Harmony;
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Manages dialogs shown by Debug Not Included.
	/// </summary>
	internal static class ModDialogs {
		// The names used for the new buttons to move mods.
		private const string REF_TOP = "MoveModToTop";
		private const string REF_UP = "MoveModUpTen";
		private const string REF_DOWN = "MoveModDownTen";
		private const string REF_BOTTOM = "MoveModToBottom";

		/// <summary>
		/// The size of the button sprites in the Mods menu.
		/// </summary>
		private static readonly Vector2 SPRITE_SIZE = new Vector2(16.0f, 16.0f);

		/// <summary>
		/// Adds the save/restore lists buttons to the bottom of the Mods screen.
		/// </summary>
		/// <param name="instance">The object hosting the mods screen.</param>
		/// <param name="bottom">The panel where the buttons should be added.</param>
		internal static void AddExtraButtons(GameObject instance, GameObject bottom) {
			var handler = instance.AddOrGet<AllModsHandler>();
			var cb = new PCheckBox("AllMods") {
				CheckSize = new Vector2(24.0f, 24.0f), Text = DebugNotIncludedStrings.
				BUTTON_ALL, ToolTip = DebugNotIncludedStrings.TOOLTIP_ALL, Margin =
				new RectOffset(5, 5, 0, 0)
			};
			// When clicked, enable/disable all
			if (handler != null)
				cb.OnChecked = handler.OnClick;
			handler.checkbox = cb.AddTo(bottom, 0);
			handler.UpdateCheckedState();
			// Current PLib version
			string version = PSharedData.GetData<string>("PLib.Version");
			new PLabel("PLibVersion") {
				TextStyle = PUITuning.Fonts.UILightStyle, Text = string.Format(
				DebugNotIncludedStrings.LABEL_PLIB, version ?? PVersion.VERSION), ToolTip =
				DebugNotIncludedStrings.TOOLTIP_PLIB, Margin = new RectOffset(5, 5, 0, 0)
			}.AddTo(bottom, 0);
		}

		/// <summary>
		/// Blames the mod which failed using a popup message.
		/// </summary>
		/// <param name="parent">The parent of the dialog.</param>
		internal static void BlameFailedMod(GameObject parent) {
			string blame;
			var mod = ModLoadHandler.CrashingMod;
			if (mod == null)
				blame = DebugNotIncludedStrings.LOADERR_UNKNOWN;
			else
				blame = string.Format(DebugNotIncludedStrings.LOADERR_BLAME, mod.ModName);
			// All mods will have "restart required" and/or "active during crash"
			Manager.Dialog(parent, STRINGS.UI.FRONTEND.MOD_DIALOGS.MOD_ERRORS_ON_BOOT.TITLE,
				string.Format(DebugNotIncludedStrings.DIALOG_LOADERROR, blame),
				DebugNotIncludedStrings.LOADERR_DISABLEMOD, () => DisableAndRestart(mod),
				STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.CANCEL, delegate { },
				DebugNotIncludedStrings.LOADERR_OPENLOG, DebugUtils.OpenOutputLog);
		}

		/// <summary>
		/// Checks to see if Debug Not Included is the first enabled mod. If not, asks the
		/// user to do so, later, or suppress dialog.
		/// </summary>
		/// <param name="parent">The parent of the dialog.</param>
		internal static void CheckFirstMod(GameObject parent) {
			var mods = Global.Instance.modManager?.mods;
			var target = DebugNotIncludedPatches.ThisMod;
			if (mods != null && mods.Count > 0 && target != null) {
				bool first = true;
				foreach (var mod in mods)
					if (mod.enabled) {
						first = mod.label.Match(target.label);
						break;
					}
				if (!first)
					// Display a confirmation
					Manager.Dialog(parent, DebugNotIncludedStrings.NOTFIRST_TITLE,
						DebugNotIncludedStrings.DIALOG_NOTFIRST, DebugNotIncludedStrings.
						NOTFIRST_CONFIRM, () => MoveToFirst(parent), DebugNotIncludedStrings.
						NOTFIRST_CANCEL, delegate { }, DebugNotIncludedStrings.NOTFIRST_IGNORE,
						DisableFirstModCheck);
			}
		}

		/// <summary>
		/// Adds buttons to a mod entry to move the mod around.
		/// </summary>
		/// <param name="modEntry">The mod entry to modify.</param>
		internal static void ConfigureRowInstance(Traverse modEntry) {
			int index = modEntry.GetField<int>("mod_index"), n;
			var rowInstance = modEntry.GetField<RectTransform>("rect_transform")?.gameObject;
			var manager = Global.Instance.modManager;
			var mods = manager?.mods;
			var refs = rowInstance.GetComponentSafe<HierarchyReferences>();
			if (refs != null && mods != null && index >= 0 && index < (n = mods.Count)) {
				// Update "Top" button
				var button = refs.GetReference<KButton>(REF_TOP);
				if (button != null) {
					button.onClick += () => manager.Reinsert(index, 0, manager);
					button.isInteractable = index > 0;
					button.gameObject.AddOrGet<ToolTip>().toolTip = DebugNotIncludedStrings.
						TOOLTIP_TOP;
				}
				// Update "Up 10" button
				button = refs.GetReference<KButton>(REF_UP);
				if (button != null) {
					// Actually up 9 to account for the index change after removal
					button.onClick += () => manager.Reinsert(index, Math.Max(0, index - 9),
						manager);
					button.isInteractable = index > 0;
					button.gameObject.AddOrGet<ToolTip>().toolTip = DebugNotIncludedStrings.
						TOOLTIP_UPONE;
				}
				// Update "Down 10" button
				button = refs.GetReference<KButton>(REF_DOWN);
				if (button != null) {
					button.onClick += () => manager.Reinsert(index, Math.Min(n, index + 10),
						manager);
					button.isInteractable = index < n - 1;
					button.gameObject.AddOrGet<ToolTip>().toolTip = DebugNotIncludedStrings.
						TOOLTIP_DOWNONE;
				}
				// Update "Bottom" button
				button = refs.GetReference<KButton>(REF_BOTTOM);
				if (button != null) {
					button.onClick += () => manager.Reinsert(index, n, manager);
					button.isInteractable = index < n - 1;
					button.gameObject.AddOrGet<ToolTip>().toolTip = DebugNotIncludedStrings.
						TOOLTIP_BOTTOM;
				}
				// Update the title
				refs.GetReference<ToolTip>("Description")?.SetSimpleTooltip(GetDescription(
					mods[index]));
			}
		}

		/// <summary>
		/// Adds the sorting buttons to the row prefab to reconstruct them faster.
		/// </summary>
		/// <param name="rowPrefab">The prefab to modify.</param>
		internal static void ConfigureRowPrefab(GameObject rowPrefab) {
			// Use the existing references object
			var refs = rowPrefab.AddOrGet<HierarchyReferences>();
			var newRefs = ListPool<ElementReference, ModDebugRegistry>.Allocate();
			if (refs.references != null)
				newRefs.AddRange(refs.references);
			// Add our new buttons
			newRefs.Add(MakeButton(REF_TOP, SpriteRegistry.GetTopIcon(), rowPrefab));
			newRefs.Add(MakeButton(REF_UP, Assets.GetSprite("icon_priority_up_2"), rowPrefab));
			newRefs.Add(MakeButton(REF_DOWN, Assets.GetSprite("icon_priority_down_2"),
				rowPrefab));
			newRefs.Add(MakeButton(REF_BOTTOM, SpriteRegistry.GetBottomIcon(), rowPrefab));
			refs.references = newRefs.ToArray();
			newRefs.Recycle();
		}

		/// <summary>
		/// Disables the offending mod and restarts the game.
		/// </summary>
		/// <param name="info">The mod to disable, or null to not disable any mods.</param>
		private static void DisableAndRestart(ModDebugInfo info) {
			var manager = Global.Instance.modManager;
			if (info != null) {
				manager.EnableMod(info.Mod.label, false, manager);
				manager.Save();
			}
			App.instance.Restart();
		}

		/// <summary>
		/// Disables the check for "is Debug Not Included the first mod?".
		/// </summary>
		private static void DisableFirstModCheck() {
			var instance = DebugNotIncludedOptions.Instance;
			instance.SkipFirstModCheck = true;
			POptions.WriteSettingsForAssembly(instance);
		}

		/// <summary>
		/// Retrieves a tooltip for mods on the mods screen to show more juicy debug info.
		/// </summary>
		/// <param name="modInfo">The mod which is being shown.</param>
		/// <returns>A tooltip for that mod in the mods screen.</returns>
		private static string GetDescription(Mod modInfo) {
			var debugInfo = ModDebugRegistry.Instance.GetDebugInfo(modInfo);
			var thisMod = DebugNotIncludedPatches.ThisMod;
			// Retrieve the primary assembly's version
			var tooltip = new StringBuilder(256);
			tooltip.AppendFormat(DebugNotIncludedStrings.LABEL_DESCRIPTION, modInfo.label.
				id, modInfo.description ?? "None");
			if (thisMod == null || !modInfo.label.Match(thisMod.label))
				foreach (var assembly in debugInfo.ModAssemblies) {
					var fileVer = assembly.GetFileVersion();
					var name = assembly.GetName();
					tooltip.AppendFormat(DebugNotIncludedStrings.LABEL_VERSIONS_ASSEMBLY,
						name.Name, (fileVer == null) ? "" : string.Format(
						DebugNotIncludedStrings.LABEL_VERSIONS_FILE, fileVer), name.Version);
				}
			else
				tooltip.Append("Thank you for using Debug Not Included!");
			return tooltip.ToString();
		}

		/// <summary>
		/// Creates a button prefab used to move mods up or down.
		/// </summary>
		/// <param name="name">The button's name.</param>
		/// <param name="sprite">The sprite on the button.</param>
		/// <param name="rowPrefab">The location where the button will be added.</param>
		/// <returns>The button reference.</returns>
		private static ElementReference MakeButton(string name, Sprite sprite,
				GameObject rowPrefab) {
			return new ElementReference() {
				Name = name, behaviour = new PButton(name) {
					DynamicSize = false, SpriteSize = SPRITE_SIZE, Sprite = sprite
				}.SetKleiPinkStyle().AddTo(rowPrefab).GetComponent<KButton>()
			};
		}

		/// <summary>
		/// Moves this mod to the first position and prompts to restart if necssary.
		/// </summary>
		/// <param name="parent">The parent of the dialog.</param>
		private static void MoveToFirst(GameObject parent) {
			var manager = Global.Instance.modManager;
			var mods = manager?.mods;
			var target = DebugNotIncludedPatches.ThisMod;
			if (mods != null && target != null) {
				int n = mods.Count, oldIndex = -1;
				// Search for this mod in the list
				for (int i = 0; i < n && oldIndex < 0; i++)
					if (mods[i].label.Match(target.label))
						oldIndex = i;
				if (oldIndex > 0) {
					manager.Reinsert(oldIndex, 0, manager);
					manager.Report(parent);
				} else
					DebugLogger.LogWarning("Unable to move Debug Not Included to top - uninstalled?");
			}
		}
	}
}
