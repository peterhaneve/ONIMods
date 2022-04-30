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
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores cached labels to be used again in the storage info screen.
	/// </summary>
	internal sealed class CachedStorageLabel : IDisposable, IEquatable<CachedStorageLabel> {
		public const string EMPTY_ITEM = "empty";

		private const int FREEZE_THAWED = 0;

		private const int FREEZE_PENDING = 1;

		private const int FREEZE_FROZEN = 2;

		/// <summary>
		/// The number of lines of text displayed, as a crude indicator of when the item can
		/// be frozen.
		/// </summary>
		public int Lines { get; set; }

		/// <summary>
		/// Whether the label is active.
		/// </summary>
		private bool active;

		/// <summary>
		/// The element used when laying out frozen rows.
		/// </summary>
		private readonly LayoutElement freeze;

		private int frozen;

		/// <summary>
		/// The label's unique ID.
		/// </summary>
		private readonly string id;

		/// <summary>
		/// The item to drop.
		/// </summary>
		private GameObject item;

		/// <summary>
		/// The root game object of the label.
		/// </summary>
		internal readonly GameObject labelObj;

		internal readonly KButton removeButton;

		internal readonly KButton selectButton;

		/// <summary>
		/// The storage to drop from.
		/// </summary>
		private Storage storage;

		internal readonly LocText text;

		/// <summary>
		/// The element used when laying out unfrozen rows.
		/// </summary>
		private readonly LayoutGroup thaw;

		internal readonly ToolTip tooltip;

		/// <summary>
		/// Creates a new blank label.
		/// </summary>
		/// <param name="sis">The info screen for the label prefab.</param>
		/// <param name="parent">The parent object for the label.</param>
		/// <param name="id">The unique ID of this label.</param>
		internal CachedStorageLabel(SimpleInfoScreen sis, GameObject parent, string id) {
			var label = Util.KInstantiate(sis.attributesLabelButtonTemplate, parent, id);
			var transform = label.transform;
			transform.localScale = Vector3.one;
			labelObj = label;
			freeze = label.AddOrGet<LayoutElement>();
			frozen = FREEZE_THAWED;
			label.TryGetComponent(out selectButton);
			label.TryGetComponent(out thaw);
			Lines = 0;
			selectButton.onClick += Select;
			tooltip = label.GetComponentInChildren<ToolTip>();
			text = label.GetComponentInChildren<LocText>();
			if (id == EMPTY_ITEM)
				text.SetText(STRINGS.UI.DETAILTABS.DETAILS.STORAGE_EMPTY);
			// Set up the manual drop button, but default disable
			transform = transform.Find("removeAttributeButton");
			if (transform != null) {
				var remove = transform.FindComponent<KButton>();
				remove.enabled = false;
				remove.gameObject.SetActive(false);
				remove.onClick += Drop;
				removeButton = remove;
			} else
				removeButton = null;
			active = false;
			this.id = id;
			item = null;
			storage = null;
			freeze.enabled = false;
		}

		public void Dispose() {
			Reset();
			if (labelObj != null)
				Util.KDestroyGameObject(labelObj);
			active = false;
		}

		/// <summary>
		/// Manually drops the requested item from the storage.
		/// </summary>
		private void Drop() {
			if (item != null && storage != null)
				storage.Drop(item);
		}

		public override bool Equals(object obj) {
			return obj is CachedStorageLabel other && other.id == id;
		}

		public bool Equals(CachedStorageLabel other) {
			return other.id == id;
		}

		/// <summary>
		/// Freezes the layout only if the number of lines has not changed.
		/// </summary>
		/// <param name="lines">The number of lines of text displayed.</param>
		/// <param name="allowUpdate">true to allow moving into frozen states, or false to thaw only.</param>
		internal void FreezeIfMatch(int lines, bool allowUpdate) {
			if (lines != Lines) {
				Lines = lines;
				if (frozen != FREEZE_THAWED) {
					freeze.enabled = false;
					thaw.enabled = true;
					frozen = FREEZE_THAWED;
				}
			} else if (allowUpdate) {
				if (frozen == FREEZE_THAWED)
					frozen = FREEZE_PENDING;
				else if (frozen == FREEZE_PENDING) {
					freeze.CopyFrom(thaw);
					thaw.enabled = false;
					freeze.enabled = true;
					frozen = FREEZE_FROZEN;
				}
			}
		}

		public override int GetHashCode() {
			return id.GetHashCode();
		}

		/// <summary>
		/// Clears the old on-click handlers from the last usage.
		/// </summary>
		internal void Reset() {
			item = null;
			storage = null;
		}

		/// <summary>
		/// Selects the item when clicked.
		/// </summary>
		private void Select() {
			if (item != null && item.TryGetComponent(out KSelectable selectable))
				SelectTool.Instance.Select(selectable);
		}

		/// <summary>
		/// Shows or hides the label.
		/// </summary>
		/// <param name="active">true to show the label, or false otherwise.</param>
		internal void SetActive(bool active) {
			if (active != this.active) {
				labelObj.SetActive(active);
				this.active = active;
			}
		}

		/// <summary>
		/// Shows or hides the drop from UI button.
		/// </summary>
		/// <param name="allowDrop">true to show the manual drop button, or false otherwise.</param>
		/// <param name="storage">The storage to drop from if clicked.</param>
		/// <param name="item">The item to drop if clicked.</param>
		internal void SetAllowDrop(bool allowDrop, Storage storage, GameObject item) {
			var remove = removeButton;
			if (remove != null) {
				// Toggle enabled if required
				if (remove.enabled != allowDrop) {
					remove.enabled = allowDrop;
					remove.gameObject.SetActive(allowDrop);
				}
				if (allowDrop)
					this.storage = storage;
			}
			this.item = item;
		}
	}

	/// <summary>
	/// Sorts side screens by their sort key.
	/// </summary>
	internal sealed class StressNoteComparer : IComparer<ReportManager.ReportEntry.Note> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly StressNoteComparer Instance = new StressNoteComparer();

		private StressNoteComparer() { }

		public int Compare(ReportManager.ReportEntry.Note x, ReportManager.ReportEntry.Note y)
		{
			return x.value.CompareTo(y.value);
		}
	}
}
