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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using OnNetworksRebuilt = System.Action<System.Collections.Generic.IList<UtilityNetwork>,
	System.Collections.Generic.ICollection<int>>;
using SolidUtilityNetworkManager = UtilityNetworkManager<FlowUtilityNetwork, SolidConduit>;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.ConduitPatches {
	/// <summary>
	/// Extracts the UtilityNetworkManager.Update method for background updates. Will only be
	/// used for solid utility networks.
	/// </summary>
	[HarmonyPatch(typeof(SolidUtilityNetworkManager), nameof(SolidUtilityNetworkManager.
		Update))]
	public static class SolidUtilityNetworkUpdater {
		/// <summary>
		/// Retrieves the registered network rebuild listeners.
		/// </summary>
		private static readonly IDetouredField<SolidUtilityNetworkManager, OnNetworksRebuilt>
			ON_NETWORKS_REBUILT = PDetours.DetourField<SolidUtilityNetworkManager,
			OnNetworksRebuilt>("onNetworksRebuilt");

		/// <summary>
		/// Retrieves the list of physical node cell locations.
		/// </summary>
		private static readonly IDetouredField<SolidUtilityNetworkManager, ISet<int>>
			PHYSICAL_NODES = PDetours.DetourField<SolidUtilityNetworkManager, ISet<int>>(
			"physicalNodes");

		internal static bool Prepare() => FastTrackOptions.Instance.ConduitOpts;

		/// <summary>
		/// Fires the update events for a conduit network manager after it is rebuilt.
		/// </summary>
		/// <param name="manager">The conduit network manager to trigger.</param>
		internal static void TriggerEvent(SolidUtilityNetworkManager manager) {
			var action = ON_NETWORKS_REBUILT.Get(manager);
			if (action != null)
				action.Invoke(manager.GetNetworks(), PHYSICAL_NODES.Get(manager));
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(nameof(SolidUtilityNetworkManager.Update))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void Update(SolidUtilityNetworkManager instance) {
			/// <summary>
			/// Transpiles Update to avoid calling onNetworksRebuilt.
			/// </summary>
			TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var remove = typeof(OnNetworksRebuilt).GetMethodSafe(nameof(OnNetworksRebuilt.
					Invoke), false, PPatchTools.AnyArguments);
				return (remove == null) ? instructions : PPatchTools.ReplaceMethodCall(
					instructions, remove, null);
			}
			_ = instance;
			_ = Transpiler(null);

			// Dummy code to ensure no inlining
			while (System.DateTime.Now.Ticks > 0L)
				throw new NotImplementedException("Reverse patch stub");
		}
	}
}
