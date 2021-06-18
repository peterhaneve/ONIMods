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
using Microsoft.Win32;
using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using Steamworks;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

#if DEBUG
namespace PeterHan.DebugNotIncluded {
	internal static class ModEditorPatches {
		private static readonly RectOffset ROW_MARGIN = new RectOffset(2, 2, 2, 2);

		internal static void AddRow(this PGridPanel panel, string rowTitle, IUIComponent text)
		{
			int row = panel.Rows;
			panel.AddRow(new GridRowSpec()).AddChild(new PLabel() {
				Text = rowTitle, Margin = ROW_MARGIN
			}, new GridComponentSpec(row, 0) {
				Alignment = TextAnchor.MiddleRight, Margin = new RectOffset(0, 5, 0, 0)
			}).AddChild(text, new GridComponentSpec(row, 1) {
				Alignment = TextAnchor.UpperLeft, Margin = ROW_MARGIN
			});
		}

		internal static object GetSubKeyValue(this RegistryKey parent, string path,
				string entry) {
			return GetSubKeyValue(parent, path.Split('\\'), 0, entry);
		}

		private static object GetSubKeyValue(RegistryKey key, string[] components, int index,
				string entry) {
			object result = null;
			using (var subKey = key.OpenSubKey(components[index++])) {
				if (subKey == null)
					result = null;
				else if (index < components.Length)
					result = GetSubKeyValue(subKey, components, index, entry);
				else
					result = subKey.GetValue(entry);
			}
			return result;
		}

		[HarmonyPatch(typeof(SteamUGCService), "OnSteamUGCQueryDetailsCompleted")]
		public static class SteamUGCService_OnSteamUGCQueryDetailsCompleted_Patch {
			internal static void Postfix(HashSet<SteamUGCDetails_t> ___publishes) {
				foreach (var details in ___publishes)
					ModEditor.AddModInfo(details);
			}
		}

		[HarmonyPatch(typeof(SteamUGCService), "Update")]
		public static class SteamUGCService_Update_Patch {
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var target = typeof(SteamUGC).GetMethodSafe(nameof(SteamUGC.
					CreateQueryUGCDetailsRequest), true, PPatchTools.AnyArguments);
				var insert = typeof(SteamUGC).GetMethodSafe(nameof(SteamUGC.
					SetReturnLongDescription), true, typeof(UGCQueryHandle_t), typeof(bool));
				foreach (var instr in method) {
					yield return instr;
					if (instr.opcode == OpCodes.Call && target != null && (instr.operand as
							MethodBase) == target) {
						// dup UGCQueryHandle
						yield return new CodeInstruction(OpCodes.Dup);
						// true
						yield return new CodeInstruction(OpCodes.Ldc_I4_1);
						yield return new CodeInstruction(OpCodes.Call, insert);
						// ignore output
						yield return new CodeInstruction(OpCodes.Pop);
					}
				}
			}
		}
	}
}
#endif
