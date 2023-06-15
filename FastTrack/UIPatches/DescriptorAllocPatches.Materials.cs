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

using System;
using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using PeterHan.PLib.Core;
using UnityEngine;

using MODIFIERS = STRINGS.ELEMENTS.MATERIAL_MODIFIERS;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to consolidate list allocations for material properties. All uses of
	/// these methods in the base game die in the same scope they were created.
	/// </summary>
	public static partial class DescriptorAllocPatches {
		/// <summary>
		/// The string key path to the material modifiers.
		/// </summary>
		private const string MATERIAL_MODIFIERS = nameof(STRINGS) + "." + nameof(STRINGS.
			ELEMENTS) + "." + nameof(STRINGS.ELEMENTS.MATERIAL_MODIFIERS) + ".";

		/// <summary>
		/// A cached version of the High Thermal Conductivity tooltip.
		/// </summary>
		private static string highTC;

		/// <summary>
		/// A cached version of the Low Thermal Conductivity tooltip.
		/// </summary>
		private static string lowTC;

		/// <summary>
		/// Applies patches to improve performance of selected materials dialogs.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use when patching.</param>
		internal static void ApplyMaterialPatches(Harmony harmony) {
			var gu = typeof(GameUtil);
			harmony.Patch(gu.GetMethodSafe(nameof(GameUtil.GetMaterialDescriptors), true,
				typeof(Element)), prefix: new HarmonyMethod(typeof(DescriptorAllocPatches),
				nameof(GetMaterialDescriptorsElement_Prefix)));
			harmony.Patch(gu.GetMethodSafe(nameof(GameUtil.GetMaterialDescriptors), true,
				typeof(Tag)), prefix: new HarmonyMethod(typeof(DescriptorAllocPatches),
				nameof(GetMaterialDescriptorsTag_Prefix)));
			harmony.Patch(gu.GetMethodSafe(nameof(GameUtil.GetMaterialTooltips), true,
				typeof(Tag)), prefix: new HarmonyMethod(typeof(DescriptorAllocPatches),
				nameof(GetMaterialTooltips_Prefix)));
		}
		
		/// <summary>
		/// Adds all of the element's specific attribute modifiers as descriptors to the list.
		/// </summary>
		/// <param name="modifiers">The attribute modifiers to describe.</param>
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
		/// Applied to GameUtil to reuse a list for getting element descriptors.
		/// </summary>
		private static bool GetMaterialDescriptorsElement_Prefix(Element element,
				ref List<Descriptor> __result) {
			var descriptors = EL_DESCRIPTORS;
			descriptors.Clear();
			if (element == null)
				throw new ArgumentNullException(nameof(element));
			GetMaterialDescriptors(element.attributeModifiers, descriptors);
			element.GetSignificantMaterialPropertyDescriptors(descriptors);
			__result = descriptors;
			return false;
		}

		/// <summary>
		/// Applied before GetMaterialDescriptors runs.
		/// </summary>
		private static bool GetMaterialDescriptorsTag_Prefix(Tag tag,
				ref List<Descriptor> __result) {
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

		/// <summary>
		/// Applied before GetMaterialTooltips runs.
		/// </summary>
		private static bool GetMaterialTooltips_Prefix(Tag tag, ref string __result) {
			var text = BUFFER;
			var element = ElementLoader.GetElement(tag);
			GameObject prefabGO;
			text.Clear().Append(tag.ProperName());
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

		/// <summary>
		/// Adds material property descriptors based off of particular values of the element
		/// properties (like low TC, "slow heating"...).
		/// </summary>
		/// <param name="element">The element to describe.</param>
		/// <param name="descriptors">The location where the descriptors will be stored.</param>
		private static void GetSignificantMaterialPropertyDescriptors(this Element element,
				ICollection<Descriptor> descriptors) {
			string name = element.name;
			// No consts for these in ONI code :uuhhhh:
			if (element.thermalConductivity > 10f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.HIGH_THERMAL_CONDUCTIVITY.Format(GameUtil.
					GetThermalConductivityString(element, false, false)), string.Format(
					highTC, name, element.thermalConductivity));
				desc.IncreaseIndent();
				descriptors.Add(desc);
			}
			if (element.thermalConductivity < 1f) {
				Descriptor desc = default;
				desc.SetupDescriptor(MODIFIERS.LOW_THERMAL_CONDUCTIVITY.Format(GameUtil.
					GetThermalConductivityString(element, false, false)), string.Format(
					lowTC, name, element.thermalConductivity));
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
	}
}
