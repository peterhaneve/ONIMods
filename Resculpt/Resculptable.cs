﻿/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib;
using UnityEngine;

namespace PeterHan.Resculpt {
	/// <summary>
	/// A behavior for Artable items that adds a Resculpt button in the UI.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class Resculptable : KMonoBehaviour {
		/// <summary>
		/// The icon sprite shown on the repaint or resculpt button.
		/// </summary>
		[SerializeField]
		public string ButtonIcon;

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
			Unsubscribe((int)GameHashes.RefreshUserMenu);
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
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
					if (n > 1) {
						var attrs = this.GetAttributes().Get(Db.Get().BuildingAttributes.Decor);
						// Remove the decor bonus (SetStage adds it back)
						attrs.Modifiers.RemoveAll((modifier) => modifier.Description ==
							"Art Quality");
						// Next entry
						artable.SetStage(eligible[(currentIndex + 1) % n].id, true);
					}
				} finally {
					eligible.Recycle();
				}
			}
		}


		/// <summary>
		/// Triggered when the user requests a rotation of the decor item.
		/// </summary>
		private void OnRotateClicked()
		{
			Rotatable rotatable = this.gameObject.GetComponent<Rotatable>();
			if (rotatable != null) rotatable.Rotate();

			// Buildings with even width values jump one tile when rotating and must be moved back
			BuildingDef def = this.gameObject.GetComponent<Building>()?.Def;
			if (def != null && def.WidthInCells % 2 == 0)
				this.transform.position += rotatable.GetOrientation() != Orientation.Neutral ? new UnityEngine.Vector3(1, 0, 0)
																							: new UnityEngine.Vector3(-1, 0, 0);
		}

		/// <summary>
		/// Called when the info screen for the decor item is refreshed.
		/// </summary>
		private void OnRefreshUserMenu(object _) {
			if (artable != null && artable.CurrentStatus != Artable.Status.Ready) {
				string text = ButtonText, icon = ButtonIcon;
				// Set default name if not set
				if (string.IsNullOrEmpty(text))
					text = ResculptStrings.RESCULPT_BUTTON;
				if (string.IsNullOrEmpty(icon))
					icon = ResculptStrings.RESCULPT_SPRITE;
				var button = new KIconButtonMenu.ButtonInfo(icon, text, OnResculpt,
					PAction.MaxAction, null, null, null, ResculptStrings.RESCULPT_TOOLTIP);
				Game.Instance?.userMenu?.AddButton(gameObject, button);

				var rotationButton = new KIconButtonMenu.ButtonInfo("action_direction_both",
																	"Rotate Art",
																	new System.Action(this.OnRotateClicked),
																	Action.BuildMenuKeyO,
																	tooltipText: "Rotates artwork. {Hotkey}");
				if (this.gameObject.GetComponent<Rotatable>() != null)
					Game.Instance?.userMenu?.AddButton(this.gameObject, rotationButton);
			}
		}
	}
}
