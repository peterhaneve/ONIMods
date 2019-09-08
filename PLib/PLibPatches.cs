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

namespace PeterHan.PLib {
	/// <summary>
	/// All patches for PLib are stored here and only applied once for all PLib mods loaded.
	/// </summary>
	sealed class PLibPatches {
		#region Patches

#pragma warning disable IDE0051 // Remove unused private members

		/// <summary>
		/// Applied to modify SteamUGCService to silence "Preview image load failed".
		/// </summary>
		private static IEnumerable<CodeInstruction> LoadPreviewImage_Transpile(
				IEnumerable<CodeInstruction> body) {
			const string BLACKLIST = "LogFormat";
			var returnBody = new List<CodeInstruction>(body);
			int n = returnBody.Count;
			// Look for "call Debug.LogFormat" and wipe it with NOP
			for (int i = 0; i < n; i++) {
				var instr = returnBody[i];
				if (instr.opcode.Name == "call" && (instr.operand as MethodBase)?.Name ==
						BLACKLIST && i > 3) {
					// Patch this instruction and the 3 before it (ldstr, ldc, newarr)
					for (int j = i - 3; j <= i; j++) {
						instr = returnBody[j];
						instr.opcode = OpCodes.Nop;
						instr.operand = null;
					}
					PRegistry.LogPatchDebug("No more preview image load failure ({0:D})".F(i));
				}
			}
			return returnBody;
		}

		/// <summary>
		/// Applied to KeyDef (constructor) to adjust array lengths if necessary.
		/// </summary>
		private static void CKeyDef_Postfix(ref KInputController.KeyDef __instance) {
			__instance.mActionFlags = PActionManager.ExtendFlags(__instance.mActionFlags,
				PActionManager.Instance.GetMaxAction());
		}

		/// <summary>
		/// Applied to KInputController to adjust array lengths if necessary.
		/// </summary>
		private static void IsActive_Prefix(ref bool[] ___mActionState) {
			___mActionState = PActionManager.ExtendFlags(___mActionState, PActionManager.
				Instance.GetMaxAction());
		}

		/// <summary>
		/// Applied to KInputController to adjust array lengths if necessary.
		/// </summary>
		private static void QueueButtonEvent_Prefix(ref bool[] ___mActionState,
				ref KInputController.KeyDef key_def) {
			if (KInputManager.isFocused) {
				int max = PActionManager.Instance.GetMaxAction();
				key_def.mActionFlags = PActionManager.ExtendFlags(key_def.mActionFlags, max);
				___mActionState = PActionManager.ExtendFlags(___mActionState, max);
			}
		}

		/// <summary>
		/// Applied to GameInputMapping to update the action count if new actions are
		/// registered.
		/// </summary>
		private static void SetDefaultKeyBindings_Postfix() {
			PActionManager.Instance.UpdateMaxAction();
		}

		/// <summary>
		/// Applies all patches.
		/// </summary>
		/// <param name="instance">The Harmony instance to use when patching.</param>
		private static void PatchAll(HarmonyInstance instance) {
			if (instance == null)
				throw new ArgumentNullException("instance");

			// GameInputMapping
			instance.Patch(typeof(GameInputMapping), "SetDefaultKeyBindings", null,
				PatchMethod("SetDefaultKeyBindings_Postfix"));

			// KInputController
			instance.PatchConstructor(typeof(KInputController.KeyDef), new Type[] {
				typeof(KKeyCode), typeof(Modifier)
			}, null, PatchMethod("CKeyDef_Postfix"));
			instance.Patch(typeof(KInputController), "IsActive",
				PatchMethod("IsActive_Prefix"), null);
			instance.Patch(typeof(KInputController), "QueueButtonEvent",
				PatchMethod("QueueButtonEvent_Prefix"), null);
			
			// SteamUGCService
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
			PActionManager.Instance.Init();
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
