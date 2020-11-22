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
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PeterHan.DLCModEnabler {
	/// <summary>
	/// Patches which will be applied via annotations for DLC Mod Enabler.
	/// </summary>
	public static class DLCModEnabler {
		public static void Init() {
			var inst = HarmonyInstance.Create("DLCModEnabler");
			try {
				inst.PatchAll(typeof(DLCModEnabler).Assembly);
			} catch (Exception e) {
				Debug.LogException(e);
				throw;
			}
		}

		private static KButton MakeModsButton(KButton buttonPrefab, GameObject buttonParent,
				ColorStyleSetting colorStyle) {
			var button = Util.KInstantiateUI<KButton>(buttonPrefab.gameObject,
				buttonParent, true);
			button.onClick += Mods;
			var image = button.GetComponent<KImage>();
			image.colorStyleSetting = colorStyle;
			image.ApplyColorStyleSetting();
			var text = button.GetComponentInChildren<LocText>();
			text.text = "MODS [!]";
			text.fontSize = 14.0f;
			return button;
		}
		
		private static void Mods() {
			Util.KInstantiateUI<ModsScreen>(ScreenPrefabs.Instance.modsMenu.gameObject,
				MainMenu.Instance.transform.parent.gameObject, false);
		}

		[HarmonyPatch(typeof(MainMenu), "OnPrefabInit")]
		public static class MainMenu_OnPrefabInit_Patch {
			internal static void Postfix(KButton ___buttonPrefab, GameObject ___buttonParent,
					ColorStyleSetting ___normalButtonStyle) {
				MakeModsButton(___buttonPrefab, ___buttonParent, ___normalButtonStyle);
			}
		}

		[HarmonyPatch(typeof(KMod.Manager), MethodType.Constructor, new Type[0])]
		public static class Manager_Constructor_Patch {
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var body = new List<CodeInstruction>(method);
				var target = typeof(KMod.Mod).GetField("enabled", BindingFlags.NonPublic |
					BindingFlags.Public | BindingFlags.Instance);
				int n = body.Count;
				for (int i = n - 1; i > 0; i--) {
					var instr = body[i];
					if (instr.opcode == OpCodes.Ldfld && (instr.operand as FieldInfo) ==
							target) {
						instr.opcode = OpCodes.Ldc_I4_0;
						instr.operand = null;
						body.Insert(i, new CodeInstruction(OpCodes.Pop));
						break;
					}
				}
				return body;
			}
		}
	}
}
