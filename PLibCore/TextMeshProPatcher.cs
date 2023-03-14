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
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Patches bugs in Text Mesh Pro.
	/// </summary>
	internal static class TextMeshProPatcher {
		/// <summary>
		/// The ID to use for Harmony patches.
		/// </summary>
		private const string HARMONY_ID = "TextMeshProPatch";

		/// <summary>
		/// Tracks whether the TMP patches have been checked.
		/// </summary>
		private static volatile bool patchChecked = false;

		/// <summary>
		/// Serializes multiple thread access to the patch status.
		/// </summary>
		private static readonly object patchLock = new object();

		/// <summary>
		/// Applied to TMP_InputField to fix a bug that prevented auto layout from ever
		/// working.
		/// </summary>
		private static bool AssignPositioningIfNeeded_Prefix(TMP_InputField __instance,
				RectTransform ___caretRectTrans, TMP_Text ___m_TextComponent) {
			bool cont = true;
			var crt = ___caretRectTrans;
			if (___m_TextComponent != null && crt != null && __instance != null &&
				___m_TextComponent.isActiveAndEnabled) {
				var rt = ___m_TextComponent.rectTransform;
				if (crt.localPosition != rt.localPosition ||
						crt.localRotation != rt.localRotation ||
						crt.localScale != rt.localScale ||
						crt.anchorMin != rt.anchorMin ||
						crt.anchorMax != rt.anchorMax ||
						crt.anchoredPosition != rt.anchoredPosition ||
						crt.sizeDelta != rt.sizeDelta ||
						crt.pivot != rt.pivot) {
					__instance.StartCoroutine(ResizeCaret(crt, rt));
					cont = false;
				}
			}
			return cont;
		}

		/// <summary>
		/// Checks to see if a patch with our class name has already been applied.
		/// </summary>
		/// <param name="patchList">The patch list to search.</param>
		/// <returns>true if a patch with this class has already patched the method, or false otherwise.</returns>
		private static bool HasOurPatch(IEnumerable<Patch> patchList) {
			bool found = false;
			if (patchList != null) {
				foreach (var patch in patchList) {
					string ownerName = patch.PatchMethod.DeclaringType.Name;
					// Avoid stomping ourselves, or legacy PLibs < 3.14
					if (ownerName == nameof(TextMeshProPatcher) || ownerName == "PLibPatches")
					{
#if DEBUG
						PUtil.LogDebug("TextMeshProPatcher found existing patch from: {0}".
							F(patch.owner));
#endif
						found = true;
						break;
					}
				}
			}
			return found;
		}

		/// <summary>
		/// Patches TMP_InputField with fixes, but only if necessary.
		/// </summary>
		/// <param name="tmpType">The type of TMP_InputField.</param>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		private static void InputFieldPatches(Type tmpType) {
			var instance = new Harmony(HARMONY_ID);
			var aip = tmpType.GetMethodSafe("AssignPositioningIfNeeded", false,
				PPatchTools.AnyArguments);
			if (aip != null && !HasOurPatch(Harmony.GetPatchInfo(aip)?.Prefixes))
				instance.Patch(aip, prefix: new HarmonyMethod(typeof(TextMeshProPatcher),
					nameof(AssignPositioningIfNeeded_Prefix)));
			var oe = tmpType.GetMethodSafe("OnEnable", false, PPatchTools.AnyArguments);
			if (oe != null && !HasOurPatch(Harmony.GetPatchInfo(oe)?.Postfixes))
				instance.Patch(oe, postfix: new HarmonyMethod(typeof(TextMeshProPatcher),
					nameof(OnEnable_Postfix)));
		}

		/// <summary>
		/// Applied to TMPro.TMP_InputField to fix a clipping bug inside of Scroll Rects.
		/// 
		/// https://forum.unity.com/threads/textmeshpro-text-still-visible-when-using-nested-rectmask2d.537967/
		/// </summary>
		private static void OnEnable_Postfix(UnityEngine.UI.Scrollbar ___m_VerticalScrollbar,
				TMP_Text ___m_TextComponent) {
			var component = ___m_TextComponent;
			if (component != null)
				component.ignoreRectMaskCulling = ___m_VerticalScrollbar != null;
		}

		/// <summary>
		/// Patches Text Mesh Pro input fields to fix a variety of bugs. Should be used before
		/// any Text Mesh Pro objects are created.
		/// </summary>
		public static void Patch() {
			lock (patchLock) {
				if (!patchChecked) {
					var tmpType = PPatchTools.GetTypeSafe("TMPro.TMP_InputField");
					if (tmpType != null)
						try {
							InputFieldPatches(tmpType);
						} catch (Exception) {
							PUtil.LogWarning("Unable to patch TextMeshPro bug, text fields may display improperly inside scroll areas");
						}
					patchChecked = true;
				}
			}
		}

		/// <summary>
		/// Resizes the caret object to match the text. Used as an enumerator.
		/// </summary>
		/// <param name="caretTransform">The rectTransform of the caret.</param>
		/// <param name="textTransform">The rectTransform of the text.</param>
		private static System.Collections.IEnumerator ResizeCaret(
				RectTransform caretTransform, RectTransform textTransform) {
			yield return new WaitForEndOfFrame();
			caretTransform.localPosition = textTransform.localPosition;
			caretTransform.localRotation = textTransform.localRotation;
			caretTransform.localScale = textTransform.localScale;
			caretTransform.anchorMin = textTransform.anchorMin;
			caretTransform.anchorMax = textTransform.anchorMax;
			caretTransform.anchoredPosition = textTransform.anchoredPosition;
			caretTransform.sizeDelta = textTransform.sizeDelta;
			caretTransform.pivot = textTransform.pivot;
		}
	}
}
