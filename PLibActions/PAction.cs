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

namespace PeterHan.PLib.Actions {
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
			if (!Enum.TryParse(nameof(Action.NumActions), out Action limit))
				limit = Action.NumActions;
			MaxAction = limit;
		}

		/// <summary>
		/// The default key binding for this action. Not necessarily the current key binding.
		/// </summary>
		internal PKeyBinding DefaultBinding { get; }

		/// <summary>
		/// The action's non-localized identifier. Something like YOURMOD.CATEGORY.ACTIONNAME.
		/// </summary>
		public string Identifier { get; }

		/// <summary>
		/// The action's ID. This ID is assigned internally upon register and used for PLib
		/// indexing. Even if you somehow obtain it in your mod, it is not to be used!
		/// </summary>
		private readonly int id;

		/// <summary>
		/// The action's title.
		/// </summary>
		public LocString Title { get; }

		internal PAction(int id, string identifier, LocString title, PKeyBinding binding) {
			if (id <= 0)
				throw new ArgumentOutOfRangeException(nameof(id));
			DefaultBinding = binding;
			Identifier = identifier;
			this.id = id;
			Title = title;
		}

		public override bool Equals(object obj) {
			return (obj is PAction other) && other.id == id;
		}

		/// <summary>
		/// Retrieves the Klei action for this PAction.
		/// </summary>
		/// <returns>The Klei action for use in game functions.</returns>
		public Action GetKAction() {
			return (Action)((int)MaxAction + id);
		}

		public override int GetHashCode() {
			return id;
		}

		public override string ToString() {
			return "PAction[" + Identifier + "]: " + Title;
		}
	}
}
