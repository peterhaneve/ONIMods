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

using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;

namespace PeterHan.DeselectNewMaterials {
	/// <summary>
	/// Patches for Deselect New Materials.
	/// </summary>
	public sealed class DeselectMaterialsPatches {
		/// <summary>
		/// The options for this mod.
		/// </summary>
		internal static DeselectMaterialsOptions Options { get; private set; }

		/// <summary>
		/// Loads settings when the mod starts up.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void LoadSettings() {
			var newOptions = POptions.ReadSettings<DeselectMaterialsOptions>();
			if (newOptions != null)
				Options = newOptions;
			PUtil.LogDebug("DeselectNewMaterials settings: Ignore Food = {0}".F(Options.
				IgnoreFoodBoxes));
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			PLocalization.Register();
			Options = new DeselectMaterialsOptions();
			POptions.RegisterOptions(typeof(DeselectMaterialsOptions));
			PUtil.RegisterPatchClass(typeof(DeselectMaterialsPatches));
		}

		/// <summary>
		/// Applied to TreeFilterable to deselect any new item discovered.
		/// </summary>
		[HarmonyPatch(typeof(TreeFilterable), "OnDiscover")]
		public static class TreeFilterable_OnDiscover_Patch {
			/// <summary>
			/// Applied after OnDiscover runs.
			/// </summary>
			internal static void Postfix(TreeFilterable __instance, Storage ___storage,
					Tag category_tag, Tag tag) {
				// Check the value of the storage-specific accepts/rejects new materials
				bool accept = ___storage.gameObject.GetComponentSafe<NewMaterialsSettings>()?.
					AcceptsNewMaterials ?? false;
				if (!accept &&  ___storage.storageFilters.Contains(category_tag))
					__instance.RemoveTagFromFilter(tag);
			}
		}

		/// <summary>
		/// Applied to TreeFilterable to ensure that objects with filterable storage get a
		/// NewMaterialsSettings.
		/// </summary>
		[HarmonyPatch(typeof(TreeFilterable), "OnPrefabInit")]
		public static class TreeFilterable_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(TreeFilterable __instance) {
				__instance.gameObject.AddOrGet<NewMaterialsSettings>();
			}
		}
	}
}
