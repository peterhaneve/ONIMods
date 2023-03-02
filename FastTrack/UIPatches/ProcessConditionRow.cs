/*
 * Copyright 2023 Peter Han
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
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores a cached process condition (checklist) row or header.
	/// </summary>
	internal sealed class ProcessConditionRow : IDisposable {
		private readonly Image box;

		private readonly GameObject check;

		/// <summary>
		/// The element used when laying out frozen rows.
		/// </summary>
		private readonly LayoutElement freeze;

		private ProcessCondition.Status lastStatus;

		private readonly GameObject root;

		private readonly LocText text;

		/// <summary>
		/// The element used when laying out unfrozen rows.
		/// </summary>
		private readonly LayoutGroup thaw;

		private readonly ToolTip tooltip;

		internal ProcessConditionRow(GameObject go, bool header) {
			// Will never be equal to a valid status
			lastStatus = (ProcessCondition.Status)(-1);
			root = go;
			root.TryGetComponent(out freeze);
			root.TryGetComponent(out thaw);
			root.TryGetComponent(out tooltip);
			if (root.TryGetComponent(out HierarchyReferences hr)) {
				text = hr.GetReference<LocText>("Label");
				if (header) {
					box = null;
					check = null;
				} else {
					var dash = hr.GetReference<Image>("Dash").gameObject;
					box = hr.GetReference<Image>("Box");
					check = hr.GetReference<Image>("Check").gameObject;
					dash.SetActive(false);
				}
			} else {
				box = null;
				text = null;
				check = null;
			}
			freeze.enabled = false;
			thaw.enabled = true;
		}

		public void Dispose() {
			if (root != null)
				Util.KDestroyGameObject(root);
		}

		/// <summary>
		/// Freezes the row layout.
		/// </summary>
		internal void Freeze() {
			if (root.activeInHierarchy) {
				freeze.CopyFrom(thaw);
				thaw.enabled = false;
				freeze.enabled = true;
			}
		}

		/// <summary>
		/// Shows or hides the row.
		/// </summary>
		/// <param name="active">true to show the row, or false to hide and thaw the row.</param>
		internal void SetActive(bool active) {
			root.SetActive(active);
			if (!active) {
				// Thaw layout
				freeze.enabled = false;
				thaw.enabled = true;
			}
		}

		/// <summary>
		/// Sets the appearance of this row to match a process condition.
		/// </summary>
		/// <param name="condition">The condition to evaluate and display.</param>
		internal void SetCondition(ProcessCondition condition) {
			var status = condition.EvaluateCondition();
			if (status != lastStatus) {
				Color itemColor;
				switch (status) {
				case ProcessCondition.Status.Warning:
					itemColor = ConditionListSideScreen.warningColor;
					break;
				case ProcessCondition.Status.Ready:
					itemColor = ConditionListSideScreen.readyColor;
					break;
				case ProcessCondition.Status.Failure:
				default:
					itemColor = ConditionListSideScreen.failedColor;
					break;
				}
				text.color = itemColor;
				box.color = itemColor;
				check.SetActive(status == ProcessCondition.Status.Ready);
				lastStatus = status;
			}
			SetTitle(condition.GetStatusMessage(status), condition.GetStatusTooltip(
				status));
		}

		/// <summary>
		/// Sets the displayed title and tool tip.
		/// </summary>
		/// <param name="titleText">The text to display in the side screen.</param>
		/// <param name="tooltipText">The text to display on hover.</param>
		internal void SetTitle(string titleText, string tooltipText) {
			if (titleText != text.text)
				text.SetText(titleText);
			tooltip.toolTip = tooltipText;
		}
	}
}
