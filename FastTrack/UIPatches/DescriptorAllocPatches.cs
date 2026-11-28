/*
 * Copyright 2026 Peter Han
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
		/// Clears any leftover data in the cached lists.
		/// </summary>
		internal static void Cleanup() {
			ALL_DESCRIPTORS.Clear();
			BUFFER.Clear();
			EFFECT_DESCRIPTORS.Clear();
			EL_DESCRIPTORS.Clear();
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
		/// Adds all descriptors to the specified list.
		/// </summary>
		/// <param name="go">The game object to describe.</param>
		/// <param name="simpleScreen">Whether the descriptors are to be shown in the
		/// simplified info screen.</param>
		/// <param name="descriptors">The location where the descriptors will be added.</param>
		internal static void GetAllDescriptors(GameObject go, bool simpleScreen,
				List<Descriptor> descriptors) {
			// U60 added an allocation free vanilla version that also uses the new
			// CollectDescriptors API, so use it to match vanilla ordering and content
			GameUtil.CollectAllDescriptors(go, descriptors, simpleScreen);
		}

		/// <summary>
		/// Filters the descriptors for effects.
		/// </summary>
		/// <param name="desc">All descriptors of the active game object.</param>
		/// <param name="simpleScreen">Whether the descriptors are to be shown in the
		/// simplified info screen.</param>
		/// <returns>The matching effect descriptors.</returns>
		internal static List<Descriptor> GetGameObjectEffects(IList<Descriptor> desc,
				bool simpleScreen) {
			var descriptors = EFFECT_DESCRIPTORS;
			int n = desc.Count;
			descriptors.Clear();
			for (int i = 0; i < n; i++) {
				var descriptor = desc[i];
				if ((simpleScreen || !descriptor.onlyForSimpleInfoScreen) && descriptor.
						IsEffectDescriptor())
					descriptors.Add(descriptor);
			}
			return descriptors;
		}
		
		/// <summary>
		/// Filters the descriptors for requirements.
		/// </summary>
		/// <param name="desc">All descriptors of the active game object.</param>
		/// <returns>The matching requirement descriptors.</returns>
		internal static List<Descriptor> GetRequirements(IList<Descriptor> desc) {
			var descriptors = EFFECT_DESCRIPTORS;
			int n = desc.Count;
			descriptors.Clear();
			for (int i = 0; i < n; i++) {
				var descriptor = desc[i];
				if (!descriptor.IsEffectDescriptor())
					descriptors.Add(descriptor);
			}
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

		// The GameUtil.Get*Descriptors patches were removed for U60: those methods are now
		// obsolete wrappers around the new allocation free GameUtil.Collect*Descriptors
		// methods, and components now provide descriptors through CollectDescriptors.
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
