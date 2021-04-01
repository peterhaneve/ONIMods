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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Patches which will be applied via annotations for Stock Bug Fix only on the Spaced Out!
	/// DLC. Must be compiled against the DLC but runs fine against vanilla.
	/// TODO Vanilla/DLC code
	/// </summary>
	internal static class DLCSpecificPatches {
		private static readonly Type CLUSTER_MANAGER = PPatchTools.GetTypeSafe(
			"ClusterManager");

		/// <summary>
		/// A fixed version of DeleteWorldObjects that waits for a short time to give Sim the
		/// time to destroy the internal cells.
		/// </summary>
		/// <param name="world">The world that is being destroyed.</param>
		/// <param name="spawnPosition">The location to spawn the objects.</param>
		private static System.Collections.IEnumerator DeleteWorldObjects(Component inst,
				Component world, GameObject _, Vector3 spawnPosition) {
			yield return new WaitForSeconds(1.0f);
			yield return new WaitForEndOfFrame();
			if (world != null) {
				if (world is WorldContainer wc) {
					wc.TransferResourcesToParentWorld(spawnPosition);
					Grid.FreeGridSpace(wc.WorldSize, wc.WorldOffset);
#if DEBUG
					PUtil.LogDebug("Clearing resources and grid space for " + wc.worldName);
#endif
				}
				var inventory = world.GetComponent<WorldInventory>();
				if (inventory != null)
					UnityEngine.Object.Destroy(inventory);
				UnityEngine.Object.Destroy(world);
			}
			yield break;
		}

		public static void PostPatch(HarmonyInstance instance) {
			var destroyWorld = CLUSTER_MANAGER?.GetMethodSafe(nameof(ClusterManager.
				DestoryRocketInteriorWorld), false, PPatchTools.AnyArguments);
			if (destroyWorld != null) {
				PUtil.LogDebug("Applying DLC specific patches");
				instance.Patch(destroyWorld, transpiler: new HarmonyMethod(
					typeof(DLCSpecificPatches), nameof(TranspileDestroyWorld)));
			}
		}

		/// <summary>
		/// Transpiles ClusterManager.DestoryRocketInteriorWorld to fix a race condition that
		/// causes rocket wall and window tiles to drop their materials on deconstruct.
		/// </summary>
		private static IEnumerable<CodeInstruction> TranspileDestroyWorld(
				IEnumerable<CodeInstruction> method) {
			return PPatchTools.ReplaceMethodCall(method, CLUSTER_MANAGER.GetMethodSafe(
				"DeleteWorldObjects", false, typeof(WorldContainer), typeof(GameObject),
				typeof(Vector3)), typeof(DLCSpecificPatches).GetMethodSafe(nameof(
				DLCSpecificPatches.DeleteWorldObjects), true, PPatchTools.AnyArguments));
		}
	}
}
