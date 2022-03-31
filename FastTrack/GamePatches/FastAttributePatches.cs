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

using HarmonyLib;
using Klei.AI;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

using Attribute = Klei.AI.Attribute;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to AttributeConverter to use fast lookup when Lookup is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverter), nameof(AttributeConverter.Lookup),
		typeof(Component))]
	public static class AttributeConverter_Lookup_Component_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before Lookup runs.
		/// </summary>
		internal static bool Prefix(Component cmp, ref AttributeConverterInstance __result,
				AttributeConverter __instance) {
			FastAttributeConverters fc;
			if (cmp != null && (fc = cmp.GetComponent<FastAttributeConverters>()) != null)
				__result = fc.Get(__instance);
			else
				__result = null;
			return false;
		}
	}

	/// <summary>
	/// Applied to AttributeConverter to use fast lookup when Lookup is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverter), nameof(AttributeConverter.Lookup),
		typeof(GameObject))]
	public static class AttributeConverter_Lookup_GameObject_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before Lookup runs.
		/// </summary>
		internal static bool Prefix(GameObject go, ref AttributeConverterInstance __result,
				AttributeConverter __instance) {
			var fc = go.GetComponentSafe<FastAttributeConverters>();
			if (fc != null)
				__result = fc.Get(__instance);
			else
				__result = null;
			return false;
		}
	}

	/// <summary>
	/// Applied to AttributeConverters to add a copy of our fast component alongside each
	/// instance.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverters), "OnPrefabInit")]
	public static class AttributeConverters_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(AttributeConverters __instance) {
			if (__instance != null)
				__instance.gameObject.AddOrGet<FastAttributeConverters>();
			else
				PUtil.LogWarning("Tried to add fast converters, but no attributes found!");
		}
	}

	/// <summary>
	/// Applied to AttributeLevels to add a copy of our fast component alongside each instance.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), "OnPrefabInit")]
	public static class AttributeLevels_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(AttributeLevels __instance,
				IList<AttributeLevel> ___levels) {
			if (__instance != null && ___levels != null) {
				var fl = __instance.gameObject.AddOrGet<FastAttributeLevels>();
				fl.Initialize(___levels);
			} else
				PUtil.LogWarning("Tried to add fast levels, but no attributes found!");
		}
	}

	/// <summary>
	/// Applied to ManualGenerator to use the fast version of attribute leveling.
	/// </summary>
	[HarmonyPatch(typeof(ManualGenerator), "OnWorkTick")]
	public static class ManualGenerator_OnWorkTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before OnWorkTick runs.
		/// </summary>
		internal static bool Prefix(ref bool __result, Generator ___generator, Worker worker,
				float dt) {
			var circuitManager = Game.Instance.circuitManager;
			bool charged = false;
			if (circuitManager != null) {
				ushort circuitID = circuitManager.GetCircuitID(___generator);
				bool hasBatteries = circuitManager.HasBatteries(circuitID);
				charged = (hasBatteries && circuitManager.GetMinBatteryPercentFullOnCircuit(
					circuitID) < 1.0f) || (!hasBatteries && circuitManager.HasConsumers(
					circuitID));
			}
			var levels = worker.GetComponent<FastAttributeLevels>();
			if (levels != null)
				levels.AddExperience(Db.Get().Attributes.Athletics.Id, dt, TUNING.
					DUPLICANTSTATS.ATTRIBUTE_LEVELING.ALL_DAY_EXPERIENCE);
			__result = !charged;
			return false;
		}
	}

	/// <summary>
	/// Applied to Workable to speed up the GetEfficiencyMultiplier method.
	/// </summary>
	[HarmonyPatch(typeof(Workable), nameof(Workable.GetEfficiencyMultiplier))]
	public static class Workable_GetEfficiencyMultiplier_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before GetEfficiencyMultiplier runs.
		/// </summary>
		internal static bool Prefix(Worker worker, AttributeConverter ___attributeConverter,
				ref float __result, bool ___lightEfficiencyBonus, ref bool ___currentlyLit,
				ref Guid ___lightEfficiencyBonusStatusItemHandle,
				float ___minimumAttributeMultiplier, Workable __instance) {
			float mult = 1f;
			int cell;
			if (___attributeConverter != null) {
				// Use fast attribute converters where possible
				var converter = worker.GetComponent<FastAttributeConverters>().GetConverter(
					___attributeConverter.Id);
				mult += converter.Evaluate();
			}
			if (___lightEfficiencyBonus && Grid.IsValidCell(cell = Grid.PosToCell(worker.
					transform.position))) {
				Guid handle = ___lightEfficiencyBonusStatusItemHandle;
				if (Grid.LightIntensity[cell] > 0) {
					___currentlyLit = true;
					mult += TUNING.DUPLICANTSTATS.LIGHT.LIGHT_WORK_EFFICIENCY_BONUS;
					if (handle == Guid.Empty)
						___lightEfficiencyBonusStatusItemHandle = worker.
							GetComponent<KSelectable>().AddStatusItem(Db.Get().
							DuplicantStatusItems.LightWorkEfficiencyBonus, __instance);
				} else {
					___currentlyLit = false;
					if (handle != Guid.Empty) {
						// Properly zero the Guid to avoid spamming the call later
						worker.GetComponent<KSelectable>().RemoveStatusItem(handle, false);
						___lightEfficiencyBonusStatusItemHandle = Guid.Empty;
					}
				}
			}
			__result = Mathf.Max(mult, ___minimumAttributeMultiplier);
			return false;
		}
	}

	/// <summary>
	/// Applied to Worker to work more efficiently using faster attribute leveling!
	/// </summary>
	[HarmonyPatch(typeof(Worker), nameof(Worker.Work), typeof(float))]
	public static class Worker_Work_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Updates a worker's experience, quickly!.
		/// </summary>
		/// <param name="workAttribute">The attribute being trained.</param>
		/// <param name="worker">The worker doing the job.</param>
		/// <param name="dt">The time since the last update.</param>
		/// <param name="resume">The worker's total experience resume.</param>
		private static Workable UpdateExperience(Attribute workAttribute, Worker worker,
				float dt, MinionResume resume) {
			var fastLevels = worker.GetComponent<FastAttributeLevels>();
			var workable = worker.workable;
			if (fastLevels != null && workAttribute != null && workAttribute.IsTrainable)
				// Add experience to attribute like Farming
				fastLevels.AddExperience(workAttribute.Id, dt, workable.
					GetAttributeExperienceMultiplier());
			string experienceGroup = workable.GetSkillExperienceSkillGroup();
			if (resume != null && experienceGroup != null)
				resume.AddExperienceWithAptitude(experienceGroup, dt, workable.
					GetSkillExperienceMultiplier());
			return workable;
		}

		/// <summary>
		/// Transpiles Work to snip the entire experience leveling section and replace it with
		/// our own.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var getAttribute = typeof(Workable).GetMethodSafe(nameof(Workable.
				GetWorkAttribute), false);
			var getResume = typeof(Worker).GetFieldSafe("resume", false);
			var updateExp = typeof(Worker_Work_Patch).GetMethodSafe(nameof(UpdateExperience),
				true, typeof(Attribute), typeof(Worker), typeof(float), typeof(MinionResume));
			var getEfficiency = typeof(Workable).GetMethodSafe(nameof(Workable.
				GetEfficiencyMultiplier), false, typeof(Worker));
			int state = 0;
			if (getAttribute != null && getEfficiency != null && getResume != null &&
					updateExp != null)
				foreach (var instr in instructions) {
					if (state == 1 && instr.Is(OpCodes.Callvirt, getEfficiency)) {
						state = 2;
						// Load this
						yield return new CodeInstruction(OpCodes.Ldarg_0);
#if DEBUG
						PUtil.LogDebug("Patched Worker.Work");
#endif
					}
					if (state != 1)
						yield return instr;
					if (state == 0 && instr.Is(OpCodes.Callvirt, getAttribute)) {
						// attribute is on the stack
						// Load this
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						// Load dt
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						// Load this.resume
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Ldfld, getResume);
						// Call our method
						yield return new CodeInstruction(OpCodes.Call, updateExp);
						state = 1;
					}
				}
			if (state != 2)
				PUtil.LogWarning("Unable to patch Worker.Work");
		}
	}
}
