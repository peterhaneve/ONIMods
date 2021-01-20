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

using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.Detours;
using UnityEngine;

namespace PeterHan.NoSensorLimits {
	/// <summary>
	/// Patches which will be applied via annotations for No Sensor Limits.
	/// </summary>
	public static class NoSensorLimitsPatches {
		/// <summary>
		/// Sensors with a default limit more than this value will not be affected, unless they
		/// are on the specific whitelist.
		/// </summary>
		private const float AFFECT_LIMITS_BELOW = 9000.0f;

		// Delegates for private methods to update displayed values
		private delegate void UpdateTargetThresholdLabel(ThresholdSwitchSideScreen screen);
		private delegate void UpdateMaxCapacityLabel(CapacityControlSideScreen screen);

		private static readonly UpdateMaxCapacityLabel UPDATE_MAX_CAPACITY_LABEL =
			typeof(CapacityControlSideScreen).Detour<UpdateMaxCapacityLabel>();
		private static readonly UpdateTargetThresholdLabel UPDATE_TARGET_THRESHOLD_LABEL =
			typeof(ThresholdSwitchSideScreen).Detour<UpdateTargetThresholdLabel>();

		public static void OnLoad() {
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Determines if a sensor should be affected by this mod.
		/// </summary>
		/// <param name="normalMax">The default maximum value for the input.</param>
		/// <param name="target">The sensor component being affected.</param>
		/// <returns>true if the sensor should be affected, or false otherwise.</returns>
		private static bool ShouldAffect(float normalMax, object target) {
			return normalMax <= AFFECT_LIMITS_BELOW || target is LogicWattageSensor ||
				target is LogicDiseaseSensor || target is ConduitDiseaseSensor;
		}

		/// <summary>
		/// Applied to CapacityControlSideScreen to set the number input field maximum to
		/// unlimited, without affecting the slider.
		/// </summary>
		[HarmonyPatch(typeof(CapacityControlSideScreen), "SetTarget")]
		public static class CapacityControlSideScreen_SetTarget_Patch {
			/// <summary>
			/// Applied after SetTarget runs.
			/// </summary>
			internal static void Postfix(KNumberInputField ___numberInput,
					GameObject new_target) {
				float normalMax = (___numberInput == null) ? ___numberInput.maxValue : 0.0f;
				if (new_target != null) {
					var cdp = new_target.GetComponent<CreatureDeliveryPoint>();
					if (cdp != null && ShouldAffect(normalMax, cdp))
						___numberInput.maxValue = float.MaxValue;
				}
			}
		}

		/// <summary>
		/// Applied to CapacityControlSideScreen to avoid overdriving the slider if a value
		/// outside of the limits is applied.
		/// </summary>
		[HarmonyPatch(typeof(CapacityControlSideScreen), "UpdateMaxCapacity")]
		public static class CapacityControlSideScreen_UpdateMaxCapacity_Patch {
			/// <summary>
			/// Applied before UpdateMaxCapacity runs.
			/// </summary>
			internal static bool Prefix(IUserControlledCapacity ___target, float newValue,
					KSlider ___slider, CapacityControlSideScreen __instance) {
				float normalMax = ___target.MaxCapacity;
				bool skip = newValue > normalMax && ___target is CreatureDeliveryPoint &&
					ShouldAffect(normalMax, ___target);
				if (skip) {
					___target.UserMaxCapacity = newValue;
					___slider.value = normalMax;
					UPDATE_MAX_CAPACITY_LABEL?.Invoke(__instance);
				}
				return !skip;
			}
		}

		/// <summary>
		/// Applied to ThresholdSwitchSideScreen to set the number input field maximum to
		/// unlimited, without affecting the slider.
		/// </summary>
		[HarmonyPatch(typeof(ThresholdSwitchSideScreen), "SetTarget")]
		public static class ThresholdSwitchSideScreen_SetTarget_Patch {
			/// <summary>
			/// Applied after SetTarget runs.
			/// </summary>
			internal static void Postfix(KNumberInputField ___numberInput,
					GameObject new_target) {
				float normalMax = (___numberInput == null) ? ___numberInput.maxValue : 0.0f;
				if (new_target != null) {
					var sw = new_target.GetComponent<IThresholdSwitch>();
					if (sw != null && ShouldAffect(normalMax, sw))
						___numberInput.maxValue = float.MaxValue;
				}
			}
		}

		/// <summary>
		/// Applied to ThresholdSwitchSideScreen to avoid overdriving the slider if a value
		/// outside of the limits is applied.
		/// </summary>
		[HarmonyPatch(typeof(ThresholdSwitchSideScreen), "UpdateThresholdValue")]
		public static class ThresholdSwitchSideScreen_UpdateThresholdValue_Patch {
			/// <summary>
			/// Applied before UpdateThresholdValue runs.
			/// </summary>
			internal static bool Prefix(float newValue, IThresholdSwitch ___thresholdSwitch,
					NonLinearSlider ___thresholdSlider, ThresholdSwitchSideScreen __instance) {
				float normalMax = ___thresholdSwitch.RangeMax;
				bool skip = newValue > normalMax && ShouldAffect(normalMax,
					___thresholdSwitch);
				if (skip) {
					___thresholdSwitch.Threshold = newValue;
					if (___thresholdSlider != null)
						___thresholdSlider.value = ___thresholdSlider.GetPercentageFromValue(
							normalMax);
					UPDATE_TARGET_THRESHOLD_LABEL?.Invoke(__instance);
				}
				return !skip;
			}
		}
	}
}
