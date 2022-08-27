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

using System;
using System.Collections.Generic;
using HarmonyLib;
using PeterHan.PLib.Actions;
using UnityEngine;

namespace PeterHan.FastTrack.SensorPatches {
	#if DEBUG
	/// <summary>
	/// An overlay mode that helps debug problems with reachability.
	/// </summary>
	internal sealed class ReachabilityDebugOverlay : OverlayModes.Mode {
		/// <summary>
		/// The ID of this overlay mode.
		/// </summary>
		public static readonly HashedString ID = new HashedString("FTREACHABLE");

		/// <summary>
		/// Retrieves the overlay color for a particular cell when in the crop view.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>The overlay color for this cell.</returns>
		internal static Color GetColor(SimDebugView _, int cell) {
			var shade = Color.black;
			var colors = GlobalAssets.Instance.colorSet;
			var cells = FastGroupProber.Instance.Cells;
			if (Grid.IsValidCell(cell))
				shade = cells[cell] > 0 ? colors.decorPositive : colors.decorNegative;
			return shade;
		}

		/// <summary>
		/// Cached legend colors used for reachability.
		/// </summary>
		private readonly List<LegendEntry> overlayLegend;

		public ReachabilityDebugOverlay() {
			var colors = GlobalAssets.Instance.colorSet;
			legendFilters = CreateDefaultFilters();
			overlayLegend = new List<LegendEntry>
			{
				new LegendEntry("Reachable", "Reachable count > 0", colors.decorPositive),
				new LegendEntry("Unreachable", "Reachable count <= 0", colors.decorNegative),
			};
		}
		
		public override void Disable() {
			UnregisterSaveLoadListeners();
			base.Disable();
		}

		public override void Enable() {
			base.Enable();
			RegisterSaveLoadListeners();
		}

		public override List<LegendEntry> GetCustomLegendData() {
			return overlayLegend;
		}

		public override string GetSoundName() {
			return "SuitRequired";
		}
		
		public override HashedString ViewMode() {
			return ID;
		}

		/// <summary>
		/// Applied to OverlayLegend to add an entry for the Reachability overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayLegend), nameof(OverlayLegend.OnSpawn))]
		internal static class OverlayLegend_OnSpawn_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

			/// <summary>
			/// Applied before OnSpawn runs.
			/// </summary>
			internal static void Prefix(ICollection<OverlayLegend.OverlayInfo> ___overlayInfoList) {
				Strings.Add("STRINGS.UI.OVERLAYS.REACHABILITY_DEBUG.NAME", "Reachability");
				Strings.Add("STRINGS.UI.OVERLAYS.REACHABILITY_DEBUG.DESCRIPTION",
					"Indicates where Fast Reachability thinks things are reachable.");
				___overlayInfoList.Add(new OverlayLegend.OverlayInfo {
					infoUnits = new List<OverlayLegend.OverlayInfoUnit>(1) {
						new OverlayLegend.OverlayInfoUnit(
							Assets.GetSprite("overlay_suit"),
							"STRINGS.UI.OVERLAYS.REACHABILITY_DEBUG.DESCRIPTION",
							Color.white, Color.white)
					},
					isProgrammaticallyPopulated = true,
					mode = ID,
					name = "STRINGS.UI.OVERLAYS.REACHABILITY_DEBUG.NAME",
				});
			}
		}
		
		/// <summary>
		/// Applied to OverlayMenu to add a button for our overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayMenu), nameof(OverlayMenu.InitializeToggles))]
		internal static class OverlayMenu_InitializeToggles_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

			/// <summary>
			/// Applied after InitializeToggles runs.
			/// </summary>
			internal static void Postfix(ICollection<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos) {
				var info = new OverlayMenu.OverlayToggleInfo("Reachability", "overlay_suit",
					ID, "", PAction.MaxAction, "Debug Fast Track reachability");
				___overlayToggleInfos?.Add(info);
			}
		}

		/// <summary>
		/// Applied to OverlayScreen to add our overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
		internal static class OverlayScreen_RegisterModes_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

			/// <summary>
			/// Applied after RegisterModes runs.
			/// </summary>
			internal static void Postfix(OverlayScreen __instance) {
				__instance.RegisterMode(new ReachabilityDebugOverlay());
			}
		}

		/// <summary>
		/// Applied to SimDebugView to add a color handler for the Reachability overlay.
		/// </summary>
		[HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
		internal static class SimDebugView_OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(IDictionary<HashedString, Func<SimDebugView,
					int, Color>> ___getColourFuncs) {
				___getColourFuncs[ID] = GetColor;
			}
		}
	}
	#endif
}
