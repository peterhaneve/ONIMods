/*
 * Copyright 2020 Peter Han
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

using PeterHan.PLib;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Handles the UI Debug action to log information about UI objects.
	/// </summary>
	internal sealed class UISnapshotHandler : IInputHandler {
		public string handlerName => "Debug UI Snapshot Handler";

		public KInputHandler inputHandler { get; set; }

		/// <summary>
		/// The action that will trigger the UI snapshot.
		/// </summary>
		private readonly Action snapshotAction;

		internal UISnapshotHandler() {
			var action = DebugNotIncludedPatches.UIDebugAction;
			if (action != null)
				snapshotAction = action.GetKAction();
			else
				snapshotAction = PAction.MaxAction;
		}

		/// <summary>
		/// Fired when a key is pressed.
		/// </summary>
		/// <param name="e">The event that occurred.</param>
		public void OnKeyDown(KButtonEvent e) {
			var es = UnityEngine.EventSystems.EventSystem.current;
			if (e.TryConsume(snapshotAction) && es != null) {
				var results = ListPool<RaycastResult, DebugHandler>.Allocate();
				es.RaycastAll(new PointerEventData(es) { position = Input.mousePosition },
					results);
				GameObject obj;
				foreach (var hit in results)
					if (hit.isValid && (obj = hit.gameObject) != null) {
						// Found it!
						PUIUtils.DebugObjectTree(obj);
						PUIUtils.DebugObjectHierarchy(obj);
					}
				results.Recycle();
			}
		}
	}
}
