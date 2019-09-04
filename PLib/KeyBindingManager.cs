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

using Harmony;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Manages the addition of key bindings to the game with a method that can be configured
	/// in the InputBindingsScreen.
	/// 
	/// While this class loads and patches InputBindingsScreen once for every mod that
	/// uses its own copy of PLib, the instances are designed to work together and not stomp
	/// on each other.
	/// </summary>
	public static class KeyBindingManager {
#if false
		/// <summary>
		/// The "building" category used for building enable, copy settings, and so forth.
		/// </summary>
		public const string CategoryBuilding = "Building";
		/// <summary>
		/// The "cinematic camera" category.
		/// </summary>
		public const string CategoryCamera = "CinematicCamera";
		/// <summary>
		/// The "debug" category, used for debug mode tools.
		/// </summary>
		public const string CategoryDebug = "Debug";
		/// <summary>
		/// The "buildings menu" category used for hot key building specific buildings.
		/// </summary>
		public const string CategoryMenu = "BuildingsMenu";
		/// <summary>
		/// The "navigation" category, used for swapping between user views.
		/// </summary>
		public const string CategoryNavigation = "Navigation";
		/// <summary>
		/// The "root" category, used for most basic buttons such as W/A/S/D, tool bindings,
		/// and overlay selection.
		/// </summary>
		public const string CategoryRoot = "Root";
		/// <summary>
		/// The "sandbox" category, used for sandbox mode tools.
		/// </summary>
		public const string CategorySandbox = "Sandbox";
		/// <summary>
		/// The "tool" category, used for drag straight and rotate.
		/// </summary>
		public const string CategoryTool = "Tool";
#endif
		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		private const string CATEGORY = "PLib";

		/// <summary>
		/// The bindings registered in this way.
		/// </summary>
		private static IList<PLibKeyBinding> ourBindings;

		/// <summary>
		/// Applied to BuildDisplay to actually give us the right titles on the PLib tab.
		/// </summary>
		[HarmonyPatch(typeof(InputBindingsScreen), "BuildDisplay")]
		public static class InputBindingsScreen_BuildDisplay_Patch {
			/// <summary>
			/// Applied after BuildDisplay runs.
			/// </summary>
			private static void Postfix(ref InputBindingsScreen __instance,
					ref List<string> ___screens, int ___activeScreen,
					ref LocText ___screenTitle) {
				string curScreen = ___screens[___activeScreen];
				if (curScreen == CATEGORY && ourBindings != null) {
					int num = 0;
					var curBindings = GameInputMapping.KeyBindings;
					var transform = __instance.gameObject.transform;
					// Update heading
					___screenTitle.text = CATEGORY;
					for (int i = 0; i < curBindings.Length; i++) {
						var binding = curBindings[i];
						GameObject child;
						if (binding.mGroup == CATEGORY && binding.mRebindable &&
								(child = transform.GetChild(num)?.gameObject) != null) {
							// Unless the key bindings changed during the method, we will
							// encounter the bindings in the same order they were added
							var lt = child.transform.GetChild(0).GetComponentInChildren<LocText>();
							PLibUtil.LogDebug(lt);
							if (lt != null)
								lt.text = ourBindings[num].Title;
							num++;
						}
					}
				}
			}
		}

		/// <summary>
		/// Adds a key binding to the key input screen.
		/// </summary>
		/// <param name="entry">The key binding to add.</param>
		public static void AddKeyBinding(PLibKeyBinding entry) {
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
					ourBindings = new List<PLibKeyBinding>(8);
				ourBindings.Add(entry);
				PLibUtil.LogDebug("Registered binding: " + entry);
			}
		}
	}
}
