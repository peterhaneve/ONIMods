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

using Klei.AI;
using PeterHan.PLib;
using System.Collections.Generic;
using static STRINGS.DUPLICANTS.TRAITS;

namespace PeterHan.TraitRework {
	/// <summary>
	/// Alters Duplicant traits.
	/// </summary>
	public sealed class TraitTuning {
		/// <summary>
		/// The effect inflicted when woken Duplicants hear a loud snorer.
		/// </summary>
		internal const string DISTURBED_EFFECT = "Disturbed";

		/// <summary>
		/// How much faster the Duplicant eats in light. Used to buff the calorie burn rate.
		/// </summary>
		public const float EAT_SPEED_BUFF = 0.15f;

		/// <summary>
		/// The amount to fart for a flatulent dupe in kg.
		/// </summary>
		public const float FART_AMOUNT = 0.5f;

		/// <summary>
		/// The food items that Gastrophobic Duplicants will not eat.
		/// Stuffed Berry, Mushroom Wrap, Surf and Turf, Pepper Bread, Spicy Tofu, Frost Burger
		/// </summary>
		internal static readonly string[] GASRANGE_FOODS = {
			SalsaConfig.ID, MushroomWrapConfig.ID, SurfAndTurfConfig.ID, SpiceBreadConfig.ID,
			SpicyTofuConfig.ID, BurgerConfig.ID
		};

		/// <summary>
		/// The food items that Pacifist Duplicants will not eat.
		/// Meat, Barbecue, Frost Burger, Pacu Fillet, Cooked Fish, Surf and Turf
		/// </summary>
		internal static readonly string[] MEAT_FOODS = {
			MeatConfig.ID, CookedMeatConfig.ID, CookedFishConfig.ID, SurfAndTurfConfig.ID,
			PacuFilletConfig.ID, BurgerConfig.ID
		};

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static TraitTuning Instance { get; } = new TraitTuning();

		/// <summary>
		/// Lazily initializes the trait list.
		/// </summary>
		public IDictionary<string, TraitTemplate> Traits {
			get {
				lock (alteredTraits) {
					if (alteredTraits.Count < 1)
						InitTraits();
				}
				return alteredTraits;
			}
		}

		/// <summary>
		/// The traits to be altered, keyed by ID.
		/// </summary>
		private readonly IDictionary<string, TraitTemplate> alteredTraits;

		private TraitTuning() {
			alteredTraits = new Dictionary<string, TraitTemplate>();
		}

		/// <summary>
		/// Adds an altered trait. Since the name, description, etc are not used they are set
		/// to empty.
		/// </summary>
		/// <param name="id">The trait ID to modify.</param>
		/// <param name="positive">Whether the trait should be positive.</param>
		/// <param name="canStart">Whether the trait can appear on a Duplicant by default.</param>
		/// <param name="disabledChores">The chores which cannot be performed, or null if this
		/// attribute does not block chores.</param>
		/// <param name="modifiers">The modifiers for this trait.</param>
		private TraitTemplate AddTrait(string id, bool positive, bool canStart = true,
				string[] disabledChores = null, params AttributeModifier[] modifiers) {
			var tmp = new TraitTemplate(id) {
				IsPositive = positive,
				ValidStartingTrait = canStart,
			};
			alteredTraits.Add(id, tmp);
			if (modifiers != null)
				tmp.Modifiers = new List<AttributeModifier>(modifiers);
			if (disabledChores != null)
				tmp.DisabledChores = new List<string>(disabledChores);
			return tmp;
		}

		/// <summary>
		/// Initializes the trait alterations.
		/// </summary>
		private void InitTraits() {
			var db = Db.Get();
			var attrs = db.Attributes;
#if DEBUG
			PUtil.LogDebug("Initializing traits");
#endif
			// Mouth Breather = +50 g/s (from +100 g/s) and +2 Athletics
			AddTrait("MouthBreather", false, true, null,
				new AttributeModifier(attrs.AirConsumptionRate.Id, 0.05f, MOUTHBREATHER.NAME),
				new AttributeModifier(attrs.Athletics.Id, 2f, MOUTHBREATHER.NAME));
			// Diver's Lungs = -25 g/s and -2 Athletics
			AddTrait("DiversLung", true, true, null,
				new AttributeModifier(attrs.AirConsumptionRate.Id, -0.025f, DIVERSLUNG.NAME),
				new AttributeModifier(attrs.Athletics.Id, -2f, DIVERSLUNG.NAME));
			// Bottomless Stomach = +500 kcal/cycle and +3 Strength
			AddTrait("CalorieBurner", false, true, null,
				new AttributeModifier("CaloriesDelta", -833.33333f, CALORIEBURNER.NAME),
				new AttributeModifier(attrs.Strength.Id, 3f, CALORIEBURNER.NAME));
			// Small Bladder = +33%/cycle (from +0.2%/cycle)
			AddTrait("SmallBladder", false, true, null,
				new AttributeModifier("BladderDelta", 0.0555555556f, SMALLBLADDER.NAME));
			// Irritable Bowel = +100% bathroom use time (from +50% bathroom use time)
			AddTrait("IrritableBowel", false, true, null,
				new AttributeModifier(attrs.ToiletEfficiency.Id, -1f, IRRITABLEBOWEL.NAME));
			// Squeamish = -30 HP (from +0 HP)
			var hpMaxAttr = attrs.Get("HitPointsMax");
			hpMaxAttr.Name = string.Format(TraitStrings.MAX_HP, db.Amounts.HitPoints.Name);
			hpMaxAttr.Description = TraitStrings.MAX_HP_SHORTDESC;
			hpMaxAttr.SetFormatter(new StandardAttributeFormatter(GameUtil.UnitClass.
				SimpleInteger, GameUtil.TimeSlice.None));
			AddTrait("Hemophobia", false, true, new string[] { "MedicalAid" },
				new AttributeModifier(hpMaxAttr.Id, -30f, HEMOPHOBIA.NAME));
			// Narcoleptic = ignores Sore Back
			AddTrait("Narcoleptic", false, true, null).IgnoredEffects = new List<string> {
				"SoreBack"
			};
			// Gastrophobia custom description
			AddTrait("CantCook", false, true, new string[] { "Cook" }).ExtendedTooltip =
				() => TraitStrings.CANTCOOK_EXT;
			// Pacifist custom description
			AddTrait("ScaredyCat", false, true, new string[] { "Combat" }).ExtendedTooltip =
				() => TraitStrings.SCAREDYCAT_EXT;
			// Fart less often, but larger amount (net more fart gas) about 4 kg/cycle
			TUNING.TRAITS.FLATULENCE_EMIT_INTERVAL_MAX = 100.0f;
			TUNING.TRAITS.FLATULENCE_EMIT_INTERVAL_MIN = 60.0f;
		}
	}
}
