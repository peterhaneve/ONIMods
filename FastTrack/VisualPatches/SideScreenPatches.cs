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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Groups patches for the logic bit selector sidescreen.
	/// </summary>
	public static class LogicBitSelectorSideScreenPatches {
		// Side screen is a singleton so this is safe for now
		private static readonly IList<bool> lastValues = new List<bool>(4);

		/// <summary>
		/// A delegate to call the UpdateInputOutputDisplay method.
		/// </summary>
		private static readonly Action<LogicBitSelectorSideScreen> UPDATE_IO_DISPLAY =
			typeof(LogicBitSelectorSideScreen).Detour<Action<LogicBitSelectorSideScreen>>(
			"UpdateInputOutputDisplay");

		/// <summary>
		/// Updates all bits of the logic bit selector side screen.
		/// </summary>
		private static void ForceUpdate(LogicBitSelectorSideScreen instance,
				Color activeColor, Color inactiveColor, ILogicRibbonBitSelector target) {
			lastValues.Clear();
			foreach (var pair in instance.toggles_by_int) {
				int bit = pair.Key;
				bool active = target.IsBitActive(bit);
				while (lastValues.Count <= bit)
					lastValues.Add(false);
				lastValues[bit] = active;
				UpdateBit(pair.Value, active, activeColor, inactiveColor);
			}
		}

		/// <summary>
		/// Updates one bit of the logic bit selector side screen.
		/// </summary>
		private static void UpdateBit(MultiToggle multiToggle, bool active,
				Color activeColor, Color inactiveColor) {
			if (multiToggle != null) {
				var hr = multiToggle.gameObject.GetComponentSafe<HierarchyReferences>();
				hr.GetReference<KImage>("stateIcon").color = active ? activeColor :
					inactiveColor;
				hr.GetReference<LocText>("stateText").SetText(active ?
					STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_ACTIVE :
					STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_INACTIVE);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to update the visuals after it spawns
		/// (because side screens can have targets set for the first time before they are
		/// initialized).
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), "OnSpawn")]
		public static class OnSpawn_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				if (__instance != null)
					UPDATE_IO_DISPLAY.Invoke(__instance);
				ForceUpdate(__instance, ___activeColor, ___inactiveColor, ___target);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to set the initial states of LAST_VALUES
		/// for each bit.
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), "RefreshToggles")]
		public static class RefreshToggles_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied after RefreshToggles runs.
			/// </summary>
			internal static void Postfix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				ForceUpdate(__instance, ___activeColor, ___inactiveColor, ___target);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to optimize down its RenderEveryTick method,
		/// limiting it only to when visible and to only what is necessary.
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), nameof(LogicBitSelectorSideScreen.
			RenderEveryTick))]
		public static class RenderEveryTick_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				if (__instance != null && __instance.isActiveAndEnabled && ___target != null)
					foreach (var pair in __instance.toggles_by_int) {
						int bit = pair.Key;
						bool active = ___target.IsBitActive(bit), update = bit >= lastValues.
							Count;
						if (!update) {
							// If in range, see if bit changed
							update = active != lastValues[bit];
							if (update)
								lastValues[bit] = active;
						}
						if (update)
							UpdateBit(pair.Value, active, ___activeColor, ___inactiveColor);
					}
				return false;
			}
		}
	}

	/// <summary>
	/// Applied to TimerSideScreen to only update the side screen if it is active.
	/// </summary>
	[HarmonyPatch(typeof(TimerSideScreen), nameof(TimerSideScreen.RenderEveryTick))]
	public static class TimerSideScreen_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix(TimerSideScreen __instance) {
			return __instance != null && __instance.isActiveAndEnabled;
		}
	}
}
