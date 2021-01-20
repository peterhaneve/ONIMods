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

using Harmony;
using Klei.AI;
using Newtonsoft.Json;
using PeterHan.PLib;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Tunes the difficulty and numbers behind Decor Reimagined.
	/// </summary>
	public static class DecorTuning {
		/// <summary>
		/// The length of the decor effect in cycles. Default 1.1 cycles (660 s)
		/// </summary>
		public const float DECOR_EFFECT_LEN = 660.0f;

		/// <summary>
		/// The percentage of decor perceived in sleep.
		/// </summary>
		public const float DECOR_FRACTION_SLEEP = 0.5f;

		/// <summary>
		/// Replaces the stock decor morale bonuses.
		/// </summary>
		internal static readonly DecorLevel[] DECOR_LEVELS = {
			new DecorLevel("DecorMinus3", -30.0f, -5, DecorReimaginedStrings.DECORMINUS3_NAME,
				DecorReimaginedStrings.DECORMINUS3_TOOLTIP),
			new DecorLevel("DecorMinus2", -20.0f, -3, DecorReimaginedStrings.DECORMINUS2_NAME,
				DecorReimaginedStrings.DECORMINUS2_TOOLTIP),
			new DecorLevel("DecorMinus1", -10.0f, -1, DecorReimaginedStrings.DECORMINUS1_NAME,
				DecorReimaginedStrings.DECORMINUS1_TOOLTIP),
			new DecorLevel("Decor0", 0.0f, 0, STRINGS.DUPLICANTS.MODIFIERS.DECOR0.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR0.TOOLTIP),
			new DecorLevel("Decor1", 20.0f, 1, STRINGS.DUPLICANTS.MODIFIERS.DECOR1.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR1.TOOLTIP),
			new DecorLevel("Decor2", 40.0f, 3, STRINGS.DUPLICANTS.MODIFIERS.DECOR2.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR2.TOOLTIP),
			new DecorLevel("Decor3", 60.0f, 6, STRINGS.DUPLICANTS.MODIFIERS.DECOR3.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR3.TOOLTIP),
			new DecorLevel("Decor4", 80.0f, 9, STRINGS.DUPLICANTS.MODIFIERS.DECOR4.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR4.TOOLTIP),
			new DecorLevel("Decor5", float.MaxValue, 12, STRINGS.DUPLICANTS.MODIFIERS.DECOR5.NAME,
				STRINGS.DUPLICANTS.MODIFIERS.DECOR5.TOOLTIP)
		};

		/// <summary>
		/// true for the stock behavior of hiding decor of objects inside bins, or false to
		/// be evil and make it show anyways.
		/// </summary>
		public const bool HIDE_DECOR_IN_STORAGE = true;

		/// <summary>
		/// true for the stock behavior of hiding decor of objects inside tiles, or false to
		/// be evil and make it show anyways.
		/// </summary>
		public const bool HIDE_DECOR_IN_WALLS = true;

		/// <summary>
		/// The number of decor items required to earn the achievement "And It Feels Like Home".
		/// </summary>
		public const int NUM_DECOR_FOR_ACHIEVEMENT = 15;

		/// <summary>
		/// Applies decor values from the database.
		/// </summary>
		internal static void ApplyDatabase(DecorReimaginedOptions options) {
			DecorDbEntry[] entries = null;
			try {
				// Read in database from the embedded config json
				using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
						"ReimaginationTeam.DecorRework.buildings.json")) {
					var jr = new JsonTextReader(new StreamReader(stream));
					entries = new JsonSerializer {
						MaxDepth = 2
					}.Deserialize<DecorDbEntry[]>(jr);
					jr.Close();
				}
			} catch (JsonException e) {
				// Error when loading decor
				PUtil.LogExcWarn(e);
			} catch (IOException e) {
				// Error when loading decor
				PUtil.LogExcWarn(e);
			}
			if (entries != null) {
				var editDecor = DictionaryPool<string, DecorDbEntry, DecorDbEntry>.Allocate();
				var tileLayer = PBuilding.GetObjectLayer(nameof(ObjectLayer.FoundationTile),
					ObjectLayer.FoundationTile);
				string id;
				// Add to dictionary, way faster
				foreach (var entry in entries)
					if (!string.IsNullOrEmpty(id = entry.id) && !editDecor.ContainsKey(id))
						editDecor.Add(id, entry);
				foreach (var def in Assets.BuildingDefs)
					// If PreserveTileDecor is set to true, ignore foundation tile decor mods
					if (editDecor.TryGetValue(id = def.PrefabID, out DecorDbEntry entry) &&
							(def.TileLayer != tileLayer || !options.PreserveTileDecor)) {
						float decor = entry.decor;
						int radius = entry.radius;
						var provider = def.BuildingComplete.GetComponent<DecorProvider>();
						// For reference, these do not alter the BuildingComplete
						def.BaseDecor = decor;
						def.BaseDecorRadius = radius;
						// Actual decor provider
						if (provider != null) {
							PUtil.LogDebug("Patched: {0} Decor: {1:F1} Radius: {2:D}".F(id,
								decor, radius));
							provider.baseDecor = decor;
							provider.baseRadius = radius;
						}
					}
				editDecor.Recycle();
			}
			// Patch in the debris decor
			var baseOreTemplate = typeof(EntityTemplates).GetFieldSafe("baseOreTemplate",
				true)?.GetValue(null) as GameObject;
			DecorProvider component;
			if (baseOreTemplate != null && (component = baseOreTemplate.
					GetComponent<DecorProvider>()) != null) {
				int radius = Math.Max(1, options.DebrisRadius);
				component.baseDecor = options.DebrisDecor;
				component.baseRadius = radius;
				PUtil.LogDebug("Debris: {0:F1} radius {1:D}".F(options.DebrisDecor, radius));
			}
			// Patch the suits
			PUtil.LogDebug("Snazzy Suit: {0:D} Warm/Cool Vest: {1:D}".F(options.
				SnazzySuitDecor, options.VestDecor));
			ClothingWearer.ClothingInfo.FANCY_CLOTHING.decorMod = options.SnazzySuitDecor;
			ClothingWearer.ClothingInfo.COOL_CLOTHING.decorMod = options.VestDecor;
			ClothingWearer.ClothingInfo.WARM_CLOTHING.decorMod = options.VestDecor;
		}

		/// <summary>
		/// Initializes the new decor level effects.
		/// </summary>
		internal static void InitEffects() {
			var effects = Db.Get().effects;
			foreach (var decorLevel in DECOR_LEVELS) {
				var effect = effects.TryGet(decorLevel.ID);
				if (effect != null) {
					// Overwrite existing
					effect.Name = decorLevel.Title;
					effect.description = decorLevel.Tooltip;
					effect.duration = DECOR_EFFECT_LEN;
				} else
					// Create new
					effects.Add(effect = new Effect(decorLevel.ID, decorLevel.Title,
						decorLevel.Tooltip, DECOR_EFFECT_LEN, false, false, false, null, 0.0f,
						"DecorQuality"));
				effect.SelfModifiers.Clear();
				effect.Add(new AttributeModifier("QualityOfLife", decorLevel.MoraleBonus,
					decorLevel.Title, false, false, true));
			}
		}

		/// <summary>
		/// Adjusts Atmo and Jet suit decor.
		/// </summary>
		/// <param name="options">The options for the decor of those suits.</param>
		/// <param name="suit">The suit def to modify.</param>
		internal static void TuneSuits(DecorReimaginedOptions options, EquipmentDef suit) {
			var attr = Db.Get().BuildingAttributes;
			suit.AttributeModifiers.Add(new AttributeModifier(attr.Decor.Id, options.
				AtmoSuitDecor, STRINGS.EQUIPMENT.PREFABS.ATMO_SUIT.NAME, false, false, true));
		}

		/// <summary>
		/// Represents the decor levels available.
		/// </summary>
		internal sealed class DecorLevel {
			/// <summary>
			/// The decor level required.
			/// </summary>
			public float MinDecor { get; }

			/// <summary>
			/// The morale bonus or penalty granted.
			/// </summary>
			public int MoraleBonus { get; }

			/// <summary>
			/// The modifier ID.
			/// </summary>
			public string ID { get; }

			/// <summary>
			/// The title displayed in the tooltip.
			/// </summary>
			public LocString Title { get; }

			/// <summary>
			/// The tool tip when mousing over the effect.
			/// </summary>
			public LocString Tooltip { get; }

			internal DecorLevel(string id, float minDecor, int moraleBonus, LocString title,
					LocString tooltip) {
				MinDecor = minDecor;
				MoraleBonus = moraleBonus;
				ID = id;
				Title = title;
				Tooltip = tooltip;
			}
		}
	}
}
