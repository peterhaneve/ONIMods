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

using KSerialization;
using UnityEngine;

namespace PeterHan.NoWasteWant {
	/// <summary>
	/// Filters the items in a storage by their food freshness. Since food changing freshness
	/// does not trigger any events, it has to be checked periodically.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public class FreshnessControl : KMonoBehaviour, ISim4000ms, ISingleSliderControl {
		public float MinFreshness {
			get => minFreshness;
			set {
				minFreshness = value;
				DropStaleItems();
			}
		}

		public string SliderTitleKey => "STRINGS.UI.UISIDESCREENS.FRESHNESS_CONTROL_SIDE_SCREEN.TITLE";

		public string SliderUnits => STRINGS.UI.UNITSUFFIXES.PERCENT;

		[Serialize]
		private float minFreshness;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		[MyCmpGet]
		private Storage storage;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public FreshnessControl() {
			minFreshness = 0.0f;
		}

		/// <summary>
		/// Drops items from storage if they are too stale to meet the criteria. Items that
		/// cannot rot like Berry Sludge will never be dropped.
		/// </summary>
		public void DropStaleItems() {
			if (storage != null && minFreshness > 0.0f) {
				var toDrop = ListPool<GameObject, FreshnessControl>.Allocate();
				foreach (var item in storage.items)
					if (item != null && !IsAcceptable(item))
						toDrop.Add(item);
				foreach (var item in toDrop)
					storage.Drop(item, false);
				toDrop.Recycle();
			}
		}

		public float GetSliderMax(int index) {
			return 100.0f;
		}

		public float GetSliderMin(int index) {
			return 0.0f;
		}

		public float GetSliderValue(int index) {
			return MinFreshness * 100.0f;
		}

		public string GetSliderTooltip() {
			return string.Format(Strings.Get(GetSliderTooltipKey(0)), MinFreshness * 100.0f);
		}

		public string GetSliderTooltip(int index) {
			return string.Format(Strings.Get(GetSliderTooltipKey(index)), MinFreshness * 100.0f);
		}

		public string GetSliderTooltipKey(int index) {
			return "STRINGS.UI.UISIDESCREENS.FRESHNESS_CONTROL_SIDE_SCREEN.TOOLTIP";
		}

		/// <summary>
		/// Checks to see if a food item is acceptable in this storage.
		/// </summary>
		/// <param name="item">The item to check.</param>
		/// <returns>true if it is either not food, cannot rot, or fresher than the threshold;
		/// false otherwise.</returns>
		public bool IsAcceptable(GameObject item) {
			Rottable.Instance smi;
			return item != null && ((smi = item.GetSMI<Rottable.Instance>()) == null || smi.
				RotConstitutionPercentage >= minFreshness);
		}

		public void SetSliderValue(float percent, int index) {
			MinFreshness = percent * 0.01f;
		}

		public void Sim4000ms(float dt) {
			DropStaleItems();
		}

		public int SliderDecimalPlaces(int index) {
			return 0;
		}
	}
}
