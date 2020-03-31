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

using KMod;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Stores information about the "More Mod Actions" screen.
	/// </summary>
	internal sealed class ModActionsScreen : MonoBehaviour, IPointerEnterHandler,
			IPointerExitHandler {
		/// <summary>
		/// Whether the mouse is over this component.
		/// </summary>
		public bool IsOver { get; private set; }

		/// <summary>
		/// The index of the active mod in the list.
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// The mod that is active.
		/// </summary>
		public Mod Mod { get; set; }

		internal ModActionsScreen() {
			Index = -1;
			IsOver = false;
			Mod = null;
		}

		public void OnPointerEnter(PointerEventData data) {
			IsOver = true;
		}

		public void OnPointerExit(PointerEventData data) {
			IsOver = false;
		}

		/// <summary>
		/// Shows or hides the parent game object.
		/// </summary>
		/// <param name="active">true to make it visible, or false otherwise.</param>
		public void SetActive(bool active) {
			gameObject.SetActive(active);
		}
	}
}
