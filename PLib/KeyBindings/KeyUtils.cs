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
using System.IO;
using System.Text;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Utility functions used in the PLib Action system.
	/// </summary>
	internal static class KeyUtils {
		/// <summary>
		/// The file used to store PLib key bindings.
		/// </summary>
		public static string BINDINGS_FILE {
			get {
				return Path.Combine(Util.RootFolder(), "keybindings_mods.json");
			}
		}

		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		public const string CATEGORY = "PLib";

		/// <summary>
		/// The title used for the PLib key bind category.
		/// </summary>
		public const string CATEGORY_TITLE = "PLib Mods";

		/// <summary>
		/// Checks to see if a key is down.
		/// </summary>
		/// <param name="key_code">The key code to check.</param>
		/// <returns>true if it is down, or false otherwise.</returns>
		private static bool CheckKeyDown(KeyCode key_code) {
			return Input.GetKey(key_code) || Input.GetKeyDown(key_code);
		}

		/// <summary>
		/// Retrieves a Klei key binding title.
		/// </summary>
		/// <param name="category">The category of the key binding.</param>
		/// <param name="item">The key binding to retrieve.</param>
		/// <returns>The Strings entry describing this key binding.</returns>
		public static string GetBindingTitle(string category, string item) {
			return Strings.Get("STRINGS.INPUT_BINDINGS." + category.ToUpper() + "." + item.
				ToUpper());
		}

		/// <summary>
		/// Retrieves the modifier string which describes the specified modifier flags.
		/// </summary>
		/// <param name="modifiers">The modifiers required.</param>
		/// <returns>A string describing those modifiers.</returns>
		public static string GetModifierString(Modifier modifiers) {
			var text = new StringBuilder();
			var options = Enum.GetValues(typeof(Modifier));
			for (int i = 0; i < options.Length; i++) {
				// Modifier string already starts with the key so leading " + " is OK
				var modifier = (Modifier)options.GetValue(i);
				if ((modifiers & modifier) != Modifier.None) {
					text.Append(" + ");
					switch (modifier) {
					case Modifier.Alt:
						text.Append(GameUtil.GetKeycodeLocalized(KKeyCode.LeftAlt));
						break;
					case Modifier.Ctrl:
						text.Append(GameUtil.GetKeycodeLocalized(KKeyCode.LeftControl));
						break;
					case Modifier.Shift:
						text.Append(GameUtil.GetKeycodeLocalized(KKeyCode.LeftShift));
						break;
					case Modifier.CapsLock:
						text.Append(GameUtil.GetKeycodeLocalized(KKeyCode.CapsLock));
						break;
					}
				}
			}
			return text.ToString();
		}

		/// <summary>
		/// Retrieves the modifier keys which are currently pressed.
		/// </summary>
		/// <returns>The pressed modifier keys as a mask.</returns>
		public static Modifier GetModifiersDown() {
			// Check for modifiers
			var modifier = (!CheckKeyDown(KeyCode.LeftAlt) && !CheckKeyDown(KeyCode.
				RightAlt)) ? Modifier.None : Modifier.Alt;
			modifier |= (!CheckKeyDown(KeyCode.LeftControl) && !CheckKeyDown(KeyCode.
				RightControl)) ? Modifier.None : Modifier.Ctrl;
			modifier |= (!CheckKeyDown(KeyCode.LeftShift) && !CheckKeyDown(KeyCode.
				RightShift)) ? Modifier.None : Modifier.Shift;
			modifier |= (!CheckKeyDown(KeyCode.CapsLock)) ? Modifier.None : Modifier.
				CapsLock;
			return modifier;
		}

		/// <summary>
		/// Initializes the global state required for key binding support.
		/// </summary>
		public static void Init() {
			var actions = PSharedData.GetData<IList<object>>(PRegistry.KEY_ACTION_TABLE);
			if (actions != null)
				KeyBindingManager.Instance.AddKeyBindings(actions);
			BindingEntry[] entries = GameInputMapping.DefaultBindings, newEntries;
			// Register a fake binding for a placeholder
			var faker = new BindingEntry(CATEGORY, GamepadButton.NumButtons,
				KKeyCode.KleiKeys, Modifier.None, Action.BUILD_MENU_START_INTERCEPT);
			int n = entries.Length;
			// Append it to the "default" set; it never is mutable so it is never written out
			newEntries = new BindingEntry[n + 1];
			Array.Copy(entries, newEntries, n);
			newEntries[n] = faker;
			GameInputMapping.SetDefaultKeyBindings(newEntries);
			KeyBindingManager.Instance.LoadBindings();
		}

		/// <summary>
		/// Checks to see if a key was just pressed.
		/// </summary>
		/// <param name="keyCode">The key code to check.</param>
		/// <returns>true if it was pressed, or false otherwise.</returns>
		public static bool IsKeyDown(KKeyCode keyCode) {
			bool result = false;
			if (keyCode < KKeyCode.KleiKeys)
				result = Input.GetKeyDown((KeyCode)keyCode);
			else {
				float axis = Input.GetAxis("Mouse ScrollWheel");
				if (keyCode == KKeyCode.MouseScrollDown)
					result = (axis < 0.0f);
				else
					result = (axis > 0.0f);
			}
			return result;
		}

		/// <summary>
		/// Checks to see if a key was just released.
		/// </summary>
		/// <param name="keyCode">The key code to check.</param>
		/// <returns>true if it was released, or false otherwise.</returns>
		public static bool IsKeyUp(KKeyCode keyCode) {
			return keyCode < KKeyCode.KleiKeys && Input.GetKeyUp((KeyCode)keyCode);
		}

		/// <summary>
		/// Logs a message encountered by the PLib key binding system.
		/// </summary>
		/// <param name="message">The message.</param>
		public static void LogKeyBind(string message) {
			Debug.LogFormat("[PKeyBinding] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered by the PLib key binding system.
		/// </summary>
		/// <param name="message">The warning message.</param>
		public static void LogKeyBindWarning(string message) {
			Debug.LogWarningFormat("[PKeyBinding] {0}", message);
		}
	}
}
