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
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

using AutomationState = STRINGS.UI.AutomationState;
using UI = PeterHan.DebugNotIncluded.DebugNotIncludedStrings.UI;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Manages dialogs shown by Debug Not Included.
	/// </summary>
	internal static class ModDialogs {
		/// <summary>
		/// The current version of PLib in this mod.
		/// </summary>
		private static readonly Version OUR_VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// The name used for the new button to perform actions on a mod.
		/// </summary>
		private const string REF_MORE = "MoreModActions";

		/// <summary>
		/// The size of the button sprites in the Mods menu.
		/// </summary>
		internal static readonly Vector2 SPRITE_SIZE = new Vector2(16.0f, 16.0f);

		/// <summary>
		/// Adds the save/restore lists buttons to the bottom of the Mods screen.
		/// </summary>
		/// <param name="instance">The object hosting the mods screen.</param>
		/// <param name="bottom">The panel where the buttons should be added.</param>
		internal static void AddExtraButtons(GameObject instance, GameObject bottom) {
#if ALL_MODS_CHECKBOX
			var handler = instance.AddOrGet<AllModsHandler>();
			var cb = new PCheckBox("AllMods") {
				CheckSize = new Vector2(24.0f, 24.0f), Text = UI.MODSSCREEN.BUTTON_ALL,
				ToolTip = UI.TOOLTIPS.DNI_ALL, Margin = new RectOffset(5, 5, 0, 0)
			};
			// When clicked, enable/disable all
			if (handler != null)
				cb.OnChecked = handler.OnClick;
			handler.checkbox = cb.AddTo(bottom, 0);
			handler.UpdateCheckedState();
#endif
			// Current PLib version
			string version = PRegistry.Instance.GetLatestVersion(
				"PeterHan.PLib.Core.PLibCorePatches")?.Version?.ToString() ?? "Unknown";
			string name = ModDebugRegistry.Instance.OwnerOfAssembly(DebugNotIncludedPatches.
				RunningPLibAssembly)?.ModName ?? "Unknown";
			new PLabel("PLibVersion") {
				TextStyle = PUITuning.Fonts.UILightStyle, Text = string.Format(
				UI.MODSSCREEN.LABEL_PLIB, version ?? PVersion.VERSION), ToolTip =
				string.Format(UI.TOOLTIPS.DNI_PLIB, name), Margin = new RectOffset(5, 5, 0, 0)
			}.AddTo(bottom, 0);
		}

#pragma warning disable CS0618 // Type or member is obsolete
		/// <summary>
		/// Adds tooltips for one archived mod version, bolding it if it is active.
		/// </summary>
		/// <param name="tooltip">The location where the tooltip will be stored.</param>
		/// <param name="modInfo">The mod to query.</param>
		/// <param name="relPath">The relative path of this candidate version.</param>
		/// <param name="info">The version information about that version.</param>
		private static void AppendArchivedVersion(StringBuilder tooltip, Mod modInfo,
				string relPath, Mod.PackagedModInfo info) {
			var message = (relPath == modInfo.relative_root && modInfo.
				IsEnabledForActiveDlc()) ? UI.MODSSCREEN.LABEL_ARCHIVED_VERSION_ACTIVE :
				UI.MODSSCREEN.LABEL_ARCHIVED_VERSION_INACTIVE;
			string[] supported = (info.supportedContent ?? "").Split(',');
			int n = supported.Length, version = info.minimumSupportedBuild;
			for (int i = 0; i < n; i++)
				supported[i] = UI.MODSSCREEN.GetLocalizedName(supported[i].Trim());
			if (version <= 0)
				version = info.lastWorkingBuild;
			tooltip.AppendFormat(message, string.IsNullOrEmpty(relPath) ? UI.MODSSCREEN.
				LABEL_ARCHIVED_VERSION_DEFAULT.ToString() : relPath, supported.Join(", "),
				version, info.APIVersion);
		}

		/// <summary>
		/// Adds tooltips listing the mod's archived versions.
		/// </summary>
		/// <param name="tooltip">The location where the tooltip will be stored.</param>
		/// <param name="modInfo">The mod to query.</param>
		private static void AppendArchivedVersions(StringBuilder tooltip, Mod modInfo) {
			var info = DebugUtils.GetModInfoForFolder(modInfo, "");
			var fs = modInfo.file_source;
			tooltip.Append(UI.MODSSCREEN.LABEL_ARCHIVED_VERSIONS);
			if (info == null)
				// No mod info? Give it vanilla since forever
				info = new Mod.PackagedModInfo {
					lastWorkingBuild = 0, minimumSupportedBuild = 0,
					supportedContent = "VANILLA_ID"
				};
			AppendArchivedVersion(tooltip, modInfo, "", info);
			if (fs.Exists(DebugUtils.ARCHIVED_VERSIONS_FOLDER)) {
				var archivedItems = ListPool<FileSystemItem, ModActionDelegates>.
					Allocate();
				fs.GetTopLevelItems(archivedItems, DebugUtils.
					ARCHIVED_VERSIONS_FOLDER);
				foreach (var oldVersion in archivedItems) {
					string path = System.IO.Path.Combine(DebugUtils.
						ARCHIVED_VERSIONS_FOLDER, oldVersion.name);
					info = DebugUtils.GetModInfoForFolder(modInfo, path);
					if (info != null)
						AppendArchivedVersion(tooltip, modInfo, path, info);
				}
				archivedItems.Recycle();
			}
		}
#pragma warning restore CS0618 // Type or member is obsolete

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
					Manager.Dialog(parent, UI.NOTFIRSTDIALOG.TITLE, UI.NOTFIRSTDIALOG.TEXT,
						UI.NOTFIRSTDIALOG.CONFIRM, () => MoveToFirst(parent),
						UI.NOTFIRSTDIALOG.CANCEL, delegate { }, UI.NOTFIRSTDIALOG.IGNORE,
						DisableFirstModCheck);
			}
		}

		/// <summary>
		/// Adds buttons to a mod entry to move the mod around.
		/// </summary>
		/// <param name="displayedMod">The mod entry to modify.</param>
		/// <param name="instance">The Mods screen that is the parent of these entries.</param>
		internal static void ConfigureRowInstance(object displayedMod, ModsScreen instance) {
			if (displayedMod == null)
				throw new ArgumentNullException(nameof(displayedMod));
			var type = displayedMod.GetType();
			KButton button;
			if (!PPatchTools.TryGetFieldValue(displayedMod, "mod_index", out int index))
				throw new ArgumentException("Unable to get mod index");
			if (!PPatchTools.TryGetFieldValue(displayedMod, "rect_transform",
					out Transform transform))
				throw new ArgumentException("Unable to get rect transform");
			// "More mod actions"
			if (transform != null && transform.TryGetComponent(out HierarchyReferences refs) &&
					(button = refs.GetReference<KButton>(REF_MORE)) != null) {
				var onAction = new ModActionDelegates(button, index, instance.gameObject);
				button.onClick += onAction.TogglePopup;
				button.gameObject.AddOrGet<ToolTip>().OnToolTip = onAction.GetDescription;
			}
		}

		/// <summary>
		/// Adds the actions button to the row prefab to reconstruct it faster.
		/// </summary>
		/// <param name="rowPrefab">The prefab to modify.</param>
		internal static void ConfigureRowPrefab(GameObject rowPrefab) {
			var refs = rowPrefab.AddOrGet<HierarchyReferences>();
			var newRefs = ListPool<ElementReference, ModDebugRegistry>.Allocate();
			// Create new button
			var buttonObj = new PButton(REF_MORE) {
				SpriteSize = SPRITE_SIZE, Sprite = Assets.GetSprite("icon_gear"),
				DynamicSize = false
			}.SetKleiPinkStyle().AddTo(rowPrefab);
			// Add to references
			if (refs.references != null)
				newRefs.AddRange(refs.references);
			newRefs.Add(new ElementReference() {
				Name = REF_MORE, behaviour = buttonObj.GetComponent<KButton>()
			});
			refs.references = newRefs.ToArray();
			newRefs.Recycle();
			// Hide the Manage button
			var manage = refs.GetReference<KButton>("ManageButton");
			if (manage != null)
				manage.gameObject.SetActive(false);
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
			POptions.WriteSettings(instance);
		}

		/// <summary>
		/// Gets the formatted PLib version of the assembly, if merged or a packed copy of
		/// PLib.
		/// </summary>
		/// <param name="assembly">The assembly to check.</param>
		/// <returns>The PLib version merged or packed into that assembly formatted for display,
		/// or null if PLib is not contained in it.</returns>
		private static string GetPVersion(Assembly assembly) {
			string version = null, versionText = null;
			const string PVERSION_TYPE = nameof(PeterHan) + "." + nameof(PLib) + "." +
				nameof(PVersion);
			// Does this assembly have PLib?
			try {
				var pvType = assembly.GetType(PVERSION_TYPE, false);
				if (pvType != null) {
					version = pvType.GetFieldSafe(nameof(PVersion.VERSION), true)?.
						GetValue(null)?.ToString();
					if (version != null)
						// Red for PLib versions that are old and bad
						version = STRINGS.UI.FormatAsAutomationState(version, (new Version(
							version).Major >= OUR_VERSION.Major) ? AutomationState.Active :
							AutomationState.Standby);
				}
			} catch (TargetInvocationException) {
			} catch (TypeLoadException) {
			} catch (OverflowException) {
			} catch (FormatException) {
			} catch (ArgumentOutOfRangeException) { }
			// If version was found, format it for display
			if (version != null)
				versionText = string.Format(UI.MODSSCREEN.LABEL_PLIB, version) + (assembly.
					GetName().Name == nameof(PLib) ? UI.MODSSCREEN.LABEL_PLIB_PACKED :
					UI.MODSSCREEN.LABEL_PLIB_MERGED);
			return versionText;
		}

		/// <summary>
		/// Lists the assemblies found in a given mod.
		/// </summary>
		/// <param name="tooltip">The location where the assembly tooltip will be stored.</param>
		/// <param name="modInfo">The mod to search.</param>
		private static void ListAssemblies(StringBuilder tooltip, Mod modInfo) {
			var debugInfo = ModDebugRegistry.Instance.GetDebugInfo(modInfo);
			string plibVersionText = null;
			// For all other mods, list the assemblies for that mod
			foreach (var assembly in debugInfo.ModAssemblies) {
				var asmName = assembly.GetName();
				string plibVer = GetPVersion(assembly), asmVer = asmName.Version.ToString(),
					name = asmName.Name, fileVer = assembly.GetFileVersion();
				// If the versions match, only display one version
				if (asmVer.Equals(fileVer, StringComparison.OrdinalIgnoreCase))
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_VERSIONS_BOTH, name, asmVer);
				else
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_VERSIONS_ASSEMBLY, name,
						(fileVer == null) ? "" : string.Format(UI.MODSSCREEN.
						LABEL_VERSIONS_FILE, fileVer), asmVer);
				if (plibVer != null)
					plibVersionText = plibVer;
			}
			// PLib version, if applicable
			if (plibVersionText != null)
				tooltip.Append(plibVersionText);
			else if (debugInfo.ModAssemblies.Count < 1)
				tooltip.Append(UI.MODSSCREEN.LABEL_ASSEMBLY_NOCODE);
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
					manager.Reinsert(oldIndex, 0, false, manager);
					manager.Report(parent);
				} else
					DebugLogger.LogWarning("Unable to move Debug Not Included to top");
			}
		}

		/// <summary>
		/// Delegates for UI actions performed on a specific mod.
		/// </summary>
		private sealed class ModActionDelegates {
			/// <summary>
			/// The More Mod Actions button for this mod.
			/// </summary>
			private readonly KButton button;

			/// <summary>
			/// The mod index in the list.
			/// </summary>
			private readonly int modIndex;

			/// <summary>
			/// The mod in the list.
			/// </summary>
			private readonly Mod modInfo;

			/// <summary>
			/// The parent of the mods screen. Not the component, because caching those is bad!
			/// </summary>
			private readonly GameObject parent;

			internal ModActionDelegates(KButton button, int modIndex, GameObject parent) {
				var mods = Global.Instance.modManager?.mods;
				this.modIndex = modIndex;
				if (mods == null)
					modInfo = null;
				else
					modInfo = mods[modIndex];
				this.button = button ?? throw new ArgumentNullException("button");
				this.parent = parent ?? throw new ArgumentNullException("parent");
			}

			/// <summary>
			/// Retrieves a tooltip for mods on the mods screen to show more juicy debug info.
			/// </summary>
			/// <param name="modInfo">The mod which is being shown.</param>
			/// <returns>A tooltip for that mod in the mods screen.</returns>
			internal string GetDescription() {
				var tooltip = new StringBuilder(512);
				if (modInfo != null) {
					var isEnabled = ListPool<string, ModActionDelegates>.Allocate();
					var thisMod = DebugNotIncludedPatches.ThisMod;
					var lc = modInfo.loaded_content;
					string staticID = modInfo.staticID;
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_DESCRIPTION, modInfo.label.id,
						string.IsNullOrEmpty(staticID) ? "-" : staticID);
					// Which DLCs use it?
					foreach (var dlcID in DlcManager.RELEASE_ORDER)
						if (modInfo.IsEnabledForDlc(dlcID))
							isEnabled.Add(UI.MODSSCREEN.GetLocalizedName(dlcID));
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_DLC_ENABLE, isEnabled.Join(", "));
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_CONTENT, (lc == 0) ? "-" : lc.
						ToString());
					// About how heavy is the mod?
					var methods = modInfo.loaded_mod_data?.patched_methods;
					tooltip.AppendFormat(UI.MODSSCREEN.LABEL_PATCHES, (methods == null) ? 0 :
						methods.Count);
					AppendArchivedVersions(tooltip, modInfo);
					tooltip.AppendLine();
					if (thisMod == null || !modInfo.label.Match(thisMod.label))
						ListAssemblies(tooltip, modInfo);
					else
						tooltip.Append(UI.MODSSCREEN.LABEL_THISMOD);
					isEnabled.Recycle();
				}
				return tooltip.ToString();
			}

			/// <summary>
			/// Shows or hides the More Mod Actions popup.
			/// </summary>
			internal void TogglePopup() {
				if (parent != null && parent.TryGetComponent(out MoreModActions actions))
					actions.TogglePopup(button, modIndex);
			}
		}
	}
}
