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

using System;

namespace PeterHan.PLib {
	/// <summary>
	/// An Action managed by PLib. Actions have key bindings assigned to them.
	/// </summary>
	public sealed class PAction {
		/// <summary>
		/// The maximum action value (typically used to mean "no action") used in the currently
		/// running instance of the game.
		/// 
		/// Since Action is compiled to a const int when a mod is built, any changes to the
		/// Action enum will break direct references to Action.NumActions. Use this property
		/// instead to always use the intended "no action" value.
		/// </summary>
		public static Action MaxAction { get; }

		static PAction() {
			// Enum.TryParse was introduced in .NET Framework 4.0
			if (!Enum.TryParse("NumActions", out Action limit))
				limit = Action.NumActions;
			MaxAction = limit;
		}

		/// <summary>
		/// Registers a PAction with the action manager. There is no corresponding Unregister
		/// call, so avoid spamming PActions.
		/// 
		/// This call should occur after PUtil.InitLibrary() during the mod OnLoad(). If called
		/// earlier, it may fail with InvalidOperationException, and if called later, the
		/// user's custom key bind (if applicable) will be discarded.
		/// </summary>
		/// <param name="identifier">The identifier for this action.</param>
		/// <param name="title">The action's title. To localize the action's title, use the
		/// UpdateLocalizedTitle method on the PAction instance in a postload patch (which
		/// runs after localization mods have been loaded)</param>
		/// <param name="binding">The default key binding for this action.</param>
		/// <returns>The action thus registered.</returns>
		/// <exception cref="InvalidOperationException">If PLib is not yet initialized.</exception>
		public static PAction Register(string identifier, LocString title,
				PKeyBinding binding = null) {
			// In case this call is used before the library was initialized
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"PAction.Register!");
			}
			int actionID;
			PAction action;
			// Avoid any future threading issues
			lock (PSharedData.GetLock(PRegistry.KEY_ACTION_LOCK)) {
				actionID = PSharedData.GetData<int>(PRegistry.KEY_ACTION_ID);
				if (actionID <= 0)
					throw new InvalidOperationException("PAction action ID is not set!");
				PSharedData.PutData(PRegistry.KEY_ACTION_ID, actionID + 1);
			}
			action = new PAction(actionID, identifier, title);
			if (!string.IsNullOrEmpty(title))
				PActionManager.ConfigureTitle(action);
			action.AddKeyBinding(binding ?? new PKeyBinding());
			return action;
		}

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
		/// The action's title.
		/// </summary>
		public LocString Title { get; }

		private PAction(int id, string identifier, LocString title) {
			Identifier = identifier;
			ID = id;
			Title = title;
		}

		/// <summary>
		/// Adds a key binding to the game for this custom Action. It must be done after mods
		/// are loaded.
		/// </summary>
		/// <param name="binding">The default key binding for this action.</param>
		internal void AddKeyBinding(PKeyBinding binding) {
			var currentBindings = GameInputMapping.DefaultBindings;
			if (binding == null)
				throw new ArgumentNullException("binding");
			if (currentBindings != null) {
				// Only if GameInputMapping is initialized
				Action action = GetKAction();
				bool inBindings = false;
				int n = currentBindings.Length;
				for (int i = 0; i < n && !inBindings; i++) {
					var cb = currentBindings[i];
					if (cb.mAction == action) {
						// Already exists, but it really should not
						PActionManager.LogKeyBindWarning(("Action {0} already exists; " +
							"assigned to KeyCode {1}").F(action, cb.mKeyCode));
						inBindings = true;
						break;
					}
				}
				if (!inBindings) {
					var newBindings = new BindingEntry[n + 1];
					Array.Copy(currentBindings, newBindings, n);
					newBindings[n] = new BindingEntry(PActionManager.CATEGORY, binding.
						GamePadButton, binding.Key, binding.Modifiers, action, true, false);
					GameInputMapping.SetDefaultKeyBindings(newBindings);
				}
			} else
				// Queue into PActionManager
				PActionManager.Instance.QueueKeyBind(this, binding);
		}

		public override bool Equals(object obj) {
			return (obj is PAction other) && other.ID == ID;
		}

		/// <summary>
		/// Retrieves the Klei action for this PAction.
		/// </summary>
		/// <returns>The Klei action for use in game functions.</returns>
		public Action GetKAction() {
			return (Action)((int)MaxAction + ID);
		}

		public override int GetHashCode() {
			return ID;
		}

		public override string ToString() {
			return "PAction[" + Identifier + "]: " + Title;
		}

		/// <summary>
		/// Updates the localized title of this action as shown in the Key Bindings window.
		/// 
		/// If you localize your key bindings, use this method in a postload patch to update
		/// the action title after all mods (including translation mods) have loaded.
		/// </summary>
		/// <param name="newTitle">The new localized title to apply.</param>
		public void UpdateLocalizedTitle(LocString newTitle) {
			if (newTitle != null && !string.IsNullOrEmpty(newTitle.text))
				PActionManager.ConfigureTitle(new PAction(ID, Identifier, newTitle));
		}
	}
}
