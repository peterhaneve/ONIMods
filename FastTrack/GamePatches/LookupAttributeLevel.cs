/*
 * Copyright 2024 Peter Han
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

using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A hacked extension of Klei.AI.AttributeLevel to quickly look up other attributes.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class LookupAttributeLevel : AttributeLevel {
		/// <summary>
		/// Should not be serialized, has a placeholder name
		/// </summary>
		public const string ID = "__LOOKUP";

		/// <summary>
		/// Why did you have to duplicate a system class name, Clay please!!!
		/// </summary>
		internal static readonly Klei.AI.Attribute LOOKUP_ATTR = new Klei.AI.Attribute(ID,
			false, Klei.AI.Attribute.Display.Never, false);

		/// <summary>
		/// Gets the fast attribute level lookup, or creates them if they do not exist.
		/// </summary>
		/// <param name="levels">The attribute levels to query.</param>
		/// <returns>The fake level to use for looking them up.</returns>
		public static LookupAttributeLevel GetAttributeLookup(AttributeLevels levels) {
			LookupAttributeLevel lookup = null;
			if (levels != null) {
				var levelList = levels.levels;
				if (!(levelList[0] is LookupAttributeLevel lol)) {
					lol = new LookupAttributeLevel(levels.gameObject, levels);
					levelList.Insert(0, lol);
				}
				lookup = lol;
			}
			return lookup;
		}

		/// <summary>
		/// A cached lookup of attribute names to levels.
		/// </summary>
		private readonly IDictionary<string, AttributeLevel> attrLevels;

		private readonly AttributeLevels levels;
		
		/// <summary>
		/// The current training speed multiplier.
		/// </summary>
		private AttributeConverterInstance trainingSpeed;

		internal LookupAttributeLevel(GameObject go, AttributeLevels levels) : base(
				new AttributeInstance(go, LOOKUP_ATTR)) {
			if (go == null)
				throw new ArgumentNullException(nameof(go));
			if (levels == null)
				throw new ArgumentNullException(nameof(levels));
			this.levels = levels;
			// Levels cannot be added after prefab init in current Klei flow
			var existing = levels.levels;
			int n = existing.Count;
			attrLevels = new Dictionary<string, AttributeLevel>(n);
			for (int i = 0; i < n; i++) {
				var level = existing[i];
				attrLevels.Add(level.attribute.Id, level);
			}
			trainingSpeed = null;
		}

		/// <summary>
		/// Adds experience to the Duplicant.
		/// </summary>
		/// <param name="id">The attribute ID to gain experience.</param>
		/// <param name="time">The time spent training the attribute.</param>
		/// <param name="multiplier">The experience multiplier.</param>
		/// <returns>true if the Duplicant leveled up, or false otherwise.</returns>
		public bool AddExperience(string id, float time, float multiplier) {
			bool result = false;
			if (id != null && attrLevels.TryGetValue(id, out var attrLevel)) {
				float effectiveTime = time * multiplier;
				var ts = trainingSpeed;
				if (ts == null && levels.TryGetComponent(out AttributeConverters converters))
					trainingSpeed = ts = converters.Get(Db.Get().AttributeConverters.
						TrainingSpeed);
				if (ts != null)
					effectiveTime += effectiveTime * ts.Evaluate();
				result = attrLevel.AddExperience(levels, effectiveTime);
				attrLevel.Apply(levels);
			}
			return result;
		}

		/// <summary>
		/// Gets the Duplicant's current attribute level.
		/// </summary>
		/// <param name="id">The attribute ID to look up.</param>
		/// <returns>The attribute's level, or 1 if the attribute was not found.</returns>
		public AttributeLevel GetAttributeLevel(string id) {
			if (id == null || !attrLevels.TryGetValue(id, out var attrLevel))
				attrLevel = null;
			return attrLevel;
		}

		/// <summary>
		/// Gets the Duplicant's current attribute level.
		/// </summary>
		/// <param name="attribute">The attribute to look up.</param>
		/// <returns>The attribute's level, or 1 if the attribute was not found.</returns>
		public int GetLevel(Klei.AI.Attribute attribute) {
			int level = 1;
			if (attribute != null && attrLevels.TryGetValue(attribute.Id, out var attrLevel))
				level = attrLevel.GetLevel();
			return level;
		}

		/// <summary>
		/// Sets the Duplicant's current attribute experience. Only used in OnDeserialized.
		/// </summary>
		/// <param name="id">The attribute ID to look up.</param>
		/// <param name="experience">The attribute experience points to set.</param>
		public void SetExperience(string id, float experience) {
			if (id != null && attrLevels.TryGetValue(id, out var attrLevel)) {
				attrLevel.SetExperience(experience);
				attrLevel.Apply(levels);
			}
		}

		/// <summary>
		/// Sets the Duplicant's current attribute level. Only used in OnDeserialized and
		/// MinionStartingStats.
		/// </summary>
		/// <param name="id">The attribute ID to look up.</param>
		/// <param name="level">The new attribute level.</param>
		public void SetLevel(string id, int level) {
			if (id != null && attrLevels.TryGetValue(id, out var attrLevel)) {
				attrLevel.SetLevel(level);
				attrLevel.Apply(levels);
			}
		}
	}
}
