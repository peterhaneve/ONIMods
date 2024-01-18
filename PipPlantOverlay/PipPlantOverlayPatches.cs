/*
 * Copyright 2024 Peter Han
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
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using StatusItemOverlays = StatusItem.StatusItemOverlays;

namespace PeterHan.PipPlantOverlay {
	/// <summary>
	/// Patches which will be applied via annotations for Pip Plant Overlay.
	/// </summary>
	public sealed class PipPlantOverlayPatches : KMod.UserMod2 {
		/// <summary>
		/// Public and non-public instance methods/constructors/types/fields.
		/// </summary>
		private const BindingFlags INSTANCE_ALL = PPatchTools.BASE_FLAGS | BindingFlags.
			Instance;

		private delegate void RegisterMode(OverlayScreen screen, OverlayModes.Mode mode);

		/// <summary>
		/// The key binding to open pip planting.
		/// </summary>
		private static PAction OpenOverlay;

		/// <summary>
		/// The private type to be used when making overlay buttons.
		/// </summary>
		private static readonly Type OVERLAY_TYPE = typeof(OverlayMenu).GetNestedType(
			"OverlayToggleInfo", INSTANCE_ALL);

		/// <summary>
		/// Registers a new overlay mode.
		/// </summary>
		private static readonly RegisterMode REGISTER_MODE = typeof(OverlayScreen).
			Detour<RegisterMode>();

		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			// Assets are now loaded, so create pip icon
			var pip = Assets.GetAnim("squirrel_kanim");
			Sprite sprite = null;
			if (pip != null)
				sprite = Def.GetUISpriteFromMultiObjectAnim(pip);
			if (sprite == null)
				// Pip anim is somehow missing?
				sprite = Assets.GetSprite("overlay_farming");
			Assets.Sprites.Add(PipPlantOverlayStrings.OVERLAY_ICON, sprite);
			// SPPR fixes the symmetry rule
			bool ruleFix = PPatchTools.GetTypeSafe("MightyVincent.Patches",
				"SimplerPipPlantRule") != null;
			if (ruleFix)
				PUtil.LogDebug("Detected Simpler Pip Plant Overlay, adjusting radius");
			PipPlantOverlayTests.SymmetricalRadius = ruleFix;
		}

		/// <summary>
		/// Gets an instance of the private overlay toggle info class used for creating new
		/// overlay buttons.
		/// </summary>
		/// <param name="text">The button text to be shown on mouseover.</param>
		/// <param name="iconName">The icon to show in the overlay list.</param>
		/// <param name="simView">The overlay mode to enter when selected.</param>
		/// <param name="openKey">The key binding to open the overlay.</param>
		/// <param name="tooltip">The tooltip to show on the overlay toggle.</param>
		/// <returns>The button to be added.</returns>
		private static KIconToggleMenu.ToggleInfo CreateOverlayInfo(string text,
				string iconName, HashedString simView, Action openKey, string tooltip) {
			const int KNOWN_PARAMS = 7;
			KIconToggleMenu.ToggleInfo info = null;
			ConstructorInfo[] cs;
			if (OVERLAY_TYPE == null || (cs = OVERLAY_TYPE.GetConstructors(INSTANCE_ALL)).
					Length != 1)
				PUtil.LogWarning("Unable to add PipPlantOverlay - missing constructor");
			else {
				var cons = cs[0];
				var toggleParams = cons.GetParameters();
				int paramCount = toggleParams.Length;
				// Manually plug in the knowns
				if (paramCount < KNOWN_PARAMS)
					PUtil.LogWarning("Unable to add PipPlantOverlay - parameters missing");
				else {
					object[] args = new object[paramCount];
					args[0] = text;
					args[1] = iconName;
					args[2] = simView;
					args[3] = "";
					args[4] = openKey;
					args[5] = tooltip;
					args[6] = text;
					// 3 and further (if existing) get new optional values
					for (int i = KNOWN_PARAMS; i < paramCount; i++) {
						var op = toggleParams[i];
						if (op.IsOptional)
							args[i] = op.DefaultValue;
						else {
							PUtil.LogWarning("Unable to add PipPlantOverlay - new parameters");
							args[i] = null;
						}
					}
					info = cons.Invoke(args) as KIconToggleMenu.ToggleInfo;
				}
			}
			return info;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(PipPlantOverlayPatches));
			LocString.CreateLocStringKeys(typeof(PipPlantOverlayStrings.INPUT_BINDINGS));
			PipPlantOverlayTests.SymmetricalRadius = false;
			OpenOverlay = new PActionManager().CreateAction(PipPlantOverlayStrings.
				OVERLAY_ACTION, PipPlantOverlayStrings.INPUT_BINDINGS.ROOT.PIPPLANT);
			new PLocalization().Register();
			// If possible, make farming status items appear properly in pip plant mode
			var overlayBitsField = typeof(StatusItem).GetFieldSafe("overlayBitfieldMap", true);
			if (overlayBitsField != null && overlayBitsField.GetValue(null) is
					IDictionary<HashedString, StatusItemOverlays> overlayBits)
				overlayBits.Add(PipPlantOverlay.ID, StatusItemOverlays.Farming);
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to OverlayLegend to add an entry for the Pip Planting overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayLegend), "OnSpawn")]
		public static class OverlayLegend_OnSpawn_Patch {
			/// <summary>
			/// Applied before OnSpawn runs.
			/// </summary>
			internal static void Prefix(ICollection<OverlayLegend.OverlayInfo> ___overlayInfoList) {
				___overlayInfoList.Add(new OverlayLegend.OverlayInfo {
					infoUnits = new List<OverlayLegend.OverlayInfoUnit>(1) {
						new OverlayLegend.OverlayInfoUnit(
							Assets.GetSprite(PipPlantOverlayStrings.OVERLAY_ICON),
							"STRINGS.UI.OVERLAYS.PIPPLANTING.DESCRIPTION",
							Color.white, Color.white)
					},
					isProgrammaticallyPopulated = true,
					mode = PipPlantOverlay.ID,
					name = "STRINGS.UI.OVERLAYS.PIPPLANTING.NAME",
				});
			}
		}

		/// <summary>
		/// Applied to OverlayMenu to add a button for our overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
		public static class OverlayMenu_InitializeToggles_Patch {
			/// <summary>
			/// Applied after InitializeToggles runs.
			/// </summary>
			internal static void Postfix(ICollection<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos) {
				LocString.CreateLocStringKeys(typeof(PipPlantOverlayStrings.UI));
				var action = OpenOverlay?.GetKAction() ?? PAction.MaxAction;
				var info = CreateOverlayInfo(PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.
					BUTTON, PipPlantOverlayStrings.OVERLAY_ICON, PipPlantOverlay.ID, action,
					PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING.TOOLTIP);
				if (info != null)
					___overlayToggleInfos?.Add(info);
			}
		}

		/// <summary>
		/// Applied to OverlayScreen to add our overlay.
		/// </summary>
		[HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
		public static class OverlayScreen_RegisterModes_Patch {
			/// <summary>
			/// Applied after RegisterModes runs.
			/// </summary>
			internal static void Postfix(OverlayScreen __instance) {
				PUtil.LogDebug("Creating PipPlantOverlay");
				REGISTER_MODE.Invoke(__instance, new PipPlantOverlay());
			}
		}

		/// <summary>
		/// Applied to SimDebugView to add a color handler for pip plant overlays.
		/// </summary>
		[HarmonyPatch(typeof(SimDebugView), "OnPrefabInit")]
		public static class SimDebugView_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(IDictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs) {
				___getColourFuncs[PipPlantOverlay.ID] = PipPlantOverlay.GetColor;
			}
		}
	}
}
