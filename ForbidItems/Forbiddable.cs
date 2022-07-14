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
using KSerialization;
using PeterHan.PLib.Actions;
using UnityEngine;

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Displays the status item for forbidden items. This component is not added until
	/// necessary.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class Forbiddable : KMonoBehaviour {
#pragma warning disable CS0649
#pragma warning disable IDE0044
		[MyCmpGet]
		private Clearable clearable;

		[MyCmpReq]
		private KPrefabID prefabID;

		[MyCmpReq]
		private KSelectable selectable;
#pragma warning restore IDE0044
#pragma warning restore CS0649
		
		/// <summary>
		/// Tracks the current status item that is shown.
		/// </summary>
		private Guid forbiddenStatus;

		/// <summary>
		/// Prevents the item from being picked up.
		/// </summary>
		public void Forbid() {
			var go = gameObject;
			if (go != null) {
				prefabID.AddTag(ForbidItemsPatches.Forbidden);
				Game.Instance.userMenu.Refresh(go);
			}
		}

		protected override void OnCleanUp() {
			base.OnCleanUp();
			Unsubscribe((int)GameHashes.RefreshUserMenu);
			Unsubscribe((int)GameHashes.Absorb);
			Unsubscribe((int)GameHashes.OnStore);
			Unsubscribe((int)GameHashes.TagsChanged);
			if (forbiddenStatus != Guid.Empty)
				forbiddenStatus = selectable.RemoveStatusItem(forbiddenStatus);
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.TagsChanged, OnTagsChanged);
			Subscribe((int)GameHashes.Absorb, OnAbsorb);
			Subscribe((int)GameHashes.OnStore, OnStore);
			Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
			RefreshStatus();
		}

		/// <summary>
		/// When an item is absorbed into this one, forbids this stack if the victim stack
		/// was also forbidden.
		/// </summary>
		/// <param name="data">The item being absorbed into this one.</param>
		private void OnAbsorb(object data) {
			if (data is Pickupable other && other.TryGetComponent(out KPrefabID id) &&
					id.HasTag(ForbidItemsPatches.Forbidden) && !prefabID.HasTag(
					ForbidItemsPatches.Forbidden)) {
				prefabID.AddTag(ForbidItemsPatches.Forbidden);
				Game.Instance.userMenu.Refresh(gameObject);
			}
		}

		/// <summary>
		/// When the user menu is updated, add a button for this object.
		/// </summary>
		private void OnRefreshUserMenu(object _) {
			// Only apply to things that can otherwise be swept
			if (!prefabID.HasTag(GameTags.Stored) && clearable != null && clearable.
					isClearable) {
				string text, tooltip;
				System.Action handler;
				if (prefabID.HasTag(ForbidItemsPatches.Forbidden)) {
					text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME_OFF;
					tooltip = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP_OFF;
					handler = Reclaim;
				} else {
					text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME;
					tooltip = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP;
					handler = Forbid;
				}
				Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
					"action_building_disabled", text, handler, PAction.MaxAction, null, null,
					null, tooltip));
			}
		}

		/// <summary>
		/// When this object is (somehow) stored, remove the forbid tag.
		/// </summary>
		private void OnStore(object _) {
			prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
		}

		/// <summary>
		/// When the Forbidden tag is added or removed, updates the UI.
		/// </summary>
		private void OnTagsChanged(object _) {
			RefreshStatus();
		}

		/// <summary>
		/// Allows the item to be picked up.
		/// </summary>
		public void Reclaim() {
			var go = gameObject;
			if (go != null) {
				prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
				Game.Instance.userMenu.Refresh(go);
			}
		}

		/// <summary>
		/// Refreshes the visual icon for forbidding items.
		/// </summary>
		internal void RefreshStatus() {
			bool forbidden = prefabID.HasTag(ForbidItemsPatches.Forbidden);
			forbiddenStatus = selectable.ToggleStatusItem(ForbidItemsPatches.ForbiddenStatus,
				forbiddenStatus, forbidden, this);
			if (forbidden && clearable != null && clearable.isClearable)
				clearable.CancelClearing();
		}
	}
}
