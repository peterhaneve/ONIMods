/*
 * Copyright 2022 Peter Han
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
using System.Collections.Generic;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Speeds up a bunch of silly linear searches in AttributeConverters.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class FastAttributeConverters : KMonoBehaviour {
#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private AttributeConverters converters;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// A cached lookup of attribute converter names to converters.
		/// </summary>
		private readonly IDictionary<string, AttributeConverterInstance> attrConverters;

		internal FastAttributeConverters() {
			attrConverters = new Dictionary<string, AttributeConverterInstance>(32);
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			foreach (var instance in converters.converters)
				attrConverters.Add(instance.converter.Id, instance);
		}

		/// <summary>
		/// Gets an attribute converter instance.
		/// </summary>
		/// <param name="converter">The attribute converter to look up.</param>
		/// <returns>The instance of that converter for this Duplicant.</returns>
		public AttributeConverterInstance Get(AttributeConverter converter) {
			if (converter == null || !attrConverters.TryGetValue(converter.Id,
					out AttributeConverterInstance instance))
				instance = null;
			return instance;
		}

		/// <summary>
		/// Gets an attribute converter instance by its ID.
		/// </summary>
		/// <param name="id">The attribute converter's ID.</param>
		/// <returns>The instance of that converter ID for this Duplicant.</returns>
		public AttributeConverterInstance GetConverter(string id) {
			if (id == null || !attrConverters.TryGetValue(id, out AttributeConverterInstance
					instance))
				instance = null;
			return instance;
		}
	}

	/// <summary>
	/// Speeds up a bunch of silly linear searches in AttributeLevels.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class FastAttributeLevels : KMonoBehaviour {
#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private AttributeConverters converters;

		[MyCmpReq]
		private AttributeLevels levels;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// A cached lookup of attribute names to levels.
		/// </summary>
		private readonly IDictionary<string, AttributeLevel> attrLevels;

		/// <summary>
		/// The current training speed multiplier.
		/// </summary>
		private AttributeConverterInstance trainingSpeed;

		internal FastAttributeLevels() {
			attrLevels = new Dictionary<string, AttributeLevel>(32);
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
			if (id != null && attrLevels.TryGetValue(id, out AttributeLevel attrLevel)) {
				float effectiveTime = time * multiplier;
				if (trainingSpeed != null)
					effectiveTime += effectiveTime * trainingSpeed.Evaluate();
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
			if (id == null || attrLevels.TryGetValue(id, out AttributeLevel attrLevel))
				attrLevel = null;
			return attrLevel;
		}

		/// <summary>
		/// Gets the Duplicant's current attribute level.
		/// </summary>
		/// <param name="attribute">The attribute to look up.</param>
		/// <returns>The attribute's level, or 1 if the attribute was not found.</returns>
		public int GetLevel(Attribute attribute) {
			int level = 1;
			if (attribute != null && attrLevels.TryGetValue(attribute.Id, out AttributeLevel
					attrLevel))
				level = attrLevel.GetLevel();
			return level;
		}

		/// <summary>
		/// Initializes the level lookup table.
		/// </summary>
		/// <param name="rawLevels">The levels taken from the stock AttributeLevels object.</param>
		internal void Initialize(IList<AttributeLevel> rawLevels) {
			foreach (var level in rawLevels)
				attrLevels.Add(level.attribute.Id, level);
			trainingSpeed = converters.Get(Db.Get().AttributeConverters.TrainingSpeed);
		}

		/// <summary>
		/// Sets the Duplicant's current attribute experience. Only used in OnDeserialized.
		/// </summary>
		/// <param name="id">The attribute ID to look up.</param>
		/// <param name="level">The attribute experience points to set.</param>
		public void SetExperience(string id, float experience) {
			if (id != null && attrLevels.TryGetValue(id, out AttributeLevel attrLevel)) {
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
			if (id != null && attrLevels.TryGetValue(id, out AttributeLevel attrLevel)) {
				attrLevel.SetLevel(level);
				attrLevel.Apply(levels);
			}
		}
	}
}
