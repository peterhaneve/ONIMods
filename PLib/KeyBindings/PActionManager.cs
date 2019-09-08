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
		/// The old maximum Action limit.
		/// </summary>
		private const int N_ACTIONS = (int)Action.NumActions;

		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		public const string CATEGORY = "PLib";

		/// <summary>
		/// The title used for the PLib key bind category.
		/// </summary>
		public const string CATEGORY_TITLE = "PLib Mods";

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
		/// <returns>The n</returns>
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
			return "STRINGS.INPUT_BINDINGS." + category.ToUpper() + "." + item.ToUpper();
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
		/// The maximum PAction ID.
		/// </summary>
		private int maxPAction;

		/// <summary>
		/// Queued key binds which are resolved on load.
		/// </summary>
		private readonly IDictionary<PAction, PKeyBinding> queueBindKeys;

		internal PActionManager() {
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
			return (maxPAction > 0) ? (maxPAction + N_ACTIONS) : N_ACTIONS - 1;
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
				// Safe to add them without risk of concurrent modification
				PUtil.LogDebug("Registering {0:D} key binds".F(n));
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

		/// <summary>
		/// Applied to GameInputMapping to apply default key bindings for actions.
		/// 
		/// This patch is supposed to be applied for <b>every</b> mod using PLib. Do not move
		/// it to PLibPatches.
		/// </summary>
		[HarmonyPatch(typeof(GameInputMapping), "LoadBindings")]
		public static class GameInputMapping_LoadBindings_Patch {
			/// <summary>
			/// Applied before LoadBindings runs.
			/// </summary>
			private static void Prefix() {
				Instance.ProcessKeyBinds();
			}
		}
	}
}
