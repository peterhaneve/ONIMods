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
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Patches which will be applied via annotations for Mod Updater.
	/// </summary>
	public static class ModUpdateDatePatches {
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
			PUtil.InitLibrary();
			POptions.RegisterOptions(typeof(ModUpdateInfo));
			LocString.CreateLocStringKeys(typeof(ModUpdateDateStrings.UI));
			PLocalization.Register();
			// Try to read the backup config first
			string backupPath = ExtensionMethods.BackupConfigPath;
			if (File.Exists(backupPath))
				try {
					// Copy and overwrite our config if possible
					File.Copy(backupPath, ExtensionMethods.ConfigPath, true);
					File.Delete(backupPath);
					PUtil.LogDebug("Restored configuration settings after self-update");
				} catch (IOException) {
					PUtil.LogWarning("Unable to restore configuration for Mod Updater");
				} catch (System.UnauthorizedAccessException) {
					PUtil.LogWarning("Unable to restore configuration for Mod Updater");
				}
			ModUpdateInfo.LoadSettings();
			// Find our mod
			foreach (var mod in Global.Instance.modManager?.mods)
				if (mod.GetModBasePath() == path) {
					ThisMod = mod;
					break;
				}
		}

		/// <summary>
		/// Handles a mod crash and bypasses disabling the mod if it is this mod.
		/// </summary>
		private static bool OnModCrash(Mod __instance) {
			return ThisMod == null || !__instance.label.Match(ThisMod.label);
		}

		/// <summary>
		/// Invoked by the game before our patches, so we get a chance to patch Mod.Crash.
		/// </summary>
		public static void PrePatch(HarmonyInstance instance) {
			var method = typeof(Mod).GetMethodSafe("Crash", false);
			if (method == null)
				method = typeof(Mod).GetMethodSafe("SetCrashed", false);
			if (method != null)
				instance.Patch(method, prefix: new HarmonyMethod(typeof(ModUpdateDatePatches),
					nameof(OnModCrash)));
		}

		/// <summary>
		/// Updates the number of outdated mods on the main menu.
		/// </summary>
		private static void UpdateMainMenu() {
			MainMenuWarning.Instance?.UpdateText();
		}

		/// <summary>
		/// Applied to MainMenu to initialize the main menu update warning.
		/// </summary>
		[HarmonyPatch(typeof(MainMenu), "OnPrefabInit")]
		public static class MainMenu_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(MainMenu __instance) {
				if (ModUpdateInfo.Settings?.ShowMainMenuWarning == true)
					__instance.gameObject.AddOrGet<MainMenuWarning>();
			}
		}

		/// <summary>
		/// Applied to KMod.Manager to fix mods being overwritten by Klei if the content in
		/// the outdated mod does not match the content in the updated mod.
		/// </summary>
		[HarmonyPatch(typeof(Manager), "Subscribe")]
		public static class Manager_Subscribe_Patch {
			/// <summary>
			/// Transpiles Subscribe to insert a call to SuppressContentChanged after the
			/// comparison.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				bool gac = false, notCheck = false;
				// get_available_content
				var targetMethod = typeof(Mod).GetPropertySafe<Content>(nameof(Mod.
					available_content), false)?.GetGetMethod();
				var insertMethod = typeof(ModUpdateDetails).GetMethodSafe(nameof(
					ModUpdateDetails.SuppressContentChanged), true, typeof(bool), typeof(Mod));
				foreach (var instr in method) {
					var opcode = instr.opcode;
					yield return instr;
					if (opcode == OpCodes.Callvirt && targetMethod != null && (instr.operand as
							MethodBase) == targetMethod) {
						// Only the one after calling get_available_content
						gac = true;
						notCheck = false;
					} else if ((opcode == OpCodes.Ldc_I4_0 || opcode == OpCodes.Ldc_I4) && gac)
						// ldc 0 and ceq is the NOT instruction
						notCheck = true;
					else if (opcode == OpCodes.Ceq && notCheck && insertMethod != null) {
#if DEBUG
						PUtil.LogDebug("Patching Manager.Subscribe");
#endif
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						yield return new CodeInstruction(OpCodes.Call, insertMethod);
						gac = false;
						notCheck = false;
					} else
						notCheck = false;
				}
			}
		}

		/// <summary>
		/// Applied to ModsScreen to adjust tool tips for the subscription button to have
		/// the dates.
		/// </summary>
		[HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
		[HarmonyPriority(Priority.High)]
		public static class ModsScreen_BuildDisplay_Patch {
			/// <summary>
			/// Applied after BuildDisplay runs.
			/// </summary>
			internal static void Postfix(KButton ___closeButton, System.Collections.
					IEnumerable ___displayedMods) {
				// Must cast the type because ModsScreen.DisplayedMod is private
				if (___displayedMods != null) {
					var outdated = new List<ModToUpdate>(16);
					foreach (var displayedMod in ___displayedMods)
						ModUpdateHandler.AddModUpdateButton(outdated, Traverse.Create(
							displayedMod));
					if (outdated.Count > 0 && ___closeButton != null)
						ModUpdateHandler.AddUpdateAll(___closeButton.gameObject.GetParent(),
							outdated);
					UpdateMainMenu();
				}
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
				if (ModUpdateDetails.ScrubConfig())
					UpdateMainMenu();
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
