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
using System.Collections.Generic;
using PeterHan.PLib.Core;
using UnityEngine;

using PreContext = Chore.Precondition.Context;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Groups patches used to optimize chore selection.
	/// </summary>
	internal static class ChorePatches {
		/// <summary>
		/// A much faster version of (extension) ClsuterUtil.GetMyParentWorldId.
		/// </summary>
		/// <param name="go">The game object to look up.</param>
		/// <returns>The top level world ID of that game object.</returns>
		private static int GetMyParentWorldID(GameObject go) {
			int cell = Grid.PosToCell(go.transform.position), id;
			int invalid = ClusterManager.INVALID_WORLD_IDX;
			if (Grid.IsValidCell(cell)) {
				WorldContainer world;
				int index = Grid.WorldIdx[cell];
				if (index != invalid && (world = ClusterManager.Instance.GetWorld(index)) !=
						null)
					id = world.ParentWorldId;
				else
					id = index;
			} else
				id = invalid;
			return id;
		}

		/// <summary>
		/// Merges common preconditions between the two patches to see if the fast chore
		/// optimizations can be run.
		/// </summary>
		/// <param name="consumerState">The current chore consumer's state.</param>
		/// <param name="parentWorldID">Returns the world ID to use for checking chores.</param>
		/// <returns>true to run the patch, or false not to.</returns>
		private static bool CanUseFastChores(ChoreConsumerState consumerState,
				out int parentWorldID) {
			var inst = RootMenu.Instance;
			GameObject go;
			bool result = false;
			if ((inst != null && inst.IsBuildingChorePanelActive()) || consumerState.
					selectable.IsSelected || consumerState.hasSolidTransferArm ||
					(go = consumerState.gameObject) == null)
				parentWorldID = 0;
			else {
				parentWorldID = GetMyParentWorldID(go);
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Applied to ChoreProvider to more efficiently check for chores.
		/// </summary>
		[HarmonyPatch(typeof(ChoreProvider), nameof(ChoreProvider.CollectChores))]
		internal static class ChoreProvider_CollectChores_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ChoreOpts;

			/// <summary>
			/// Applied before CollectChores runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(ChoreConsumerState consumer_state,
					ChoreProvider __instance, List<PreContext> succeeded) {
				bool run = false;
				// Avoid doing double the work on the patch that GCP already has
				if (__instance.GetType() != typeof(GlobalChoreProvider) && CanUseFastChores(
						consumer_state, out int worldID) && __instance.choreWorldMap.
						TryGetValue(worldID, out var chores) && chores != null) {
					var ci = ChoreComparator.Instance;
					run = ci.Setup(consumer_state, succeeded);
					if (run) {
						ci.CollectNonFetch(chores);
						ci.Cleanup();
					}
				}
				return !run;
			}
		}

		/// <summary>
		/// Applied to GlobalChoreProvider to more efficiently check for chores.
		/// </summary>
		[HarmonyPatch(typeof(GlobalChoreProvider), nameof(GlobalChoreProvider.CollectChores))]
		internal static class GlobalChoreProvider_CollectChores_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ChoreOpts;

			/// <summary>
			/// Applied before CollectChores runs.
			/// </summary>
			[HarmonyPriority(Priority.LowerThanNormal)]
			internal static bool Prefix(ChoreConsumerState consumer_state,
					GlobalChoreProvider __instance, List<PreContext> succeeded) {
				bool run = false;
				if (CanUseFastChores(consumer_state, out int worldID)) {
					var ci = ChoreComparator.Instance;
					run = ci.Setup(consumer_state, succeeded);
					if (run) {
						var fetches = __instance.fetches;
						if (__instance.choreWorldMap.TryGetValue(worldID, out var chores))
							ci.CollectNonFetch(chores);
						ci.CollectSweep(__instance.clearableManager);
						int n = fetches.Count;
						for (int i = 0; i < n; i++)
							ci.Collect(fetches[i].chore);
						ci.Cleanup();
					}
				}
				return !run;
			}
		}
	}
}
