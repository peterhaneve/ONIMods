/*
 * Copyright 2026 Peter Han
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

using KSerialization;
using PeterHan.PLib.Actions;
using UnityEngine;

namespace PeterHan.DeselectNewMaterials {
	/// <summary>
	/// Stores the accepts/rejects new materials option on each Storage object.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class NewMaterialsSettings : KMonoBehaviour, ISaveLoadable {
		/// <summary>
		/// Whether this storage accepts new materials.
		/// </summary>
		public bool AcceptsNewMaterials => acceptsNew == NewMaterialSetting.Accepts;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private Refrigerator refrigerator;

		[MyCmpGet]
		private RationBox rationBox;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		[Serialize]
		[SerializeField]
		private NewMaterialSetting acceptsNew;

		internal NewMaterialsSettings() {
			acceptsNew = NewMaterialSetting.Default;
		}

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.RefreshUserMenu);
			Unsubscribe((int)GameHashes.CopySettings, OnCopySettings);
			base.OnCleanUp();
		}

		/// <summary>
		/// Called when the building settings are copied.
		/// </summary>
		/// <param name="data">The GameObject with the source settings.</param>
		private void OnCopySettings(object data) {
			if (data is GameObject go && go != null && go.TryGetComponent(
					out NewMaterialsSettings other)) {
				acceptsNew = other.acceptsNew;
				Game.Instance.userMenu?.Refresh(gameObject);
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			if (acceptsNew != NewMaterialSetting.Accepts && acceptsNew != NewMaterialSetting.
					Rejects)
				SetInitialValue();
			Subscribe((int)GameHashes.CopySettings, OnCopySettings);
			Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
		}

		/// <summary>
		/// Called when the info screen for the storage is refreshed.
		/// </summary>
		private void OnRefreshUserMenu(object _) {
			Game.Instance.userMenu?.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
				"action_power", (acceptsNew == NewMaterialSetting.Accepts) ?
				DeselectMaterialsStrings.ACCEPTS_MATERIALS : DeselectMaterialsStrings.
				REJECTS_MATERIALS, OnToggleAcceptsItems, PAction.MaxAction, null, null, null,
				DeselectMaterialsStrings.ACCEPT_TOOLTIP));
		}

		/// <summary>
		/// Fired when the user toggles accept/reject new items.
		/// </summary>
		private void OnToggleAcceptsItems() {
			acceptsNew = (acceptsNew == NewMaterialSetting.Accepts) ? NewMaterialSetting.
				Rejects : NewMaterialSetting.Accepts;
			Game.Instance.userMenu?.Refresh(gameObject);
		}

		/// <summary>
		/// Sets the initial value of the "Accepts New Materials" option based on the
		/// settings. Used for newly built storage.
		/// </summary>
		public void SetInitialValue() {
			acceptsNew = ((DeselectMaterialsPatches.Options?.IgnoreFoodBoxes ?? false) &&
				(refrigerator != null || rationBox != null)) ? NewMaterialSetting.Accepts :
				NewMaterialSetting.Rejects;
			Game.Instance.userMenu?.Refresh(gameObject);
		}

		/// <summary>
		/// The possible settings for new materials.
		/// </summary>
		private enum NewMaterialSetting {
			Default, Accepts, Rejects
		}
	}
}
