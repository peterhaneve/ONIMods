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
using PeterHan.PLib.Core;

namespace PeterHan.MismatchedFinder {
	/// <summary>
	/// Patches which will be applied via annotations for Mismatched Wire Finder.
	/// </summary>
	public sealed class MismatchedFinderPatches : KMod.UserMod2 {
		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			PRegistry.PutData("Bugs.FlowUtilityNetworkConduits", true);
		}

		/// <summary>
		/// Applied to Conduit to add the mismatched pipe menu to all pipes in game.
		/// </summary>
		[HarmonyPatch(typeof(Conduit), "OnSpawn")]
		public static class Conduit_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Conduit __instance) {
				__instance.gameObject.AddOrGet<FindMismatchedPipe>();
			}
		}

		/// <summary>
		/// Applied to FlowUtilityNetwork to actually add pipes to it correctly. Klei has the
		/// conduit field, but never actually adds anything to it...
		/// </summary>
		[HarmonyPatch(typeof(FlowUtilityNetwork), nameof(FlowUtilityNetwork.AddItem))]
		public static class FlowUtilityNetwork_AddItem_Patch {
			/// <summary>
			/// Applied after AddItem runs.
			/// </summary>
			internal static void Postfix(FlowUtilityNetwork __instance, object generic_item) {
				if (generic_item is FlowUtilityNetwork.IItem conduit && conduit.EndpointType ==
						Endpoint.Conduit)
					__instance.conduits.Add(conduit);
			}
		}

		/// <summary>
		/// Applied to FlowUtilityNetwork to clear the list of pipes before resetting as the
		/// Klei code is bugged and crashes when trying to reset them.
		/// </summary>
		[HarmonyPatch(typeof(FlowUtilityNetwork), nameof(FlowUtilityNetwork.Reset))]
		public static class FlowUtilityNetwork_Reset_Patch {
			/// <summary>
			/// Applied before Reset runs.
			/// </summary>
			internal static void Prefix(FlowUtilityNetwork __instance) {
				__instance.conduits.Clear();
			}
		}

		/// <summary>
		/// Applied to Wire to add the mismatched wire menu to all wires in game.
		/// </summary>
		[HarmonyPatch(typeof(Wire), "OnSpawn")]
		public static class Wire_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Wire __instance) {
				__instance.gameObject.AddOrGet<FindMismatchedWire>();
			}
		}
	}
}
