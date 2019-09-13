/*
 * Copyright 2019 Peter Han
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
using PeterHan.PLib;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.SweepByType {
	/// <summary>
	/// Patches which will be applied via annotations for Sweep By Type.
	/// </summary>
	public static class SweepByTypePatches {
		public static void OnLoad() {
			PUtil.LogModInit();
		}

		/// <summary>
		/// Handles localization by registering for translation.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			/// <summary>
			/// Applied before Initialize runs.
			/// </summary>
			internal static void Prefix() {
#if DEBUG
				ModUtil.RegisterForTranslation(typeof(SweepByTypeStrings));
#else
				Localization.RegisterForTranslation(typeof(SweepByTypeStrings));
#endif
			}
		}

		/// <summary>
		/// Applied to Game to clean up the filtered sweep tool on close.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DestroyInstances")]
		public static class Game_DestroyInstances_Patch {
			/// <summary>
			/// Applied after DestroyInstances runs.
			/// </summary>
			internal static void Postfix() {
				PUtil.LogDebug("Destroying FilteredClearTool");
				FilteredClearTool.DestroyInstance();
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
				// Create list so that new tool can be appended at the end
				var interfaceTools = new List<InterfaceTool>(__instance.tools);
				var filteredSweepTool = new GameObject(typeof(FilteredClearTool).Name);
				filteredSweepTool.AddComponent<FilteredClearTool>();
				// Reparent tool to the player controller, then enable/disable to load it
				filteredSweepTool.transform.SetParent(__instance.gameObject.transform);
				filteredSweepTool.gameObject.SetActive(true);
				filteredSweepTool.gameObject.SetActive(false);
				PUtil.LogDebug("Created FilteredClearTool");
				// Add tool to tool list
				interfaceTools.Add(filteredSweepTool.GetComponent<InterfaceTool>());
				__instance.tools = interfaceTools.ToArray();
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
			internal static void Postfix(ref ToolMenu __instance) {
				var filteredSweep = ToolMenu.CreateToolCollection(STRINGS.UI.TOOLS.
					MARKFORSTORAGE.NAME, "icon_action_store", Action.Clear,
					"FilteredClearTool", STRINGS.UI.TOOLTIPS.CLEARBUTTON, false);
				var tools = __instance.basicTools;
				int n = tools.Count;
				bool replaced = false;
				for (int i = 0; i < n && !replaced; i++)
					// Replace by icon since it is a top level member
					if (tools[i].icon == filteredSweep.icon) {
						PUtil.LogDebug("Replacing sweep tool {0:D} with filtered sweep".F(i));
						tools[i] = filteredSweep;
						replaced = true;
					}
				// If no tool match found, log a warning
				if (!replaced)
					PUtil.LogWarning("Could not install filtered sweep tool!");
			}
		}
	}
}
