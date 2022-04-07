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
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to ModsScreen to update the scroll pane whenever the list changes.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.BuildDisplay))]
	public static class ModsScreen_BuildDisplay_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after BuildDisplay runs.
		/// </summary>
		[HarmonyPriority(Priority.VeryLow)]
		internal static void Postfix(ModsScreen __instance) {
			var entryList = __instance.entryParent;
			if (entryList != null) {
				var vs = entryList.gameObject.GetComponentSafe<VirtualScroll>();
				if (vs != null)
					vs.Rebuild();
			}
		}
	}

	/// <summary>
	/// Applied to ModsScreen to set up listeners and state for virtual scroll.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.OnActivate))]
	public static class ModsScreen_OnActivate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after OnActivate runs.
		/// </summary>
		internal static void Postfix(ModsScreen __instance) {
			var entryList = __instance.entryParent;
			GameObject go;
			RectTransform rt;
			if (entryList != null && (go = entryList.gameObject) != null && (rt = go.
					rectTransform()) != null)
				go.AddOrGet<VirtualScroll>().Initialize(rt, 60.0f);
		}
	}
}
