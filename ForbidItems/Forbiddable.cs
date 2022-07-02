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
using KSerialization;
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
				RefreshStatus();
				Game.Instance.userMenu.Refresh(go);
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			RefreshStatus();
		}

		protected override void OnCleanUp() {
			base.OnCleanUp();
			if (forbiddenStatus != Guid.Empty)
				forbiddenStatus = selectable.RemoveStatusItem(forbiddenStatus);
		}

		/// <summary>
		/// Allows the item to be picked up.
		/// </summary>
		public void Reclaim() {
			var go = gameObject;
			if (go != null) {
				prefabID.RemoveTag(ForbidItemsPatches.Forbidden);
				RefreshStatus();
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
		}
	}
}
