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

using HarmonyLib;
using Klei.AI;
using PeterHan.PLib.Core;
using System;
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
			__result = cmp != null && cmp.TryGetComponent(out AttributeConverters converters) ?
				LookupAttributeConverter.GetConverter(converters, __instance.Id) : null;
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
			__result = go != null && go.TryGetComponent(out AttributeConverters converters) ?
				LookupAttributeConverter.GetConverter(converters, __instance.Id) : null;
			return false;
		}
	}

	/// <summary>
	/// Applied to AttributeConverters to initialize the fake attribute converter with quick
	/// lookup.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverters), nameof(AttributeConverters.OnPrefabInit))]
	public static class AttributeConverters_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(AttributeConverters __instance) {
			if (__instance != null)
				LookupAttributeConverter.GetConverterLookup(__instance);
			else
				PUtil.LogWarning("Tried to add fast converters, but no attributes found!");
		}
	}

	/// <summary>
	/// Applied to AttributeConverters to use fast lookup when Get is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverters), nameof(AttributeConverters.Get))]
	public static class AttributeConverters_Get_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before Get runs.
		/// </summary>
		internal static bool Prefix(AttributeConverters __instance,
				AttributeConverter converter, ref AttributeConverterInstance __result) {
			__result = converter == null ? null : LookupAttributeConverter.GetConverter(
				__instance, converter.Id);
			return false;
		}
	}

	/// <summary>
	/// Applied to AttributeConverters to use fast lookup when GetConverter is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeConverters), nameof(AttributeConverters.GetConverter))]
	public static class AttributeConverters_GetConverter_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before GetConverter runs.
		/// </summary>
		internal static bool Prefix(AttributeConverters __instance, string id,
				ref AttributeConverterInstance __result) {
			__result = LookupAttributeConverter.GetConverter(__instance, id);
			return false;
		}
	}

	/// <summary>
	/// Applied to AttributeLevels to use fast lookup when GetAttributeLevel is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.GetAttributeLevel))]
	public static class AttributeLevels_GetAttributeLevel_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before GetAttributeLevel runs.
		/// </summary>
		internal static bool Prefix(AttributeLevels __instance, string attribute_id,
				ref AttributeLevel __result) {
			var lookup = LookupAttributeLevel.GetAttributeLookup(__instance);
			if (lookup != null)
				__result = lookup.GetAttributeLevel(attribute_id);
			return lookup != null;
		}
	}
	
	/// <summary>
	/// Applied to AttributeLevels to use fast lookup when GetLevel is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.GetLevel))]
	public static class AttributeLevels_GetLevel_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before GetLevel runs.
		/// </summary>
		internal static bool Prefix(AttributeLevels __instance, Attribute attribute,
				ref int __result) {
			var lookup = LookupAttributeLevel.GetAttributeLookup(__instance);
			if (lookup != null)
				__result = lookup.GetLevel(attribute);
			return lookup != null;
		}
	}
	
	/// <summary>
	/// Applied to AttributeLevels to initialize the fake attribute level with quick lookup.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.OnPrefabInit))]
	public static class AttributeLevels_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(AttributeLevels __instance) {
			if (__instance != null)
				LookupAttributeLevel.GetAttributeLookup(__instance);
			else
				PUtil.LogWarning("Tried to add fast levels, but no attributes found!");
		}
	}
	
	/// <summary>
	/// Applied to AttributeLevels to use fast lookup when SetExperience is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.SetExperience))]
	public static class AttributeLevels_SetExperience_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before SetExperience runs.
		/// </summary>
		internal static bool Prefix(AttributeLevels __instance, string attribute_id,
				float experience) {
			var lookup = LookupAttributeLevel.GetAttributeLookup(__instance);
			lookup?.SetExperience(attribute_id, experience);
			return lookup != null;
		}
	}
	
	/// <summary>
	/// Applied to AttributeLevels to use fast lookup when SetLevel is called.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.SetLevel))]
	public static class AttributeLevels_SetLevel_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied before SetLevel runs.
		/// </summary>
		internal static bool Prefix(AttributeLevels __instance, string attribute_id,
				int level) {
			var lookup = LookupAttributeLevel.GetAttributeLookup(__instance);
			lookup?.SetLevel(attribute_id, level);
			return lookup != null;
		}
	}
	
	/// <summary>
	/// Applied to AttributeLevels to avoid serializing our fake attribute level.
	/// </summary>
	[HarmonyPatch(typeof(AttributeLevels), nameof(AttributeLevels.OnSerializing))]
	public static class AttributeLevels_OnSerializing_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastAttributesMode;

		/// <summary>
		/// Applied after OnSerializing runs.
		/// </summary>
		internal static void Postfix(AttributeLevels __instance) {
			var saveLoad = __instance.saveLoadLevels;
			int n;
			if (saveLoad != null && (n = saveLoad.Length) > 0 && saveLoad[0].attributeId ==
					LookupAttributeLevel.ID) {
				var newSaveLoad = new AttributeLevels.LevelSaveLoad[n - 1];
				Array.Copy(saveLoad, 1, newSaveLoad, 0, n - 1);
				__instance.saveLoadLevels = newSaveLoad;
			}
		}
	}

	/// <summary>
	/// Applied to ManualGenerator to use the fast version of attribute leveling.
	/// </summary>
	[HarmonyPatch(typeof(ManualGenerator), nameof(ManualGenerator.OnWorkTick))]
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
			if (worker.TryGetComponent(out AttributeLevels levels)) {
				var lookup = LookupAttributeLevel.GetAttributeLookup(levels);
				var athletics = Db.Get().Attributes.Athletics.Id;
				var exp = TUNING.DUPLICANTSTATS.ATTRIBUTE_LEVELING.ALL_DAY_EXPERIENCE;
				if (lookup != null)
					lookup.AddExperience(athletics, dt, exp);
				else
					levels.AddExperience(athletics, dt, exp);
			}
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
		internal static bool Prefix(Worker worker, ref float __result, Workable __instance) {
			float mult = 1f;
			int cell;
			var ac = __instance.attributeConverter;
			if (ac != null && worker.TryGetComponent(out AttributeConverters converters)) {
				var lookup = LookupAttributeConverter.GetConverterLookup(converters);
				var converter = (lookup != null) ? lookup.GetConverter(ac.Id) : converters.
					GetConverter(ac.Id);
				// Use fast attribute converters where possible
				if (converter != null)
					mult += converter.Evaluate();
			}
			if (__instance.lightEfficiencyBonus && Grid.IsValidCell(cell = Grid.PosToCell(
					worker.transform.position))) {
				ref var handle = ref __instance.lightEfficiencyBonusStatusItemHandle;
				var hv = handle;
				bool lit;
				if (Grid.LightIntensity[cell] > 0) {
					lit = true;
					mult += TUNING.DUPLICANTSTATS.LIGHT.LIGHT_WORK_EFFICIENCY_BONUS;
					if (hv == Guid.Empty && worker.TryGetComponent(out KSelectable ks))
						handle = ks.AddStatusItem(Db.Get().DuplicantStatusItems.
							LightWorkEfficiencyBonus, __instance);
				} else {
					lit = false;
					if (hv != Guid.Empty && worker.TryGetComponent(out KSelectable ks)) {
						// Properly zero the Guid to avoid spamming the call later
						ks.RemoveStatusItem(hv);
						handle = Guid.Empty;
					}
				}
				__instance.currentlyLit = lit;
			}
			__result = Mathf.Max(mult, __instance.minimumAttributeMultiplier);
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
			var workable = worker.workable;
			if (worker.TryGetComponent(out AttributeLevels levels) &&
					workAttribute != null && workAttribute.IsTrainable) {
				var lookup = LookupAttributeLevel.GetAttributeLookup(levels);
				var exp = workable.GetAttributeExperienceMultiplier();
				// Add experience to attribute like Farming
				if (lookup != null)
					lookup.AddExperience(workAttribute.Id, dt, exp);
				else
					levels.AddExperience(workAttribute.Id, dt, exp);
			}
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
			var getResume = typeof(Worker).GetFieldSafe(nameof(Worker.resume), false);
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
