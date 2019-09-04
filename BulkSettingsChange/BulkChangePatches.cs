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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// Patches which will be applied via annotations for Bulk Settings Change.
	/// 
	/// This code took inspiration from https://github.com/0Mayall/ONIBlueprints/
	/// </summary>
	public static class BulkChangePatches {
		/// <summary>
		/// Logs when the mod is loaded.
		/// </summary>
		public static class Mod_OnLoad {
			public static void OnLoad() {
				PLibUtil.LogModInit();
			}
		}

		/// <summary>
		/// Applied to LoadBindings to inject a custom binding when the database is loaded.
		/// </summary>
		[HarmonyPatch(typeof(GameInputMapping), "LoadBindings")]
		public static class GameInputMapping_LoadBindings_Patch {
			/// <summary>
			/// Invoked before LoadBindings runs.
			/// </summary>
			private static void Prefix() {
				var key = new PLibKeyBinding(BulkChangeStrings.ToolTitle, KKeyCode.Q);
				KeyBindingManager.AddKeyBinding(key);
			}
		}

		/// <summary>
		/// Applied to OnPrefabInit to load the change settings tool into the available tool list.
		/// </summary>
		[HarmonyPatch(typeof(PlayerController), "OnPrefabInit")]
		public static class PlayerController_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			/// <param name="__instance">The current instance.</param>
			private static void Postfix(PlayerController __instance) {
				// Create list so that new tool can be appended at the end
				var interfaceTools = new List<InterfaceTool>(__instance.tools);
				var bulkChangeTool = new GameObject(typeof(BulkChangeTool).Name);
				bulkChangeTool.AddComponent<BulkChangeTool>();
				// Reparent tool to the player controller, then enable/disable to load it
				bulkChangeTool.transform.SetParent(__instance.gameObject.transform);
				bulkChangeTool.gameObject.SetActive(true);
				bulkChangeTool.gameObject.SetActive(false);
				PLibUtil.LogDebug("Created BulkChangeTool");
				// Add tool to tool list
				interfaceTools.Add(bulkChangeTool.GetComponent<InterfaceTool>());
				__instance.tools = interfaceTools.ToArray();
			}
		}

		/// <summary>
		/// Applied to ToolMenu to add the settings change icon.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "OnPrefabInit")]
		public static class ToolMenu_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			/// <param name="___icons">The icon list where the icon can be added.</param>
			private static void Postfix(ref List<Sprite> ___icons) {
				___icons.Add(SpriteRegistry.GetToolIcon());
			}
		}

		/// <summary>
		/// Applied to ToolMenu to add the settings change tool to the tool list.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "CreateBasicTools")]
		public static class ToolMenu_CreateBasicTools_Patch {
			/// <summary>
			/// Applied after CreateBasicTools runs.
			/// </summary>
			/// <param name="__instance">The basic tool list.</param>
			private static void Postfix(ref ToolMenu __instance) {
				PLibUtil.LogDebug("Adding BulkChangeTool to basic tools");
				__instance.basicTools.Add(ToolMenu.CreateToolCollection(BulkChangeStrings.
					ToolTitle, BulkChangeStrings.ToolIconName, Action.BuildMenuKeyQ, typeof(
					BulkChangeTool).Name, BulkChangeStrings.ToolDescription, false));
			}
		}

		/// <summary>
		/// Applied to Game to clean up the bulk change tool on close.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DestroyInstances")]
		public static class Game_DestroyInstances_Patch {
			/// <summary>
			/// Applied after DestroyInstances runs.
			/// </summary>
			private static void Postfix() {
				PLibUtil.LogDebug("Destroying BulkChangeTool");
				BulkChangeTool.DestroyInstance();
			}
		}
	}
}
