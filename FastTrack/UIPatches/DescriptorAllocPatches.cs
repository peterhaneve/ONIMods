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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using MODIFIERS = STRINGS.ELEMENTS.MATERIAL_MODIFIERS;
using PLANTERSIDESCREEN = STRINGS.UI.UISIDESCREENS.PLANTERSIDESCREEN;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to consolidate list allocations. All uses of these methods in the base
	/// game die in the same scope they were created.
	/// </summary>
	public static partial class DescriptorAllocPatches {
		/// <summary>
		/// A shared instance that will be reused for all descriptors. Only used on the
		/// foreground thread.
		/// </summary>
		internal static readonly List<Descriptor> ALL_DESCRIPTORS = new List<Descriptor>(32);

		/// <summary>
		/// A shared buffer that will be reused for tooltip text. Only used on the foreground
		/// thread.
		/// </summary>
		private static readonly StringBuilder BUFFER = new StringBuilder(256);

		/// <summary>
		/// A shared instance that will be reused for effect descriptors. Only used on the
		/// foreground thread.
		/// </summary>
		private static readonly List<Descriptor> EFFECT_DESCRIPTORS = new List<Descriptor>(16);

		/// <summary>
		/// A shared instance that will be reused for element descriptors. Only used on the...
		/// you know what I mean.
		/// </summary>
		private static readonly List<Descriptor> EL_DESCRIPTORS = new List<Descriptor>(16);

		/// <summary>
		/// A shared instance that will be reused for effect descriptors. You know where it
		/// is only used?
		/// </summary>
		private static readonly List<Descriptor> PLANT_DESCRIPTORS = new List<Descriptor>(16);

		private static Descriptor plantEffects;

		private static Descriptor plantLifecycle;

		private static Descriptor plantRequirements;

		/// <summary>
		/// Clears any leftover data in the cached lists.
		/// </summary>
		internal static void Cleanup() {
			ALL_DESCRIPTORS.Clear();
			BUFFER.Clear();
			EFFECT_DESCRIPTORS.Clear();
			EL_DESCRIPTORS.Clear();
			PLANT_DESCRIPTORS.Clear();
		}

		/// <summary>
		/// Adds the matching descriptors to the full list.
		/// </summary>
		/// <param name="descriptors">The location where the descriptors will be added.</param>
		/// <param name="toAdd">The descriptors to be added.</param>
		/// <param name="simpleScreen">Whether the descriptors are to be shown in the
		/// simplified info screen.</param>
		private static void AddDescriptors(this ICollection<Descriptor> descriptors,
				IList<Descriptor> toAdd, bool simpleScreen) {
			if (toAdd != null) {
				int n = toAdd.Count;
				for (int i = 0; i < n; i++) {
					var descriptor = toAdd[i];
					if (simpleScreen || !descriptor.onlyForSimpleInfoScreen)
						descriptors.Add(descriptor);
				}
			}
		}
		
		/// <summary>
		/// Adds the modifiers to the tooltip.
		/// </summary>
		/// <param name="modifiers">The modifiers to add.</param>
		/// <param name="text">The location where the tooltip will be stored.</param>
		private static void AddModifiers(IList<AttributeModifier> modifiers,
			StringBuilder text) {
			int n = modifiers.Count;
			var buildAttributes = Db.Get().BuildingAttributes;
			for (int i = 0; i < n; i++) {
				var modifier = modifiers[i];
				text.AppendLine().Append(Constants.TABBULLETSTRING).AppendFormat(
					STRINGS.DUPLICANTS.MODIFIERS.MODIFIER_FORMAT, buildAttributes.Get(
						modifier.AttributeId).Name, modifier.GetFormattedString());
			}
		}

		/// <summary>
		/// Adds components that can provide descriptors from a state machine.
		/// </summary>
		/// <param name="smc">The state machine controller to examine.</param>
		/// <param name="descComponents">The location where the descriptor components will be added.</param>
		private static void AddStateMachineDescriptors(this StateMachineController smc,
				ICollection<IGameObjectEffectDescriptor> descComponents) {
			if (smc.defHandle.IsValid()) {
				// Avoid allocating another list
				var defs = smc.cmpdef.defs;
				int n = defs.Count;
				for (int i = 0; i < n; i++) {
					var baseDef = defs[i];
					if (baseDef is IGameObjectEffectDescriptor descriptor)
						descComponents.Add(descriptor);
				}
			}
		}
		
		/// <summary>
		/// Adds all descriptors to the specified list.
		/// </summary>
		/// <param name="go">The game object to describe.</param>
		/// <param name="simpleScreen">Whether the descriptors are to be shown in the
		/// simplified info screen.</param>
		/// <param name="descriptors">The location where the descriptors will be added.</param>
		internal static void GetAllDescriptors(GameObject go, bool simpleScreen,
				ICollection<Descriptor> descriptors) {
			var comps = ListPool<IGameObjectEffectDescriptor, DescriptorSorter>.Allocate();
			go.GetComponents(comps);
			if (go.TryGetComponent(out StateMachineController smc))
				smc.AddStateMachineDescriptors(comps);
			comps.Sort(DescriptorSorter.Instance);
			int n = comps.Count;
			for (int i = 0; i < n; i++)
				descriptors.AddDescriptors(comps[i].GetDescriptors(go), simpleScreen);
			comps.Recycle();
			if (go.TryGetComponent(out KPrefabID prefabID)) {
				descriptors.AddDescriptors(prefabID.AdditionalRequirements,
					simpleScreen);
				descriptors.AddDescriptors(prefabID.AdditionalEffects, simpleScreen);
			}
		}

		/// <summary>
		/// Gets the game object's effects in the shared descriptor list.
		/// </summary>
		/// <param name="go">The game object to describe.</param>
		/// <param name="simpleInfoScreen">Whether the descriptors are to be shown in the
		/// simplified info screen.</param>
		/// <returns>The game object's effect descriptors.</returns>
		internal static List<Descriptor> GetGameObjectEffects(GameObject go,
				bool simpleInfoScreen) {
			var descriptors = EFFECT_DESCRIPTORS;
			var comps = ListPool<IGameObjectEffectDescriptor, DescriptorSorter>.Allocate();
			descriptors.Clear();
			go.GetComponents(comps);
			if (go.TryGetComponent(out StateMachineController smc))
				smc.AddStateMachineDescriptors(comps);
			comps.Sort(DescriptorSorter.Instance);
			int n = comps.Count;
			for (int i = 0; i < n; i++) {
				var toAdd = comps[i].GetDescriptors(go);
				if (toAdd != null) {
					int nd = toAdd.Count;
					for (int j = 0; j < nd; j++) {
						var descriptor = toAdd[j];
						if ((simpleInfoScreen || !descriptor.onlyForSimpleInfoScreen) &&
								descriptor.IsEffectDescriptor())
							descriptors.Add(descriptor);
					}
				}
			}
			comps.Recycle();
			if (go.TryGetComponent(out KPrefabID prefabID))
				descriptors.AddDescriptors(prefabID.AdditionalEffects, simpleInfoScreen);
			return descriptors;
		}

		/// <summary>
		/// Initializes some strings, which when resolving LocString.ToString at runtime would
		/// require a relatively expensive Strings.Get.
		/// </summary>
		internal static void Init() {
			highTC = MODIFIERS.TOOLTIP.HIGH_THERMAL_CONDUCTIVITY.Replace("{1}",
				"{1:0.######}");
			lowTC = MODIFIERS.TOOLTIP.LOW_THERMAL_CONDUCTIVITY.Replace("{1}",
				"{1:0.######}");
			plantEffects.SetupDescriptor(PLANTERSIDESCREEN.PLANTEFFECTS, PLANTERSIDESCREEN.
				TOOLTIPS.PLANTEFFECTS);
			plantLifecycle.SetupDescriptor(PLANTERSIDESCREEN.LIFECYCLE, PLANTERSIDESCREEN.
				TOOLTIPS.PLANTLIFECYCLE, Descriptor.DescriptorType.Lifecycle);
			plantRequirements.SetupDescriptor(PLANTERSIDESCREEN.PLANTREQUIREMENTS,
				PLANTERSIDESCREEN.TOOLTIPS.PLANTREQUIREMENTS, Descriptor.DescriptorType.
				Requirement);
		}

		/// <summary>
		/// Checks to see if a descriptor should be shown under Effects.
		/// </summary>
		/// <param name="desc">The descriptor to show.</param>
		/// <returns>Whether it belongs in the Effects section.</returns>
		internal static bool IsEffectDescriptor(this Descriptor desc) {
			var dt = desc.type;
			return dt == Descriptor.DescriptorType.Effect || dt == Descriptor.DescriptorType.
				DiseaseSource;
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetAllDescriptors))]
		internal static class GetAllDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetAllDescriptors runs.
			/// </summary>
			internal static bool Prefix(GameObject go, bool simpleInfoScreen,
					ref List<Descriptor> __result) {
				var descriptors = ALL_DESCRIPTORS;
				descriptors.Clear();
				GetAllDescriptors(go, simpleInfoScreen, descriptors);
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting effect descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetEffectDescriptors))]
		internal static class GetEffectDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetEffectDescriptors runs.
			/// </summary>
			internal static bool Prefix(List<Descriptor> descriptors,
					ref List<Descriptor> __result) {
				var filtered = EFFECT_DESCRIPTORS;
				int n = descriptors.Count;
				filtered.Clear();
				for (int i = 0; i < n; i++) {
					var descriptor = descriptors[i];
					if (descriptor.IsEffectDescriptor()) {
						descriptor.IncreaseIndent();
						filtered.Add(descriptor);
					}
				}
				__result = filtered;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting game object effects.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetGameObjectEffects))]
		internal static class GetGameObjectEffects_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetGameObjectEffects runs.
			/// </summary>
			internal static bool Prefix(GameObject go, bool simpleInfoScreen,
					ref List<Descriptor> __result) {
				__result = GetGameObjectEffects(go, simpleInfoScreen);
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting plant effect descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetPlantEffectDescriptors))]
		internal static class GetPlantEffectDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetPlantEffectDescriptors runs.
			/// </summary>
			internal static bool Prefix(GameObject go, ref List<Descriptor> __result) {
				var descriptors = PLANT_DESCRIPTORS;
				descriptors.Clear();
				if (go.TryGetComponent(out Growing _)) {
					var allDescriptors = ALL_DESCRIPTORS;
					bool added = false;
					allDescriptors.Clear();
					GetAllDescriptors(go, false, allDescriptors);
					int n = allDescriptors.Count;
					for (int i = 0; i < n; i++) {
						var descriptor = allDescriptors[i];
						if (descriptor.IsEffectDescriptor()) {
							if (!added) {
								descriptors.Add(plantEffects);
								added = true;
							}
							descriptor.IncreaseIndent();
							descriptors.Add(descriptor);
						}
					}
				}
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting plant effect descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetPlantLifeCycleDescriptors))]
		internal static class GetPlantLifeCycleDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetPlantLifeCycleDescriptors runs.
			/// </summary>
			internal static bool Prefix(GameObject go, ref List<Descriptor> __result) {
				var descriptors = PLANT_DESCRIPTORS;
				var allDescriptors = ALL_DESCRIPTORS;
				bool added = false;
				descriptors.Clear();
				allDescriptors.Clear();
				GetAllDescriptors(go, false, allDescriptors);
				int n = allDescriptors.Count;
				for (int i = 0; i < n; i++) {
					var descriptor = allDescriptors[i];
					if (descriptor.type == Descriptor.DescriptorType.Lifecycle) {
						if (!added) {
							descriptors.Add(plantLifecycle);
							added = true;
						}
						descriptor.IncreaseIndent();
						descriptors.Add(descriptor);
					}
				}
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting plant requirement descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetPlantRequirementDescriptors))]
		internal static class GetPlantRequirementDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetPlantRequirementDescriptors runs.
			/// </summary>
			internal static bool Prefix(GameObject go, ref List<Descriptor> __result) {
				var descriptors = PLANT_DESCRIPTORS;
				var allDescriptors = ALL_DESCRIPTORS;
				bool added = false;
				descriptors.Clear();
				allDescriptors.Clear();
				GetAllDescriptors(go, false, allDescriptors);
				int n = allDescriptors.Count;
				for (int i = 0; i < n; i++) {
					var descriptor = allDescriptors[i];
					if (descriptor.type == Descriptor.DescriptorType.Requirement) {
						if (!added) {
							descriptors.Add(plantRequirements);
							added = true;
						}
						descriptor.IncreaseIndent();
						descriptors.Add(descriptor);
					}
				}
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting requirement descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetRequirementDescriptors))]
		internal static class GetRequirementDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetRequirementDescriptors runs.
			/// </summary>
			internal static bool Prefix(List<Descriptor> descriptors,
					ref List<Descriptor> __result) {
				var filtered = EFFECT_DESCRIPTORS;
				int n = descriptors.Count;
				filtered.Clear();
				for (int i = 0; i < n; i++) {
					var descriptor = descriptors[i];
					if (descriptor.type == Descriptor.DescriptorType.Requirement) {
						descriptor.IncreaseIndent();
						filtered.Add(descriptor);
					}
				}
				__result = filtered;
				return false;
			}
		}
	}

	/// <summary>
	/// Sorts game object descriptors by their index in the component description order.
	/// </summary>
	internal sealed class DescriptorSorter : IComparer<IGameObjectEffectDescriptor> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static DescriptorSorter Instance { get; private set; }

		internal static void CreateInstance() {
			Instance = new DescriptorSorter();
		}

		/// <summary>
		/// Looks up the sort index of a descriptor by its type.
		/// </summary>
		private readonly IDictionary<Type, int> order;

		private DescriptorSorter() {
			var types = TUNING.BUILDINGS.COMPONENT_DESCRIPTION_ORDER;
			int n = types.Count;
			order = new Dictionary<Type, int>(n);
			for (int i = 0; i < n; i++) {
				var type = types[i];
				// There are dupes, CLAY PLEASE
				if (!order.ContainsKey(type))
					order.Add(type, i);
			}
		}

		public int Compare(IGameObjectEffectDescriptor e1, IGameObjectEffectDescriptor e2) {
			int result = 0;
			if (e1 != null && e2 != null) {
				if (!order.TryGetValue(e1.GetType(), out int o1))
					o1 = -1;
				if (!order.TryGetValue(e2.GetType(), out int o2))
					o2 = -1;
				result = o1.CompareTo(o2);
			}
			return result;
		}
	}
}
