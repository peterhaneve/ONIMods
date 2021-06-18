/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using UnityEngine;

using SearchFilter = SandboxToolParameterMenu.SelectorValue.SearchFilter;

namespace PeterHan.SandboxTools {
	/// <summary>
	/// Patches which will be applied via annotations for Sandbox Tools.
	/// </summary>
	public sealed class SandboxToolsPatches : KMod.UserMod2 {
		/// <summary>
		/// Whether multi select is available for filtering on Destroy.
		/// </summary>
		public static bool AdvancedFilterEnabled { get; private set; }

		/// <summary>
		/// Adds more items to the spawner list, including geysers, artifacts, and POI items.
		/// </summary>
		/// <param name="instance">The sandbox tool menu to modify.</param>
		private static void AddToSpawnerMenu(SandboxToolParameterMenu instance) {
			// Transpiling it is possible (and a bit faster) but way more brittle
			var selector = instance.entitySelector;
			var filters = ListPool<SearchFilter, SandboxToolParameterMenu>.Allocate();
			filters.AddRange(selector.filters);
			// POI Props
			filters.Add(new SearchFilter(SandboxToolsStrings.FILTER_POIPROPS,
				(entity) => {
					var prefab = entity as KPrefabID;
					bool ok = prefab != null;
					if (ok) {
						string name = prefab.PrefabTag.Name;
						// Include anti-entropy thermo nullifier and neural vacillator
						// Vacillator's ID is private, we have to make do
						ok = (name.StartsWith("Prop") && name.Length > 4 && char.IsUpper(
							name, 4)) || name == MassiveHeatSinkConfig.ID ||
							name == "GeneShuffler";
					}
					return ok;
				}, null, Def.GetUISprite(Assets.GetPrefab("PropLadder"))));
			// Artifacts
			filters.Add(new SearchFilter(SandboxToolsStrings.FILTER_ARTIFACTS,
				(entity) => {
					var prefab = entity as KPrefabID;
					bool ok = prefab != null;
					if (ok)
						ok = prefab.PrefabTag.Name.StartsWith("artifact_");
					return ok;
				}, null, Def.GetUISprite(Assets.GetPrefab("artifact_eggrock"))));
			// Geysers
			filters.Add(new SearchFilter(SandboxToolsStrings.FILTER_GEYSERS,
				(entity) => {
					var prefab = entity as KPrefabID;
					return prefab != null && (prefab.GetComponent<Geyser>() != null || prefab.
						PrefabTag.Name == "OilWell");
				}, null, Def.GetUISprite(Assets.GetPrefab("GeyserGeneric_slush_water"))));
			// Add matching assets
			var options = ListPool<object, SandboxToolParameterMenu>.Allocate();
			foreach (var prefab in Assets.Prefabs)
				foreach (var filter in filters)
					if (filter.condition(prefab)) {
						options.Add(prefab);
						break;
					}
#if DEBUG
			PUtil.LogDebug("Added {0:D} options to spawn menu".F(options.Count));
#endif
			selector.options = options.ToArray();
			selector.filters = filters.ToArray();
			options.Recycle();
			filters.Recycle();
		}

		/// <summary>
		/// A wrapper method used on BuildingDef.Build to use the right material for some
		/// POI buildings.
		/// </summary>
		private static GameObject BuildFixedMaterials(BuildingDef def, int cell,
				Orientation orient, Storage storage, IList<Tag> elements, float temperature,
				bool sound, float timeBuilt) {
			if (def != null && def.PrefabID == MassiveHeatSinkConfig.ID && elements != null) {
				// Special case the AETN to iron (it uses niobium otherwise)
				elements.Clear();
				elements.Add(ElementLoader.FindElementByHash(SimHashes.Iron).tag);
			}
			return def.Build(cell, orient, storage, elements, temperature, sound, timeBuilt);
		}

		/// <summary>
		/// A wrapper method used on BuildingDef.Build to avoid melting buildings built at cold
		/// temperatures.
		/// </summary>
		private static GameObject BuildAtTemp(BuildingDef def, int cell, Orientation orient,
				Storage storage, IList<Tag> elements, float temperature, bool sound,
				float timeBuilt) {
			if (elements != null && elements.Count > 0) {
				// Lower temperature to at least the element's melt point - 1 K
				var pe = ElementLoader.GetElement(elements[0]);
				if (pe != null)
					temperature = Math.Min(temperature, Math.Max(1.0f, pe.highTemp - 1.0f));
			}
			return def.Build(cell, orient, storage, elements, temperature, sound, timeBuilt);
		}

		[PLibMethod(RunAt.BeforeDbInit)]
		internal static void LoadImages() {
			var icon = SpriteRegistry.GetToolIcon();
			Assets.Sprites.Add(icon.name, icon);
		}

		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
			PUtil.LogDebug("Destroying FilteredDestroyTool");
			DestroyParameterMenu.DestroyInstance();
		}

		[PLibMethod(RunAt.AfterModsLoad)]
		internal static void TestAdvancedFilter() {
			AdvancedFilterEnabled = PPatchTools.GetTypeSafe(
				"AdvancedFilterMenu.AdvancedFiltrationAssets", "AdvancedFilterMenu") != null;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PLocalization().Register();
			new PPatchManager(harmony).RegisterPatchClass(typeof(SandboxToolsPatches));
		}

		/// <summary>
		/// Applied to BuildTool to build items at the correct temperature.
		/// </summary>
		[HarmonyPatch(typeof(BuildTool), "TryBuild")]
		public static class BuildTool_TryBuild_Patch {
			/// <summary>
			/// Transpiles TryBuild to place buildings at the right temperature in sandbox.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(BuildingDef).GetMethodSafe(
					nameof(BuildingDef.Build), false, PPatchTools.AnyArguments),
					typeof(SandboxToolsPatches).GetMethodSafe(nameof(BuildAtTemp), true,
					PPatchTools.AnyArguments));
			}
		}

		/// <summary>
		/// Applied to PlayerController to load the filtered destroy tool into the available
		/// tool list.
		/// </summary>
		[HarmonyPatch(typeof(PlayerController), "OnPrefabInit")]
		public static class PlayerController_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(PlayerController __instance) {
				PToolMode.RegisterTool<FilteredDestroyTool>(__instance);
				PUtil.LogDebug("Created FilteredDestroyTool");
			}
		}

		/// <summary>
		/// Applied to SandboxSpawnerTool to place buildings with the right material in some
		/// special cases.
		/// </summary>
		[HarmonyPatch(typeof(SandboxSpawnerTool), "Place")]
		public static class SandboxSpawnerTool_Place_Patch {
			/// <summary>
			/// Transpiles Place to properly place special buildings.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(BuildingDef).GetMethodSafe(
					nameof(BuildingDef.Build), false, PPatchTools.AnyArguments),
					typeof(SandboxToolsPatches).GetMethodSafe(nameof(BuildFixedMaterials),
					true, PPatchTools.AnyArguments));
			}
		}

		/// <summary>
		/// Applied to SandboxToolParameterMenu to add more items to the spawnable menu.
		/// </summary>
		[HarmonyPatch(typeof(SandboxToolParameterMenu), "ConfigureEntitySelector")]
		public static class SandboxToolParameterMenu_ConfigureEntitySelector_Patch {
			/// <summary>
			/// Applied after ConfigureEntitySelector runs.
			/// </summary>
			internal static void Postfix(SandboxToolParameterMenu __instance) {
				AddToSpawnerMenu(__instance);
			}
		}

		/// <summary>
		/// Applied to ToolMenu to replace the destroy tool with the filtered destroy tool.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "CreateSandBoxTools")]
		public static class ToolMenu_CreateSandBoxTools_Patch {
			/// <summary>
			/// Applied after CreateSandBoxTools runs.
			/// </summary>
			internal static void Postfix(ToolMenu __instance) {
				if (!Enum.TryParse("SandboxDestroy", out Action destroyAction))
					destroyAction = Action.SandboxDestroy;
				var filteredDestroy = ToolMenu.CreateToolCollection(SandboxToolsStrings.
					TOOL_DESTROY_NAME, SandboxToolsStrings.TOOL_DESTROY_ICON, destroyAction,
					nameof(FilteredDestroyTool), SandboxToolsStrings.TOOL_DESTROY_TOOLTIP,
					false);
				var tools = __instance.sandboxTools;
				int n = tools.Count;
				bool replaced = false;
				for (int i = 0; i < n && !replaced; i++)
					// Replace by icon since it is a top level member
					if (tools[i].icon == "destroy") {
						PUtil.LogDebug("Replacing destroy tool {0:D} with filtered destroy".
							F(i));
						tools[i] = filteredDestroy;
						replaced = true;
					}
				// If no tool match found, log a warning
				if (!replaced)
					PUtil.LogWarning("Could not install filtered destroy tool!");
			}
		}

		/// <summary>
		/// Applied to ToolMenu to add the filtered destroy parameter menu for Advanced
		/// Filter Menu compatibility.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "OnPrefabInit")]
		public static class ToolMenu_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix() {
				DestroyParameterMenu.CreateInstance();
			}
		}
	}
}
