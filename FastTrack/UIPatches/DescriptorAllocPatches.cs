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
	public static class DescriptorAllocPatches {
		/// <summary>
		/// A shared instance that will be reused for all descriptors. Only used on the
		/// foreground thread.
		/// </summary>
		private static readonly List<Descriptor> ALL_DESCRIPTORS = new List<Descriptor>(32);

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

		private static string HIGH_TC;

		private static string LOW_TC;

		/// <summary>
		/// The string key path to the material modifiers.
		/// </summary>
		private const string MATERIAL_MODIFIERS = nameof(STRINGS) + "." + nameof(STRINGS.
			ELEMENTS) + "." + nameof(STRINGS.ELEMENTS.MATERIAL_MODIFIERS) + ".";

		/// <summary>
		/// A shared instance that will be reused for effect descriptors. You know where it
		/// is only used?
		/// </summary>
		private static readonly List<Descriptor> PLANT_DESCRIPTORS = new List<Descriptor>(16);

		private static readonly Descriptor PLANT_EFFECTS = default;

		private static readonly Descriptor PLANT_LIFECYCLE = default;

		private static readonly Descriptor PLANT_REQUIREMENTS = default;

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
		/// Adds all of the element's specific attribute modifiers as descriptors to the list.
		/// </summary>
		/// <param name="element">The element to describe.</param>
		/// <param name="descriptors">The location where the descriptors will be stored.</param>
		internal static void GetMaterialDescriptors(IList<AttributeModifier> modifiers,
				IList<Descriptor> descriptors) {
			if (modifiers != null && modifiers.Count > 0) {
				int n = modifiers.Count;
				for (int i = 0; i < n; i++) {
					Descriptor item = default;
					var modifier = modifiers[i];
					string attribute = modifier.AttributeId.ToUpper(), value =
						modifier.GetFormattedString();
					item.SetupDescriptor(Strings.Get(MATERIAL_MODIFIERS + attribute).Format(
						value), Strings.Get(MATERIAL_MODIFIERS + nameof(MODIFIERS.TOOLTIP) +
						"." + attribute).Format(value));
					item.IncreaseIndent();
					descriptors.Add(item);
				}
			}
		}

		/// <summary>
		/// Adds material property descriptors based off of particular values of the element
		/// properties (like low TC, "slow heating"...).
		/// </summary>
		/// <param name="element">The element to describe.</param>
		/// <param name="descriptors">The location where the descriptors will be stored.</param>
		private static void GetSignificantMaterialPropertyDescriptors(this Element element,
				IList<Descriptor> descriptors) {
			string name = element.name;
			// No consts for these in ONI code :uuhhhh:
			if (element.thermalConductivity > 10f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.HIGH_THERMAL_CONDUCTIVITY.Format(GameUtil.
					GetThermalConductivityString(element, false, false)), string.Format(
					HIGH_TC, name, element.thermalConductivity));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
			if (element.thermalConductivity < 1f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.LOW_THERMAL_CONDUCTIVITY.Format(GameUtil.
					GetThermalConductivityString(element, false, false)), string.Format(
					LOW_TC, name, element.thermalConductivity));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
			if (element.specificHeatCapacity <= 0.2f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.LOW_SPECIFIC_HEAT_CAPACITY, string.Format(
					MODIFIERS.TOOLTIP.LOW_SPECIFIC_HEAT_CAPACITY, name, element.
					specificHeatCapacity));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
			if (element.specificHeatCapacity >= 1f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.HIGH_SPECIFIC_HEAT_CAPACITY, string.Format(
					MODIFIERS.TOOLTIP.HIGH_SPECIFIC_HEAT_CAPACITY, name, element.
					specificHeatCapacity));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
			if (Sim.IsRadiationEnabled() && element.radiationAbsorptionFactor >= 0.8f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.EXCELLENT_RADIATION_SHIELD, string.Format(
					MODIFIERS.TOOLTIP.EXCELLENT_RADIATION_SHIELD, name, element.
					radiationAbsorptionFactor));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
		}

		/// <summary>
		/// Initializes some strings, which when resolving LocString.ToString at runtime would
		/// require a relatively expensive Strings.Get.
		/// </summary>
		internal static void Init() {
			HIGH_TC = MODIFIERS.TOOLTIP.HIGH_THERMAL_CONDUCTIVITY.Replace("{1}",
				"{1:0.######}");
			LOW_TC = MODIFIERS.TOOLTIP.LOW_THERMAL_CONDUCTIVITY.Replace("{1}",
				"{1:0.######}");
			PLANT_EFFECTS.SetupDescriptor(PLANTERSIDESCREEN.PLANTEFFECTS, PLANTERSIDESCREEN.
				TOOLTIPS.PLANTEFFECTS);
			PLANT_LIFECYCLE.SetupDescriptor(PLANTERSIDESCREEN.LIFECYCLE, PLANTERSIDESCREEN.
				TOOLTIPS.PLANTLIFECYCLE, Descriptor.DescriptorType.Lifecycle);
			PLANT_REQUIREMENTS.SetupDescriptor(PLANTERSIDESCREEN.PLANTREQUIREMENTS,
				PLANTERSIDESCREEN.TOOLTIPS.PLANTREQUIREMENTS, Descriptor.DescriptorType.
				Requirement);
			DescriptorSorter.CreateInstance();
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
					if (descriptor.IsEffectDescriptor())
						filtered.Add(descriptor);
				}
				GameUtil.IndentListOfDescriptors(filtered);
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
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting element descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors),
			typeof(Element))]
		internal static class GetMaterialDescriptorsElement_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetMaterialDescriptors runs.
			/// </summary>
			internal static bool Prefix(Element element, ref List<Descriptor> __result) {
				var descriptors = EL_DESCRIPTORS;
				descriptors.Clear();
				if (element == null)
					throw new ArgumentNullException(nameof(element));
				GetMaterialDescriptors(element.attributeModifiers, descriptors);
				element.GetSignificantMaterialPropertyDescriptors(descriptors);
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reuse a list for getting element descriptors.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), typeof(Tag))]
		internal static class GetMaterialDescriptorsTag_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before GetMaterialDescriptors runs.
			/// </summary>
			internal static bool Prefix(Tag tag, ref List<Descriptor> __result) {
				var descriptors = EL_DESCRIPTORS;
				var element = ElementLoader.GetElement(tag);
				GameObject prefabGO;
				descriptors.Clear();
				if (element != null) {
					// If element is defined
					GetMaterialDescriptors(element.attributeModifiers, descriptors);
					element.GetSignificantMaterialPropertyDescriptors(descriptors);
				} else if ((prefabGO = Assets.TryGetPrefab(tag)) != null && prefabGO.
						TryGetComponent(out PrefabAttributeModifiers prefabMods))
					GetMaterialDescriptors(prefabMods.descriptors, descriptors);
				__result = descriptors;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to use a string buffer when getting material tooltips.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialTooltips), typeof(Tag))]
		internal static class GetMaterialTooltips_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

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
			/// Applied before GetMaterialTooltips runs.
			/// </summary>
			internal static bool Prefix(Tag tag, ref string __result) {
				var text = BUFFER;
				var element = ElementLoader.GetElement(tag);
				GameObject prefabGO;
				text.Clear();
				text.Append(tag.ProperName());
				if (element != null) {
					var descriptors = EL_DESCRIPTORS;
					descriptors.Clear();
					AddModifiers(element.attributeModifiers, text);
					element.GetSignificantMaterialPropertyDescriptors(EL_DESCRIPTORS);
					if (descriptors.Count > 0) {
						int n = descriptors.Count;
						text.AppendLine();
						for (int i = 0; i < n; i++)
							text.Append(Constants.TABBULLETSTRING).Append(Util.
								StripTextFormatting(descriptors[i].text)).AppendLine();
					}
				} else if ((prefabGO = Assets.TryGetPrefab(tag)) != null && prefabGO.
						TryGetComponent(out PrefabAttributeModifiers prefabMods))
					AddModifiers(prefabMods.descriptors, text);
				__result = text.ToString();
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
								descriptors.Add(PLANT_EFFECTS);
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
							descriptors.Add(PLANT_LIFECYCLE);
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
							descriptors.Add(PLANT_REQUIREMENTS);
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
					if (descriptor.type == Descriptor.DescriptorType.Requirement)
						filtered.Add(descriptor);
				}
				GameUtil.IndentListOfDescriptors(filtered);
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
			if (!order.TryGetValue(e1.GetType(), out int o1))
				o1 = -1;
			if (!order.TryGetValue(e2.GetType(), out int o2))
				o2 = -1;
			return o1.CompareTo(o2);
		}
	}
}
