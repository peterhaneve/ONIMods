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

using HarmonyLib;
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;

namespace PeterHan.SweepByType {
	/// <summary>
	/// Patches which will be applied via annotations for Sweep By Type.
	/// </summary>
	public sealed class SweepByTypePatches : KMod.UserMod2 {
		/// <summary>
		/// The current mod options, set on load.
		/// </summary>
		internal static SweepByTypeOptions Options { get; private set; }

		/// <summary>
		/// Triggers the default Sweep (all) tool if enabled.
		/// </summary>
		internal static PAction defaultSweepAction;

		/// <summary>
		/// Adds the filtered sweep icon to the list of sprites.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AddToolSprite() {
			Assets.Sprites.Add(SweepByTypeStrings.TOOL_ICON_NAME, SpriteRegistry.
				GetToolIcon());
		}
		
		/// <summary>
		/// Generates a new sweep tool collection that contains the new filtered sweep tool
		/// with an alternate mode for the original version.
		/// </summary>
		/// <param name="title">The title to assign the tool.</param>
		/// <returns>A replacement tool collection with filtered and base tools.</returns>
		private static ToolMenu.ToolCollection CreateFilteredSweepTool(string title) {
			if (!Enum.TryParse(nameof(Action.Clear), out Action clearAction))
				clearAction = Action.Clear;
			return ToolMenu.CreateToolCollection(title, SweepByTypeStrings.TOOL_ICON_NAME,
				clearAction, nameof(FilteredClearTool), STRINGS.UI.TOOLTIPS.CLEARBUTTON,
				false);
		}

		/// <summary>
		/// Cleans up the filtered sweep tool on close.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void DestroyTool() {
			PUtil.LogDebug("Destroying FilteredClearTool");
			FilteredClearTool.DestroyInstance();
		}

		/// <summary>
		/// Determines the Action to use for activating the classic sweep tool, if a key is
		/// bound to it.
		/// </summary>
		/// <returns>The action to use, or PAction.MaxAction if no key is bound.</returns>
		private static Action GetDefaultSweepAction() {
			var ds = defaultSweepAction;
			Action result = PAction.MaxAction, candidate = ds.GetKAction();
			// Klei loves changing enums
			if (!Enum.TryParse(nameof(KKeyCode.None), out KKeyCode noKey))
				noKey = KKeyCode.None;
			if (!Enum.TryParse(nameof(GamepadButton.NumButtons), out GamepadButton noButton))
				noButton = GamepadButton.NumButtons;
			if (ds != null)
				foreach (var entry in GameInputMapping.KeyBindings)
					if (entry.mAction == candidate && (entry.mKeyCode != noKey || entry.
							mButton != noButton)) {
						result = candidate;
						break;
					}
			return result;
		}

		/// <summary>
		/// Installs the Filtered Sweep tool in the specified collection.
		/// </summary>
		/// <param name="tools">The tool menu in which to show the tool.</param>
		private static void InstallSweepTool(IList<ToolMenu.ToolCollection> tools) {
			var defaultAction = GetDefaultSweepAction();
			int n = tools.Count;
			bool replaced = false;
			for (int i = 0; i < n && !replaced; i++) {
				var tool = tools[i];
				// Replace by icon since it is a top level member
				if (tool.icon == "icon_action_store") {
					PUtil.LogDebug("Replacing sweep tool {0:D} with filtered sweep".F(i));
					if (defaultAction == PAction.MaxAction)
						tools[i] = CreateFilteredSweepTool(STRINGS.UI.TOOLS.MARKFORSTORAGE.
							NAME);
					else {
						tools.Insert(i, CreateFilteredSweepTool(SweepByTypeStrings.
							TOOLBAR_TITLE_FILTERED));
						SwapHotkey(tool, defaultAction);
					}
					replaced = true;
				}
			}
			// If no tool match found, log a warning
			if (!replaced)
				PUtil.LogWarning("Could not install filtered sweep tool!");
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(SweepByTypeOptions));
			new PLocalization().Register();
			Options = null;
			new PPatchManager(harmony).RegisterPatchClass(typeof(SweepByTypePatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			defaultSweepAction = new PActionManager().CreateAction(SweepByTypeStrings.
				DEFAULT_SWEEP_KEY, SweepByTypeStrings.DEFAULT_SWEEP_TITLE);
		}

		/// <summary>
		/// Swaps the hotkey of all tools in the collection to a new one.
		/// </summary>
		/// <param name="collection">The collection to modify.</param>
		/// <param name="newKey">The hotkey to assign.</param>
		private static void SwapHotkey(ToolMenu.ToolCollection collection, Action newKey) {
			var tools = collection.tools;
			int n = tools.Count;
			for (int i = 0; i < n; i++)
				tools[i].hotkey = newKey;
		}

		/// <summary>
		/// Applied to SaveGame to add a list of saved types for sweeping.
		/// </summary>
		[HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
		public static class SaveGame_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(SaveGame __instance) {
				__instance.gameObject.AddOrGet<SavedTypeSelections>();
			}
		}

		/// <summary>
		/// Applied to PlayerController to load the filtered sweep tool into the available
		/// tool list.
		/// </summary>
		[HarmonyPatch(typeof(PlayerController), "OnPrefabInit")]
		public static class PlayerController_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			/// <param name="__instance">The current instance.</param>
			internal static void Postfix(PlayerController __instance) {
				Options = POptions.ReadSettings<SweepByTypeOptions>();
				PToolMode.RegisterTool<FilteredClearTool>(__instance);
				PUtil.LogDebug("Created FilteredClearTool with icons " + ((Options?.
					DisableIcons == true) ? "disabled" : "enabled"));
			}
		}

		/// <summary>
		/// Applied to ToolMenu to replace the sweep tool with the filtered sweep tool!
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "CreateBasicTools")]
		public static class ToolMenu_CreateBasicTools_Patch {
			/// <summary>
			/// Applied after CreateBasicTools runs.
			/// </summary>
			internal static void Postfix(ToolMenu __instance) {
				InstallSweepTool(__instance.basicTools);
			}
		}
	}
}
