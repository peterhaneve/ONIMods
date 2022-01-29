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

using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Adds a button, only in sandbox mode, to instantly cause a Duplicant to become overjoyed
	/// or stressed out.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class InstantEmotable : KMonoBehaviour, IRefreshUserMenu {
		/// <summary>
		/// Handles user menu refresh events system-wide.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<InstantEmotable>
			ON_REFRESH_MENU = PGameUtils.CreateUserMenuHandler<InstantEmotable>();

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
		}

		/// <summary>
		/// Called when a Duplicant needs to become stressed out.
		/// </summary>
		private void OnStressOut() {
			var stress = Db.Get().Amounts.Stress;
			stress.Lookup(gameObject)?.SetValue(stress.maxAttribute.BaseValue);
		}

		/// <summary>
		/// Called when a Duplicant needs to become overjoyed.
		/// </summary>
		private void OnOverjoyed() {
			gameObject.GetSMI<JoyBehaviourMonitor.Instance>()?.GoToOverjoyed();
		}

		/// <summary>
		/// Called when the info screen for the plant or creature is refreshed.
		/// </summary>
		public void OnRefreshUserMenu() {
			if (Game.Instance.SandboxModeActive) {
				Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
					ButtonInfo("action_repair", DebugNotIncludedStrings.
					UI.USERMENUOPTIONS.OVERJOY, OnOverjoyed, PAction.MaxAction, null,
					null, null, DebugNotIncludedStrings.UI.TOOLTIPS.DNI_OVERJOY));
				Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
					ButtonInfo("action_building_cancel", DebugNotIncludedStrings.
					UI.USERMENUOPTIONS.STRESSOUT, OnStressOut, PAction.MaxAction, null,
					null, null, DebugNotIncludedStrings.UI.TOOLTIPS.DNI_STRESSOUT));
			}
		}
	}
}
