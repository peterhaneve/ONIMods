/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Shorthand for declaring new achievements.
	/// </summary>
	internal sealed class AD {
		/// <summary>
		/// Gets the achievement data for the specified achievement ID.
		/// </summary>
		/// <param name="id">The achievement ID.</param>
		/// <returns>The achievement constant data, or null if no achievement with this ID
		/// was found in AchievementStrings.</returns>
		internal static Type GetAchievementData(string id) {
			return typeof(AchievementStrings).GetNestedType(id.ToUpperInvariant(),
				PPatchTools.BASE_FLAGS | BindingFlags.Static);
		}

		/// <summary>
		/// Retrieves string data (possibly localized) from the achievement strings.
		/// </summary>
		/// <param name="key">The string name to retrieve.</param>
		/// <returns>The value of that string.</returns>
		internal static string GetStringValue(Type type, string key) {
			string value = "";
			if (type?.GetFieldSafe(key, true)?.GetValue(null) is LocString field)
				value = field.text;
			return value;
		}

		/// <summary>
		/// The achievement ID.
		/// </summary>
		public string ID { get; }

		/// <summary>
		/// The achievement icon name.
		/// </summary>
		public string Icon { get; }

		/// <summary>
		/// The requirements for this achievement.
		/// </summary>
		private readonly ColonyAchievementRequirement[] requirements;

		public AD(string id, string icon, params ColonyAchievementRequirement[] requirements) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException("id");
			if (requirements == null || requirements.Length < 1)
				throw new ArgumentException("No requirements for colony achievement");
			ID = id;
			this.requirements = requirements;
			Icon = string.IsNullOrEmpty(icon) ? id : icon;
		}

		/// <summary>
		/// Creates a colony achievement object.
		/// </summary>
		/// <returns>The colony achievement for this descriptor.</returns>
		public PColonyAchievement GetColonyAchievement() {
			// Get strings from the AchievementStrings class
			var type = GetAchievementData(ID);
			return new PColonyAchievement(ID) {
				Name = GetStringValue(type, "NAME"),
				Description = GetStringValue(type, "DESC"),
				Icon = Icon,
				Requirements = new List<ColonyAchievementRequirement>(requirements)
			};
		}
	}
}
