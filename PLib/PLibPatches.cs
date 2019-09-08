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
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// All patches for PLib are stored here and only applied once for all PLib mods loaded.
	/// </summary>
	sealed class PLibPatches {
		#region Patches

#pragma warning disable IDE0051 // Remove unused private members

		/// <summary>
		/// Applied to InputBindingsScreen to show PLib bindings properly.
		/// </summary>
		private static bool BuildDisplay_Prefix(ref InputBindingsScreen __instance) {
			KeyBindingManager.Instance.BuildDisplay(__instance);
			return false;
		}

		/// <summary>
		/// Applied to InputBindingsScreen to clean up PLib bindings properly.
		/// </summary>
		private static void DestroyDisplay_Prefix(ref GameObject ___parent) {
			KeyBindingManager.Instance.DestroyDisplay(___parent);
		}

		/// <summary>
		/// Applied to KInputManager to cancel key inputs.
		/// </summary>
		private static void HandleCancelInput_Postfix() {
			KeyBindingManager.Instance.HandleCancelInput();
		}

		/// <summary>
		/// Applied to modify LoadPreviewImage to silence "Preview image load failed".
		/// </summary>
		private static IEnumerable<CodeInstruction> LoadPreviewImage_Transpile(
				IEnumerable<CodeInstruction> body) {
			const string BLACKLIST = "LogFormat";
			var returnBody = new List<CodeInstruction>(body);
			int n = returnBody.Count;
			// Look for "call Debug.LogFormat" and omit it
			for (int i = 0; i < n; i++) {
				var instr = returnBody[i];
				if (instr.opcode.Name == "call" && (instr.operand as MethodBase)?.Name ==
						BLACKLIST && i > 3)
					// Patch this instruction and the 3 before it (ldstr, ldc, newarr)
					for (int j = i - 3; j <= i; j++) {
						instr = returnBody[j];
						instr.opcode = OpCodes.Nop;
						instr.operand = null;
					}
			}
			return returnBody;
		}

		/// <summary>
		/// Applied to InputBindingsScreen to suppress keystrokes when rebinding keys.
		/// </summary>
		private static bool OnKeyDown_Prefix(KButtonEvent e) {
			return KeyBindingManager.Instance.OnKeyDown(e);
		}

		/// <summary>
		/// Applied to InputBindingsScreen to reset PLib bindings if all bindings are reset.
		/// </summary>
		private static void OnReset_Prefix() {
			KeyBindingManager.Instance.Reset();
		}

		/// <summary>
		/// Applied to GameInputMapping to save our bindings when the game bindings are saved.
		/// </summary>
		private static void SaveBindings_Postfix() {
			KeyBindingManager.Instance.SaveBindings();
		}

		/// <summary>
		/// Applied to KInputController to handle custom button events.
		/// </summary>
		private static void KIC_Update_Postfix(KInputController __instance,
				Modifier ___mActiveModifiers) {
			KeyBindingManager.Instance.ProcessKeys(__instance, ___mActiveModifiers);
		}

		/// <summary>
		/// Applied to InputBindingsScreen to map a key when it is pressed.
		private static void IBS_Update_Postfix(ref InputBindingsScreen __instance,
				ref KeyCode[] ___validKeys) {
			KeyBindingManager.Instance.Update(__instance, ___validKeys);
		}

		/// <summary>
		/// Applies all patches.
		/// </summary>
		/// <param name="instance">The Harmony instance to use when patching.</param>
		private static void PatchAll(HarmonyInstance instance) {
			if (instance == null)
				throw new ArgumentNullException("instance");
			instance.Patch(typeof(InputBindingsScreen), "BuildDisplay",
				PatchMethod("BuildDisplay_Prefix"), null);
			instance.Patch(typeof(InputBindingsScreen), "DestroyDisplay",
				PatchMethod("DestroyDisplay_Prefix"), null);
			instance.Patch(typeof(InputBindingsScreen), "OnKeyDown",
				PatchMethod("OnKeyDown_Prefix"), null);
			instance.Patch(typeof(InputBindingsScreen), "OnReset",
				PatchMethod("OnReset_Prefix"), null);
			instance.Patch(typeof(InputBindingsScreen), "Update", null,
				PatchMethod("IBS_Update_Postfix"));
			instance.Patch(typeof(GameInputMapping), "SaveBindings", null,
				PatchMethod("SaveBindings_Postfix"));
			instance.Patch(typeof(KInputController), "HandleCancelInput", null,
				PatchMethod("HandleCancelInput_Postfix"));
			instance.Patch(typeof(KInputController), "Update", null,
				PatchMethod("KIC_Update_Postfix"));
			instance.PatchTranspile(typeof(SteamUGCService), "LoadPreviewImage",
				PatchMethod("LoadPreviewImage_Transpile"));
		}

#pragma warning restore IDE0051 // Remove unused private members

		#endregion

		#region Infrastructure

		/// <summary>
		/// Returns a patch method from this class. It must be static.
		/// </summary>
		/// <param name="name">The patch method name.</param>
		/// <returns>The matching method.</returns>
		private static HarmonyMethod PatchMethod(string name) {
			return new HarmonyMethod(typeof(PLibPatches).GetMethod(name, BindingFlags.
				NonPublic | BindingFlags.Static));
		}

		/// <summary>
		/// The version of PLib that these patches will represent.
		/// </summary>
		public string MyVersion { get; }

		public PLibPatches() {
			MyVersion = PVersion.VERSION;
		}

		/// <summary>
		/// Applies the patches for this version of PLib.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		public void Apply(HarmonyInstance instance) {
			PRegistry.LogPatchDebug("Using version " + MyVersion);
			PatchAll(instance);
			KeyUtils.Init();
		}

		public override bool Equals(object obj) {
			return obj is PLibPatches other && other.MyVersion == MyVersion;
		}

		public override int GetHashCode() {
			return MyVersion.GetHashCode();
		}

		public override string ToString() {
			return "PLibPatches version " + MyVersion;
		}

		#endregion
	}
}
