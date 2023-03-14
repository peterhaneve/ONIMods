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

using Database;
using System;

namespace PeterHan.PLib.Database {
	/// <summary>
	/// Functions which deal with entries in the game database and strings.
	/// </summary>
	public static class PDatabaseUtils {
		/// <summary>
		/// Adds a colony achievement to the colony summary screen. Must be invoked after the
		/// database is initialized (Db.Initialize() postfix recommended).
		/// 
		/// Note that achievement structures significantly changed from Vanilla to the DLC.
		/// </summary>
		/// <param name="achievement">The achievement to add.</param>
		public static void AddColonyAchievement(ColonyAchievement achievement) {
			if (achievement == null)
				throw new ArgumentNullException(nameof(achievement));
			Db.Get()?.ColonyAchievements?.resources?.Add(achievement);
		}

		/// <summary>
		/// Adds the name and description for a status item.
		/// 
		/// Must be used before the StatusItem is first instantiated.
		/// </summary>
		/// <param name="id">The status item ID.</param>
		/// <param name="category">The status item category.</param>
		/// <param name="name">The name to display in the UI.</param>
		/// <param name="desc">The description to display in the UI.</param>
		public static void AddStatusItemStrings(string id, string category, string name,
				string desc) {
			string uid = id.ToUpperInvariant();
			string ucategory = category.ToUpperInvariant();
			Strings.Add("STRINGS." + ucategory + ".STATUSITEMS." + uid + ".NAME", name);
			Strings.Add("STRINGS." + ucategory + ".STATUSITEMS." + uid + ".TOOLTIP", desc);
		}

		/// <summary>
		/// Logs a message encountered by the PLib database system.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogDatabaseDebug(string message) {
			Debug.LogFormat("[PLibDatabase] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered by the PLib database system.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogDatabaseWarning(string message) {
			Debug.LogWarningFormat("[PLibDatabase] {0}", message);
		}
	}
}
