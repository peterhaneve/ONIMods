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
	/// An Action managed by PLib. Actions have key bindings assigned to them.
	/// </summary>
	public sealed class PAction {
		/// <summary>
		/// Wraps ToolMenu.CreateToolCollection to accept a PAction instead of an Action.
		/// </summary>
		public static ToolMenu.ToolCollection CreateToolCollection(LocString collectionName,
				string iconName, PAction action, string toolName, LocString tooltip,
				bool largeIcon) {
			if (action == null)
				throw new ArgumentNullException("action");
			/*
			 * Fill in the current hotkey; the base game does not update these if bindings are
			 * changed mid-game, so we will not either!
			 */
			string desc = (tooltip == null) ? string.Empty : PKeyBinding.ReplaceHotkeyString(
				tooltip, action.GetCurrentKeyBinding());
			var collection = ToolMenu.CreateToolCollection(collectionName, iconName, Action.
				BUILD_MENU_START_INTERCEPT, toolName, desc, largeIcon);
			// Track the key binds with a component
			var hkc = ToolMenu.Instance.gameObject.AddOrGet<ToolMenuHotkeyComponent>();
			hkc.Add(action, collection);
			return collection;
		}

		/// <summary>
		/// Registers a PAction with the action manager. There is no corresponding Unregister
		/// call, so avoid spamming PActions.
		/// </summary>
		/// <param name="identifier">The identifier for this action.</param>
		/// <param name="title">The action's title.</param>
		/// <param name="key">The default key binding for this action.</param>
		/// <returns>The action thus registered.</returns>
		/// <exception cref="InvalidOperationException">If PLib is not yet initialized.</exception>
		public static PAction Register(string identifier, LocString title, PKeyBinding key =
				null) {
			object locker = PSharedData.GetData<object>(PRegistry.KEY_ACTION_LOCK);
			int actionID;
			if (locker == null)
				throw new InvalidOperationException("PAction.Register called before PLib loaded!");
			PAction action;
			lock (locker) {
				actionID = PSharedData.GetData<int>(PRegistry.KEY_ACTION_ID);
				if (actionID <= 0)
					throw new InvalidOperationException("PAction action ID is not set!");
				PSharedData.PutData(PRegistry.KEY_ACTION_ID, actionID + 1);
				var actionList = PSharedData.GetData<IList<object>>(PRegistry.
					KEY_ACTION_TABLE);
				// Initialize the action list if needed
				if (actionList == null) {
					actionList = new List<object>(32);
					PSharedData.PutData(PRegistry.KEY_ACTION_TABLE, actionList);
				}
				action = new PAction(actionID, identifier, title, key ?? new PKeyBinding());
				actionList.Add(action);
			}
			return action;
		}

		/// <summary>
		/// Action which can be called to trigger all key down events.
		/// </summary>
		internal System.Action DoKeyDown { get; }

		/// <summary>
		/// Action which can be called to trigger all key up events.
		/// </summary>
		internal System.Action DoKeyUp { get; }

		/// <summary>
		/// The currently bound gamepad button.
		/// </summary>
		internal GamepadButton GamePadButton { get; set; }

		/// <summary>
		/// The action's non-localized identifier. Something like YOURMOD.CATEGORY.ACTIONNAME.
		/// </summary>
		public string Identifier { get; }

		/// <summary>
		/// The action's ID. This ID is assigned internally upon register and used for PLib
		/// indexing. Even if you somehow obtain it in your mod, it is not to be used!
		/// </summary>
		private int ID { get; }

		/// <summary>
		/// The currently bound key code.
		/// </summary>
		internal KKeyCode Key { get; set; }

		/// <summary>
		/// The currently bound modifier code.
		/// </summary>
		internal Modifier Modifiers { get; set; }

		/// <summary>
		/// The event called when the triggering key is pressed.
		/// </summary>
		public event System.Action KeyDown;

		/// <summary>
		/// The event called when the triggering key is released.
		/// </summary>
		public event System.Action KeyUp;

		/// <summary>
		/// The action's title.
		/// </summary>
		public LocString Title { get; }

		private PAction(int id, string identifier, LocString title, PKeyBinding binding) {
			Identifier = identifier;
			ID = id;
			Title = title;
			GamePadButton = binding.GamePadButton;
			Key = binding.Key;
			Modifiers = binding.Modifiers;
			DoKeyDown = TriggerKeyDown;
			DoKeyUp = TriggerKeyUp;
		}

		public override bool Equals(object obj) {
			return (obj is PAction other) && other.ID == ID;
		}

		/// <summary>
		/// Retrieves the currently assigned key binding of this action.
		/// </summary>
		/// <returns>The current key binding.</returns>
		public PKeyBinding GetCurrentKeyBinding() {
			return new PKeyBinding(Key, Modifiers, GamePadButton);
		}

		public override int GetHashCode() {
			return ID;
		}

		public override string ToString() {
			return "PAction[" + Identifier + "]: " + Title;
		}

		/// <summary>
		/// Invokes all handlers registered for key presses.
		/// </summary>
		private void TriggerKeyDown() {
			KeyDown?.Invoke();
		}

		/// <summary>
		/// Invokes all handlers registered for key releases.
		/// </summary>
		private void TriggerKeyUp() {
			KeyUp?.Invoke();
		}

		/// <summary>
		/// A component which lives in a tool collection and (while it is alive) includes a
		/// listener to activate the tool.
		/// </summary>
		private sealed class ToolMenuHotkeyComponent : MonoBehaviour {
			/// <summary>
			/// The actions which need to be cleaned up.
			/// </summary>
			private readonly IDictionary<PAction, System.Action> actions;

			public ToolMenuHotkeyComponent() {
				actions = new Dictionary<PAction, System.Action>(8);
			}

			/// <summary>
			/// Adds an action to trigger a tool menu.
			/// </summary>
			/// <param name="trigger">The triggering Action.</param>
			/// <param name="response">The tool to open.</param>
			internal void Add(PAction trigger, ToolMenu.ToolCollection response) {
				if (trigger == null)
					throw new ArgumentNullException("trigger");
				if (response == null)
					throw new ArgumentNullException("response");
				if (!actions.ContainsKey(trigger)) {
					void openTool() {
						var instance = ToolMenu.Instance;
						if (instance != null)
							Traverse.Create(instance).CallMethod("ChooseCollection", response,
								true);
					};
					trigger.KeyDown += openTool;
					actions.Add(trigger, openTool);
				}
			}

			public void OnDestroy() {
				// Stop listening
				foreach (var pair in actions)
					pair.Key.KeyDown -= pair.Value;
				actions.Clear();
			}
		}
	}
}
