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

using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.PLib {
	/// <summary>
	/// Manages PAction functionality which must be single instance.
	/// </summary>
	internal sealed class PActionManager {
		/// <summary>
		/// PAction is a singleton which is only actually used in the active version of PLib.
		/// </summary>
		internal static PActionManager Instance { get; } = new PActionManager();

		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		public const string CATEGORY = "PLib";

		/// <summary>
		/// The title used for the PLib key bind category.
		/// </summary>
		public const string CATEGORY_TITLE = "PLib Mods";

		/// <summary>
		/// Adds a patch on GameInputMapping.LoadBindings to register key bindings for this
		/// mod. The typical table approach in PSharedData does not work, because LoadBindings
		/// executes after mods load but before PLib patches are applied...
		/// </summary>
		private static void AddActionManager() {
			var hi = HarmonyInstance.Create("PKeyBinding_" + (Assembly.GetExecutingAssembly().
				GetNameSafe() ?? "Unknown"));
			hi.Patch(typeof(GameInputMapping).GetMethodSafe(nameof(GameInputMapping.
				LoadBindings), true), new HarmonyMethod(typeof(PActionManager),
				nameof(RunActionManagers)), null);
		}

		/// <summary>
		/// Configures the action entry in the strings database to display its title properly.
		/// </summary>
		/// <param name="action">The action to configure.</param>
		internal static void ConfigureTitle(PAction action) {
			Strings.Add(GetBindingTitle(CATEGORY, action.GetKAction().ToString()), action.
				Title);
		}

		/// <summary>
		/// Extends the action flags array to the new maximum length.
		/// </summary>
		/// <param name="oldActionFlags">The old flags array.</param>
		/// <param name="newMax">The minimum length.</param>
		/// <returns>The new action flags array.</returns>
		internal static bool[] ExtendFlags(bool[] oldActionFlags, int newMax) {
			int n = oldActionFlags.Length;
			bool[] newActionFlags;
			if (n < newMax) {
				newActionFlags = new bool[newMax];
				Array.Copy(oldActionFlags, newActionFlags, n);
			} else
				newActionFlags = oldActionFlags;
			return newActionFlags;
		}

		/// <summary>
		/// Retrieves a Klei key binding title.
		/// </summary>
		/// <param name="category">The category of the key binding.</param>
		/// <param name="item">The key binding to retrieve.</param>
		/// <returns>The Strings entry describing this key binding.</returns>
		private static string GetBindingTitle(string category, string item) {
			return "STRINGS.INPUT_BINDINGS." + category.ToUpperInvariant() + "." + item.
				ToUpperInvariant();
		}

		/// <summary>
		/// Retrieves a "localized" (if PLib is localized) description of additional key codes
		/// from the KKeyCode enumeration, to avoid warning spam on popular keybinds like
		/// arrow keys, delete, home, and so forth.
		/// </summary>
		/// <param name="code">The key code.</param>
		/// <returns>A description of that key code, or null if no localization is found.</returns>
		internal static string GetExtraKeycodeLocalized(KKeyCode code) {
			string localCode = null;
			switch (code) {
			case KKeyCode.Home:
				localCode = KeyCodeStrings.HOME;
				break;
			case KKeyCode.End:
				localCode = KeyCodeStrings.END;
				break;
			case KKeyCode.Delete:
				localCode = KeyCodeStrings.DELETE;
				break;
			case KKeyCode.PageDown:
				localCode = KeyCodeStrings.PAGEDOWN;
				break;
			case KKeyCode.PageUp:
				localCode = KeyCodeStrings.PAGEUP;
				break;
			case KKeyCode.LeftArrow:
				localCode = KeyCodeStrings.ARROWLEFT;
				break;
			case KKeyCode.UpArrow:
				localCode = KeyCodeStrings.ARROWUP;
				break;
			case KKeyCode.RightArrow:
				localCode = KeyCodeStrings.ARROWRIGHT;
				break;
			case KKeyCode.DownArrow:
				localCode = KeyCodeStrings.ARROWDOWN;
				break;
			case KKeyCode.Pause:
				localCode = KeyCodeStrings.PAUSE;
				break;
			case KKeyCode.SysReq:
				localCode = KeyCodeStrings.SYSRQ;
				break;
			case KKeyCode.Print:
				localCode = KeyCodeStrings.PRTSCREEN;
				break;
			default:
				break;
			}
			return localCode;
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

		/// <summary>
		/// Executes all registered action managers from any mod.
		/// </summary>
		internal static void RunActionManagers() {
			Instance?.ProcessKeyBinds();
		}

		/// <summary>
		/// Whether the action manager has been patched in.
		/// </summary>
		private bool Added { get; set; }

		/// <summary>
		/// The maximum PAction ID.
		/// </summary>
		private int maxPAction;

		/// <summary>
		/// Queued key binds which are resolved on load.
		/// </summary>
		private readonly IDictionary<PAction, PKeyBinding> queueBindKeys;

		internal PActionManager() {
			Added = false;
			maxPAction = 0;
			queueBindKeys = new Dictionary<PAction, PKeyBinding>(8);
		}

		/// <summary>
		/// Returns the maximum int equivalent value that an Action enum can contain when
		/// including custom actions. If no actions are defined, returns NumActions - 1 since
		/// NumActions is reserved in the base game.
		/// </summary>
		/// <returns>The maximum int index for an Action.</returns>
		public int GetMaxAction() {
			int nActions = (int)PAction.MaxAction;
			return (maxPAction > 0) ? (maxPAction + nActions) : nActions - 1;
		}

		/// <summary>
		/// Initializes the global state required for key binding support.
		/// </summary>
		public void Init() {
			Strings.Add(GetBindingTitle(CATEGORY, "NAME"), CATEGORY_TITLE);
			UpdateMaxAction();
		}

		/// <summary>
		/// Processes any queued key binds.
		/// </summary>
		private void ProcessKeyBinds() {
			int n = queueBindKeys.Count;
			if (n > 0 && GameInputMapping.DefaultBindings != null) {
				string name = Assembly.GetExecutingAssembly().GetNameSafe() ?? "?";
				// Safe to add them without risk of concurrent modification
				LogKeyBind("Registering {0:D} key bind(s) for mod {1}".F(n, name));
				foreach (var pair in queueBindKeys)
					pair.Key.AddKeyBinding(pair.Value);
				queueBindKeys.Clear();
			}
		}

		/// <summary>
		/// Adds a key bind command to the pending queue.
		/// </summary>
		/// <param name="action">The action to bind.</param>
		/// <param name="binding">The key to bind it to.</param>
		internal void QueueKeyBind(PAction action, PKeyBinding binding) {
			if (!Added) {
				AddActionManager();
				Added = true;
			}
			queueBindKeys[action] = binding;
		}

		/// <summary>
		/// Updates the action system based on the current actions registered.
		/// </summary>
		public void UpdateMaxAction() {
			object locker = PSharedData.GetData<object>(PRegistry.KEY_ACTION_LOCK);
			if (locker != null)
				lock (locker) {
					maxPAction = Math.Max(0, PSharedData.GetData<int>(PRegistry.
						KEY_ACTION_ID));
				}
		}
	}
}
