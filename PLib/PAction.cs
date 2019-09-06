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
using System.Reflection;

namespace PeterHan.PLib {
	/// <summary>
	/// An Action managed by PLib.
	/// </summary>
	public sealed class PAction {
		/// <summary>
		/// Registers a PAction.
		/// </summary>
		/// <param name="identifier">The identifier for this action.</param>
		/// <param name="title">The action's title.</param>
		/// <returns>The action thus registered.</returns>
		/// <exception cref="InvalidOperationException">If PLib is not yet initialized.</exception>
		public static PAction Register(string identifier, LocString title) {
			object locker = PSharedData.GetData<object>(PRegistry.KEY_ACTION_LOCK);
			int actionID;
			if (locker == null)
				throw new InvalidOperationException("PAction.Register called before PLib loaded!");
			lock (locker) {
				actionID = PSharedData.GetData<int>(PRegistry.KEY_ACTION_ID);
				if (actionID <= 0)
					throw new InvalidOperationException("PAction action ID is not set!");
				PSharedData.PutData(PRegistry.KEY_ACTION_ID, actionID + 1);
			}
			return new PAction(actionID, identifier, title);
		}

		/// <summary>
		/// The action's non-localized identifier. Something like YOURMOD.CATEGORY.ACTIONNAME.
		/// </summary>
		public string Identifier { get; }

		/// <summary>
		/// The action's ID. This ID is assigned internally upon register and used for PLib
		/// indexing. Even if you somehow obtain it in your mod, it is not to be used!
		/// </summary>
		int ID { get; }

		/// <summary>
		/// The action's title.
		/// </summary>
		public LocString Title { get; }

		private PAction(int id, string identifier, LocString title) {
			Identifier = identifier;
			ID = id;
			Title = title;
		}

		public override bool Equals(object obj) {
			// Allow comparisons with PAction from other assemblies
			bool equals = false;
			if (obj != null) {
				var type = obj.GetType();
				if (obj is PAction other)
					equals = (other.ID == ID);
				else if (type.FullName == typeof(PAction).FullName) {
					// Use reflection to grab the property value
					var idProp = type.GetProperty("ID", BindingFlags.NonPublic | BindingFlags.
						Instance);
					try {
						equals = (idProp != null && (int)idProp.GetValue(obj, null) == ID);
					} catch (InvalidCastException) {
					} catch (AmbiguousMatchException) {
					} catch (TargetInvocationException) { }
				}
			}
			return equals;
		}

		public override int GetHashCode() {
			return ID;
		}

		public override string ToString() {
			return "PAction[" + Identifier + "]";
		}
	}
}
