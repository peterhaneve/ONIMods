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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Manages the addition of key bindings to the game with a method that can be configured
	/// in the InputBindingsScreen.
	/// 
	/// This class is only patched in once thanks to PLibPatches.
	/// </summary>
	public static class KeyBindingManager {
		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		private const string CATEGORY = "PLib";

		/// <summary>
		/// The title used for the PLib key bind category.
		/// </summary>
		private const string CATEGORY_TITLE = "PLib Mods";

		/// <summary>
		/// The bindings registered in this way.
		/// </summary>
		private static IList<PKeyBinding> ourBindings;

		/// <summary>
		/// Adds a key binding to the key input screen.
		/// </summary>
		/// <param name="entry">The key binding to add.</param>
		public static void AddKeyBinding(PKeyBinding entry) {
			var currentBindings = GameInputMapping.DefaultBindings;
			if (currentBindings != null) {
				// Create Klei binding
				var kEntry = new BindingEntry(CATEGORY, entry.GamePadButton, entry.KeyCode,
					entry.Modifier, Action.Invalid);
				// Allocate a new array and append the element
				int len = currentBindings.Length;
				var newBindings = new BindingEntry[len + 1];
				Array.Copy(currentBindings, newBindings, len);
				newBindings[len] = kEntry;
				GameInputMapping.SetDefaultKeyBindings(newBindings);
				// Add to our dictionary
				if (ourBindings == null)
					ourBindings = new List<PKeyBinding>(8);
				ourBindings.Add(entry);
				Strings.Add("STRINGS.INPUT_BINDINGS." + CATEGORY + ".NAME", CATEGORY_TITLE);
				PUtil.LogDebug("Registered binding: " + entry);
			}
		}

		public static void BuildDisplay(InputBindingsScreen instance, string curScreen) {
			if (curScreen == CATEGORY && ourBindings != null) {
				int num = 0;
				var curBindings = GameInputMapping.KeyBindings;
				var transform = instance.gameObject.transform;
				for (int i = 0; i < curBindings.Length; i++) {
					var binding = curBindings[i];
					GameObject child;
					if (binding.mGroup == CATEGORY && binding.mRebindable &&
							(child = transform.GetChild(num)?.gameObject) != null) {
						// Unless the key bindings changed during the method, we will
						// encounter the bindings in the same order they were added
						var lt = child.transform.GetChild(0).GetComponentInChildren<LocText>();
						PUtil.LogDebug(lt);
						if (lt != null)
							lt.text = ourBindings[num].Action.Title;
						num++;
					}
				}
			}
		}
	}
}
