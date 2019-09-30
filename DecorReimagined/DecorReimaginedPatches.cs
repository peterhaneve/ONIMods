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
using PeterHan.PLib.Options;
using System;

namespace PeterHan.DecorRework {
	/// <summary>
	/// Patches which will be applied via annotations for Decor Reimagined.
	/// </summary>
	public static class DecorReimaginedPatches {
		/// <summary>
		/// The options for Decor Reimagined.
		/// </summary>
		internal static DecorReimaginedOptions Options { get; private set; }

		public static void OnLoad() {
			PUtil.InitLibrary();
			Options = new DecorReimaginedOptions();
			POptions.RegisterOptions(typeof(DecorReimaginedOptions));
			PUtil.RegisterPostload(DecorTuning.TuneBuildings);
		}

		/// <summary>
		/// Applied to Db to apply the new decor levels.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				DecorTuning.InitEffects();
				PUtil.LogDebug("Initialized decor effects");
			}
		}

		/// <summary>
		/// Applied to DecorProvider to properly attribute decor sources.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), "GetDecorForCell")]
		public static class DecorProvider_GetDecorForCell_Patch {
			/// <summary>
			/// Applied before GetDecorForCell runs.
			/// </summary>
			internal static bool Prefix(ref DecorProvider __instance, int cell,
					out float __result) {
				bool cont = true;
				var inst = DecorCellManager.Instance;
				if (inst != null) {
					__result = inst.GetDecorProvided(cell, __instance);
					cont = false;
				} else
					__result = 0.0f;
				return cont;
			}
		}

		/// <summary>
		/// Applied to DecorProvider to clean up obsolete instances on destroy.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), "OnCleanUp")]
		public static class DecorProvider_OnCleanUp_Patch {
			/// <summary>
			/// Applied after OnCleanUp runs.
			/// </summary>
			internal static void Postfix(ref DecorProvider __instance) {
				DecorCellManager.Instance?.DestroyDecor(__instance);
			}
		}

		/// <summary>
		/// Applied to DecorProvider to refresh it when operational status changes.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), "OnSpawn")]
		public static class DecorProvider_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(ref DecorProvider __instance) {
				DecorCellManager.Instance?.RegisterDecor(__instance);
			}
		}

		/// <summary>
		/// Applied to DecorProvider to properly handle broken/disabled building decor.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), "Refresh")]
		public static class DecorProvider_Refresh_Patch {
			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix(ref DecorProvider __instance) {
				var obj = __instance.gameObject;
				var inst = DecorCellManager.Instance;
				bool cont = true;
				if (obj != null && inst != null) {
					cont = false;
					inst.RefreshDecor(__instance);
				}
				return cont;
			}
		}

		/// <summary>
		/// Applied to Game to clean up the decor manager on close.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DestroyInstances")]
		public static class Game_DestroyInstances_Patch {
			/// <summary>
			/// Applied after DestroyInstances runs.
			/// </summary>
			internal static void Postfix() {
				PUtil.LogDebug("Destroying DecorCellManager");
				DecorCellManager.DestroyInstance();
			}
		}

		/// <summary>
		/// Applied to Game to set up the decor manager on start.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix() {
				PUtil.LogDebug("Creating DecorCellManager");
				DecorCellManager.CreateInstance();
			}
		}

		/// <summary>
		/// Applied to LegacyModMain to alter building decor.
		/// </summary>
		[HarmonyPatch(typeof(LegacyModMain), "LoadBuildings")]
		public static class LegacyModMain_LoadBuildings_Patch {
			/// <summary>
			/// Applied after LoadBuildings runs.
			/// </summary>
			internal static void Postfix() {
				// Settings need to be read at this time
				Options = POptions.ReadSettings<DecorReimaginedOptions>() ??
					new DecorReimaginedOptions();
				PUtil.LogDebug("DecorReimaginedOptions settings: Hard Mode = {0}".F(Options.
					HardMode));
				PUtil.LogDebug("Loading decor database");
				DecorTuning.ApplyDatabase(Options);
			}
		}
	}
}
