/*
 * Copyright 2019 Peter Han
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

using UnityEngine;

namespace PeterHan.Resculpt {
	/// <summary>
	/// A behavior for Artable items that adds a Resculpt button in the UI.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class Resculptable : KMonoBehaviour {
		/// <summary>
		/// The event handler when the info menu is displayed.
		/// </summary>
		private static EventSystem.IntraObjectHandler<Resculptable> OnRefreshDelegate =
			new EventSystem.IntraObjectHandler<Resculptable>(OnRefreshUserMenu);

		/// <summary>
		/// The delegate called when the user menu is displayed.
		/// </summary>
		private static void OnRefreshUserMenu(Resculptable component, object _) {
			component.OnRefreshUserMenu();
		}

		/// <summary>
		/// The text shown on the repaint or resculpt button. If null, the Resculpt text is
		/// used.
		/// </summary>
		[SerializeField]
		public string ButtonText;

		[MyCmpReq]
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		private Artable artable;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		protected override void OnCleanUp() {
			base.OnCleanUp();
			Unsubscribe((int)GameHashes.RefreshUserMenu, OnRefreshDelegate);
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshDelegate);
		}

		/// <summary>
		/// Triggered when the user requests a resculpt of the decor item.
		/// </summary>
		private void OnResculpt() {
			Artable.Status currentStatus;
			if (artable != null && (currentStatus = artable.CurrentStatus) != Artable.Status.
					Ready) {
				var eligible = ListPool<Artable.Stage, Resculptable>.Allocate();
				int currentIndex = 0;
				string stageID = artable.CurrentStage;
				try {
					// Populate with valid stages
					foreach (var stage in artable.stages)
						if (stage.statusItem == currentStatus) {
							// Search for the current one if possible
							if (stage.id == stageID)
								currentIndex = eligible.Count;
							eligible.Add(stage);
						}
					int n = eligible.Count;
					if (n > 1)
						// Next entry
						artable.SetStage(eligible[(currentIndex + 1) % n].id, true);
				} finally {
					eligible.Recycle();
				}
			}
		}

		/// <summary>
		/// Called when the info screen for the decor item is refreshed.
		/// </summary>
		private void OnRefreshUserMenu() {
			if (artable != null && artable.CurrentStatus != Artable.Status.Ready) {
				string text = ButtonText;
				// Set default name if not set
				if (string.IsNullOrEmpty(text))
					text = ResculptStrings.RESCULPT_BUTTON;
				var button = new KIconButtonMenu.ButtonInfo("action_control", text, OnResculpt,
					Action.NumActions, null, null, null, ResculptStrings.RESCULPT_TOOLTIP);
				Game.Instance?.userMenu?.AddButton(gameObject, button);
			}
		}
	}
}
