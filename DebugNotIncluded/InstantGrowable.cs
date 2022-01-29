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

using Klei.AI;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Adds a button, only in sandbox mode, to instantly set a critter to tame or a plant to
	/// full growth.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class InstantGrowable : KMonoBehaviour, IRefreshUserMenu {
		/// <summary>
		/// Handles user menu refresh events system-wide.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<InstantGrowable>
			ON_REFRESH_MENU = PGameUtils.CreateUserMenuHandler<InstantGrowable>();

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpGet]
		private Growing growing;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		private WildnessMonitor.Def wildMonitor;

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
			wildMonitor = gameObject.GetDef<WildnessMonitor.Def>();
		}

		/// <summary>
		/// Called when a creature or plant needs to be grown faster.
		/// </summary>
		private void OnInstantGrow() {
			if (growing != null) {
				var maturity = Db.Get().Amounts.Maturity.Lookup(gameObject);
				if (maturity != null)
					maturity.SetValue(maturity.GetMax());
			} else if (wildMonitor != null) {
				var wildness = Db.Get().Amounts.Wildness.Lookup(gameObject);
				if (wildness != null) {
					var effects = GetComponent<Effects>();
					wildness.SetValue(wildness.GetMin());
					// Remove "wild" effect which slowly increases wildness
					if (effects != null && effects.HasEffect(wildMonitor.wildEffect))
						effects.Remove(wildMonitor.wildEffect);
				}
			}
		}

		/// <summary>
		/// Called when the info screen for the plant or creature is refreshed.
		/// </summary>
		public void OnRefreshUserMenu() {
			if (Game.Instance.SandboxModeActive) {
				string tt = null;
				if (wildMonitor != null)
					tt = DebugNotIncludedStrings.UI.TOOLTIPS.DNI_INSTANT_TAME;
				else if (growing != null)
					tt = DebugNotIncludedStrings.UI.TOOLTIPS.DNI_INSTANT_GROW;
				if (tt != null)
					Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
						ButtonInfo("action_building_disabled", DebugNotIncludedStrings.
						UI.USERMENUOPTIONS.INSTANTGROW, OnInstantGrow, PAction.MaxAction, null,
						null, null, tt));
			}
		}
	}
}
