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
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;

namespace PeterHan.NoWasteWant {
#if DEBUG
	/// <summary>
	/// Adds a button, only in sandbox mode, to instantly rot or unrot food.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class InstantRottable : KMonoBehaviour, IRefreshUserMenu {
		/// <summary>
		/// Handles user menu refresh events system-wide.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<InstantRottable>
			ON_REFRESH_MENU = PGameUtils.CreateUserMenuHandler<InstantRottable>();

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
		}

		private void OnRot() {
			var smi = gameObject.GetSMI<Rottable.Instance>();
			if (smi != null)
				smi.RotValue = 0.01f;
		}

		private void OnFreshen() {
			var smi = gameObject.GetSMI<Rottable.Instance>();
			if (smi != null)
				smi.RotValue = smi.def.spoilTime * 0.5f;
		}

		/// <summary>
		/// Called when the info screen for the plant or creature is refreshed.
		/// </summary>
		public void OnRefreshUserMenu() {
			if (Game.Instance.SandboxModeActive && gameObject.GetSMI<Rottable.Instance>() !=
					null) {
				Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
					ButtonInfo("action_repair", "Unrot food", OnFreshen, PAction.MaxAction,
					null, null, null, "Set freshness to 50%"));
				Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
					ButtonInfo("action_building_cancel", "Rot food", OnRot, PAction.MaxAction,
					null, null, null, "Set freshness to almost 0%"));
			}
		}

		/// <summary>
		/// Debug only handler to manipulate freshness values for testing.
		/// </summary>
		[HarmonyPatch(typeof(Edible), "OnPrefabInit")]
		public static class Edible_OnPrefabInit_Patch {
			internal static void Postfix(Edible __instance) {
				__instance.gameObject.AddOrGet<InstantRottable>();
			}
		}
	}
#endif
}
