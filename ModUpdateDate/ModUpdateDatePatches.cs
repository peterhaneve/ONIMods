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
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using Steamworks;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Patches which will be applied via annotations for Mod Updater.
	/// </summary>
	public sealed class ModUpdateDatePatches : KMod.UserMod2 {
		/// <summary>
		/// The KMod which describes this mod.
		/// </summary>
		internal static Mod ThisMod { get; private set; }

		/// <summary>
		/// Handles a mod crash and bypasses disabling the mod if it is this mod.
		/// </summary>
		private static bool OnModCrash(Mod __instance) {
			return ThisMod == null || !__instance.label.Match(ThisMod.label);
		}

		/// <summary>
		/// Updates the number of outdated mods on the main menu.
		/// </summary>
		private static void UpdateMainMenu() {
			MainMenuWarning.Instance?.UpdateText();
		}

		public override void OnLoad(Harmony harmony) {
			var method = typeof(Mod).GetMethodSafe(nameof(Mod.SetCrashed), false);
			if (method != null)
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(ModUpdateDatePatches),
					nameof(OnModCrash)));
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(ModUpdateDatePatches));
			new POptions().RegisterOptions(this, typeof(ModUpdateInfo));
			new PLocalization().Register();
			ModUpdateInfo.LoadSettings();
			base.OnLoad(harmony);
			ThisMod = mod;
			// Shut off AVC
			PRegistry.PutData("PLib.VersionCheck.ModUpdaterActive", true);
			if (ModUpdateInfo.Settings?.PassiveMode == true)
				PRegistry.PutData("PLib.VersionCheck.PassiveSteamUpdate", true);
		}

		[PLibMethod(RunAt.BeforeDbInit)]
		private static void BeforeDbInit() {
			LocString.CreateLocStringKeys(typeof(ModUpdateDateStrings.UI));
		}

		/// <summary>
		/// Applied to MainMenu to initialize the main menu update warning.
		/// </summary>
		[HarmonyPatch(typeof(MainMenu), "OnPrefabInit")]
		public static class MainMenu_OnPrefabInit_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode != true;

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
		[HarmonyPatch(typeof(Manager), nameof(Manager.Subscribe))]
		public static class Manager_Subscribe_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode != true;

			/// <summary>
			/// Transpiles Subscribe to insert a call to SuppressContentChanged after the
			/// comparison, and a call to UpdateContentChanged instead of CopyPersistentDataTo.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				bool gac = false, notCheck = false;
				// get_available_content
				var targetMethod = typeof(Mod).GetPropertySafe<Content>(nameof(Mod.
					available_content), false)?.GetGetMethod();
				var insertMethod = typeof(ModUpdateDetails).GetMethodSafe(nameof(
					ModUpdateDetails.SuppressContentChanged), true, typeof(bool), typeof(Mod));
				var copyPersistent = typeof(Mod).GetMethodSafe(nameof(Mod.
					CopyPersistentDataTo), false, typeof(Mod));
				var updateCC = typeof(ModUpdateDetails).GetMethodSafe(nameof(ModUpdateDetails.
					UpdateContentChanged), true, typeof(Mod), typeof(Mod));
				foreach (var instr in method) {
					var opcode = instr.opcode;
					var operand = instr.operand;
					if (opcode == OpCodes.Callvirt && copyPersistent != null && updateCC !=
							null && (operand as MethodBase) == copyPersistent)
						instr.operand = updateCC;
					yield return instr;
					if (opcode == OpCodes.Callvirt && targetMethod != null && (operand as
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
		/// Applied to ModsScreen to add the update mod buttons.
		/// </summary>
		[HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
		[HarmonyPriority(Priority.High)]
		public static class ModsScreen_BuildDisplay_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode != true;

			/// <summary>
			/// Applied after BuildDisplay runs.
			/// </summary>
			internal static void Postfix(KButton ___closeButton, System.Collections.
					IEnumerable ___displayedMods) {
				// Must cast the type because ModsScreen.DisplayedMod is private
				if (___displayedMods != null) {
					var outdated = new List<ModToUpdate>(16);
					foreach (object displayedMod in ___displayedMods)
						if (displayedMod != null)
							ModUpdateHandler.AddModUpdateButton(outdated, displayedMod);
					if (outdated.Count > 0 && ___closeButton != null)
						ModUpdateHandler.AddUpdateAll(___closeButton.gameObject.GetParent(),
							outdated);
					UpdateMainMenu();
				}
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to add clients to the fixed version instead.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), nameof(SteamUGCService.AddClient))]
		public static class SteamUGCService_AddClient_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied before AddClient runs.
			/// </summary>
			internal static bool Prefix(SteamUGCService.IClient client) {
				SteamUGCServiceFixed.Instance.AddClient(client);
				return false;
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to retrieve mod lookups from the fixed version instead.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), nameof(SteamUGCService.FindMod))]
		public static class SteamUGCService_FindMod_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied after FindMod runs.
			/// </summary>
			internal static void Postfix(PublishedFileId_t item,
					ref SteamUGCService.Mod __result) {
				__result = SteamUGCServiceFixed.Instance.FindMod(item);
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to initialize the fixed version when the broken Klei
		/// version is initialized.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), nameof(SteamUGCService.Initialize))]
		public static class SteamUGCService_Initialize_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				SteamUGCServiceFixed.Instance.Initialize();
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to retrieve mod status from the fixed version instead.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), nameof(SteamUGCService.IsSubscribed))]
		public static class SteamUGCService_IsSubscribed_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied after IsSubscribed runs.
			/// </summary>
			internal static void Postfix(PublishedFileId_t item,
					ref bool __result) {
				__result = SteamUGCServiceFixed.Instance.IsSubscribed(item);
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to clean up our version when the Klei version is
		/// destroyed.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "OnDestroy")]
		public static class SteamUGCService_OnDestroy_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied after OnDestroy runs.
			/// </summary>
			internal static void Postfix() {
				SteamUGCServiceFixed.Instance.Dispose();
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to get detailed mod info when it is requested.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "OnSteamUGCQueryDetailsCompleted")]
		public static class SteamUGCService_OnSteamUGCQueryDetailsCompleted_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode != true;

			/// <summary>
			/// Applied after OnSteamUGCQueryDetailsCompleted runs.
			/// </summary>
			internal static void Postfix(HashSet<SteamUGCDetails_t> ___publishes) {
				if (___publishes != null)
					ModUpdateDetails.OnInstalledUpdate(___publishes);
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to remove clients from the fixed version instead.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), nameof(SteamUGCService.RemoveClient))]
		public static class SteamUGCService_RemoveClient_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied before RemoveClient runs.
			/// </summary>
			internal static bool Prefix(SteamUGCService.IClient client) {
				SteamUGCServiceFixed.Instance.RemoveClient(client);
				return false;
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to display the number of outdated mods after each mod
		/// update is checked.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "Update")]
		public static class SteamUGCService_UpdateActive_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode != true;

			/// <summary>
			/// Applied after Update runs.
			/// </summary>
			internal static void Postfix() {
				if (ModUpdateDetails.ScrubConfig())
					UpdateMainMenu();
			}
		}

		/// <summary>
		/// Applied to SteamUGCService to replace Update with the fixed version.
		/// </summary>
		[HarmonyPatch(typeof(SteamUGCService), "Update")]
		public static class SteamUGCService_UpdatePassive_Patch {
			internal static bool Prepare() => ModUpdateInfo.Settings?.PassiveMode == true;

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static bool Prefix() {
				SteamUGCServiceFixed.Instance.Process();
				return false;
			}
		}
	}
}
