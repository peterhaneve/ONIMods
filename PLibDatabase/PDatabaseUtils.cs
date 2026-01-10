/*
 * Copyright 2026 Peter Han
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
using Klei.AI;
using PeterHan.PLib.Detours;

namespace PeterHan.PLib.Database {
	/// <summary>
	/// Functions which deal with entries in the game database and strings.
	/// </summary>
	public static class PDatabaseUtils {
		// Even though these changes were introduced in U51-594211, do not remove even after
		// it is deprecated, as Klei likes to add optional fields
		private delegate AttributeModifier NewModifierFunc(string attributeID,
			float value, Func<string> getDescription, bool multiplier, bool uiOnly);
		
		private delegate AttributeModifier NewModifierString(string attributeID,
			float value, string description, bool multiplier, bool uiOnly, bool readOnly);

		private static readonly NewModifierFunc NEW_MODIFIER_FUNC =
			typeof(AttributeModifier).DetourConstructor<NewModifierFunc>();

		private static readonly NewModifierString NEW_MODIFIER_STRING =
			typeof(AttributeModifier).DetourConstructor<NewModifierString>();

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
		/// Creates an attribute modifier using the attributes that work across multiple game
		/// versions.
		/// </summary>
		/// <param name="attributeID">The attribute ID to modify.</param>
		/// <param name="value">The amount to modify the attribute.</param>
		/// <param name="description">The description to display for the attribute.</param>
		/// <param name="multiplier">If true, the modifier is treated as a multiplier instead of an addition.</param>
		/// <param name="uiOnly">If true, the modifier is only shown in the UI.</param>
		/// <param name="readOnly">If true, the modifier value cannot be changed after creation.</param>
		/// <returns>The created attribute modifier.</returns>
		public static AttributeModifier CreateAttributeModifier(string attributeID,
				float value, string description = null, bool multiplier = false,
				bool uiOnly = false, bool readOnly = true) {
			return NEW_MODIFIER_STRING.Invoke(attributeID, value, description, multiplier,
				uiOnly, readOnly);
		}
		
		/// <summary>
		/// Creates an attribute modifier using the attributes that work across multiple game
		/// versions.
		/// </summary>
		/// <param name="attributeID">The attribute ID to modify.</param>
		/// <param name="value">The amount to modify the attribute.</param>
		/// <param name="getDescription">A function to retrieve the descriptor string.</param>
		/// <param name="multiplier">If true, the modifier is treated as a multiplier instead of an addition.</param>
		/// <param name="uiOnly">If true, the modifier is only shown in the UI.</param>
		/// <returns>The created attribute modifier.</returns>
		public static AttributeModifier CreateAttributeModifier(string attributeID,
				float value, Func<string> getDescription = null, bool multiplier = false,
				bool uiOnly = false) {
			return NEW_MODIFIER_FUNC.Invoke(attributeID, value, getDescription, multiplier,
				uiOnly);
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
