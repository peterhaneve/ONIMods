/*
 * Copyright 2023 Peter Han
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
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to LoadScreen to remove a duplicate file read on every colony.
	/// </summary>
	[HarmonyPatch(typeof(LoadScreen), nameof(LoadScreen.GetColoniesDetails))]
	public static class LoadScreen_GetColoniesDetails_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.OptimizeDialogs;

		/// <summary>
		/// A stub method to replace the IsFileValid call.
		/// </summary>
		private static bool IsValidFake(LoadScreen _, string path) {
			return !string.IsNullOrEmpty(path);
		}

		/// <summary>
		/// Transpiles GetColoniesDetails to remove the duplicate call.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var target = typeof(LoadScreen).GetMethodSafe(nameof(LoadScreen.IsFileValid),
				false, typeof(string));
			var stub = typeof(LoadScreen_GetColoniesDetails_Patch).GetMethodSafe(
				nameof(IsValidFake), true, typeof(LoadScreen), typeof(string));
			var getInfo = typeof(SaveGame).GetMethodSafe(nameof(SaveGame.GetFileInfo), true,
				typeof(string));
			var addList = typeof(List<LoadScreen.SaveGameFileDetails>).GetMethodSafe(
				nameof(List<int>.Add), false, typeof(LoadScreen.SaveGameFileDetails));
			if (target != null && stub != null && getInfo != null && addList != null) {
				Label skipMe = generator.DefineLabel(), success = generator.DefineLabel();
				int state = 0;
				foreach (var instr in instructions) {
					if (state == 0 && instr.Is(OpCodes.Call, target)) {
						instr.operand = stub;
						state = 1;
					}
					if (state == 2 || state == 4) {
						// The target instruction to bounce, if the check passed
						var labels = instr.labels;
						if (labels == null)
							instr.labels = labels = new List<Label>(2);
						labels.Add(state == 2 ? success : skipMe);
						state++;
					}
					yield return instr;
					if (state == 1 && instr.Is(OpCodes.Call, getInfo)) {
						// null is returned if the save major version is invalid
						yield return new CodeInstruction(OpCodes.Dup);
						yield return new CodeInstruction(OpCodes.Brtrue_S, success);
						// Need a failure trampoline before jumping to end of method
						yield return new CodeInstruction(OpCodes.Pop);
						yield return new CodeInstruction(OpCodes.Br, skipMe);
						state = 2;
					} else if (state == 3 && instr.Is(OpCodes.Callvirt, addList))
						// The skip will target the next instruction
						state = 4;
				}
				if (state >= 5) {
#if DEBUG
					PUtil.LogDebug("Patched LoadScreen.GetColoniesDetails");
#endif
				} else
					PUtil.LogWarning("Unable to patch LoadScreen.GetColoniesDetails: {0}".
						F(state));
			} else {
				foreach (var instr in instructions)
					yield return instr;
				PUtil.LogWarning("Unable to patch LoadScreen.GetColoniesDetails");
			}
		}
	}

	/// <summary>
	/// Applied to LoadScreen to turn off the colony previews in exchange for a faster load.
	/// </summary>
	[HarmonyPatch(typeof(LoadScreen), nameof(LoadScreen.SetPreview))]
	public static class LoadScreen_SetPreview_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.DisableLoadPreviews;

		/// <summary>
		/// Applied before SetPreview runs.
		/// </summary>
		internal static bool Prefix(Image preview) {
			preview.color = Color.black;
			preview.gameObject.SetActive(false);
			return false;
		}
	}

	/// <summary>
	/// Applied to LoadScreen to stop a wasteful check to show the migration prompt.
	/// </summary>
	[HarmonyPatch(typeof(LoadScreen), nameof(LoadScreen.ShowMigrationIfNecessary))]
	public static class LoadScreen_ShowMigrationIfNecessary_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.OptimizeDialogs;

		/// <summary>
		/// Applied before ShowMigrationIfNecessary runs.
		/// </summary>
		internal static bool Prefix(LoadScreen __instance) {
			bool closed = __instance != null;
			if (closed)
				__instance.Deactivate();
			return !closed;
		}
	}
}
