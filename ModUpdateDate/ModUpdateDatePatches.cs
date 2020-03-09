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
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using Steamworks;
using System.Collections.Generic;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Patches which will be applied via annotations for Mod Update Date.
	/// </summary>
	public static class ModUpdateDatePatches {
		/// <summary>
		/// The path to our mod's data directory.
		/// </summary>
		internal static string OurModPath { get; private set; }

		/// <summary>
		/// The KMod which describes this mod.
		/// </summary>
		internal static Mod ThisMod { get; private set; }

		/// <summary>
		/// Configures the request to limit the cache time to 1 hour, then sends it.
		/// </summary>
		/// <param name="query">The UGC query to send.</param>
		/// <returns>The API call result of the query.</returns>
		internal static SteamAPICall_t ConfigureAndSend(UGCQueryHandle_t query) {
			SteamUGC.SetAllowCachedResponse(query, 3600U);
			return SteamUGC.SendQueryUGCRequest(query);
		}

		public static void OnLoad(string path) {
			OurModPath = path;
			PUtil.InitLibrary();
			POptions.RegisterOptions(typeof(ModUpdateInfo));
			PLocalization.Register();
			ModUpdateInfo.LoadSettings();
			// Find our mod
			foreach (var mod in Global.Instance.modManager?.mods)
				if (mod.label.install_path == path) {
					ThisMod = mod;
					break;
				}
		}

		/// <summary>
		/// Applied to Mod to avoid disabling this mod on crash.
		/// 
		/// If this mod gets disabled on crash, Steam might then downgrade a bunch of other
		/// mods and cause out of control follow on crashes.
		/// </summary>
		[HarmonyPatch(typeof(Mod), "Crash")]
		public static class Mod_Crash_Patch {
			/// <summary>
			/// Applied before Crash runs.
			/// </summary>
			internal static bool Prefix(Mod __instance) {
				return ThisMod == null || !__instance.label.Match(ThisMod.label);
			}
		}

		/// <summary>
		/// Applied to ModsScreen to adjust tool tips for the subscription button to have
		/// the dates.
		/// </summary>
		[HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
		[HarmonyPriority(Priority.First)]
		public static class ModsScreen_BuildDisplay_Patch {
			/// <summary>
			/// Applied after BuildDisplay runs.
			/// </summary>
			internal static void Postfix(object ___displayedMods) {
				if (___displayedMods != null)
					// Must cast the type because ModsScreen.DisplayedMod is private
					foreach (var displayedMod in (System.Collections.IEnumerable)___displayedMods)
						ModUpdateHandler.AddModUpdateButton(Traverse.Create(displayedMod));
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to make the update bypass the cache.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "Update")]
		public static class SteamUGCService_Update_Patch {
			/// <summary>
			/// Applied after Update runs.
			/// </summary>
			internal static void Postfix() {
				ModUpdateDetails.ScrubConfig();
			}

			/// <summary>
			/// Transpiles Update to make the request max caching interval 1 hour.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var argType = typeof(UGCQueryHandle_t);
				return PPatchTools.ReplaceMethodCall(method, typeof(SteamUGC).GetMethodSafe(
					"SendQueryUGCRequest", true, argType), typeof(ModUpdateDatePatches).
					GetMethodSafe(nameof(ConfigureAndSend), true, argType));
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to get detailed mod info when it is requested.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "OnSteamUGCQueryDetailsCompleted")]
		public static class SteamUGCService_OnSteamUGCQueryDetailsCompleted_Patch {
			/// <summary>
			/// Applied after OnSteamUGCQueryDetailsCompleted runs.
			/// </summary>
			internal static void Postfix(HashSet<SteamUGCDetails_t> ___publishes) {
				if (___publishes != null)
					ModUpdateDetails.OnInstalledUpdate(___publishes);
			}
		}
	}
}
