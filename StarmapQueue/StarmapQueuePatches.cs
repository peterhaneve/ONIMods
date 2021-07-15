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

using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using System.Collections.Generic;

namespace PeterHan.StarmapQueue {
	/// <summary>
	/// Patches which will be applied via annotations for Starmap Queue.
	/// </summary>
	public sealed class StarmapQueuePatches : KMod.UserMod2 {
		/// <summary>
		/// The substitute method which queues up the next destination when the old one would
		/// simply remove the destination. Note that the value of the second parameter is
		/// always -1 in practice.
		/// </summary>
		private static void QueueNextDestination(SpacecraftManager instance, int toSet) {
			int oldID = instance.GetStarmapAnalysisDestinationID();
			SpaceDestination oldDest;
			if (toSet != oldID && toSet == -1 && (oldDest = instance.GetDestination(oldID)) !=
					null && instance.GetDestinationAnalysisState(oldDest) == SpacecraftManager.
					DestinationAnalysisState.Complete) {
				// Destination was just discovered
				int dist = oldDest.distance, minDist = int.MaxValue;
				SpaceDestination next = null;
				foreach (var dest in instance.destinations) {
					int newDist = dest.distance;
					if (instance.GetDestinationAnalysisState(dest) != SpacecraftManager.
							DestinationAnalysisState.Complete && newDist >= dist &&
							newDist < minDist) {
						// At a further or equal distance, and not complete, but visible
						// (closest unanalyzed planet)
						next = dest;
						minDist = newDist;
					}
				}
#if DEBUG
				if (next != null)
					PUtil.LogDebug("Discovered planet: {0:D}, Queueing: {1:D} ({2})".
						F(oldID, next.id, next.type));
				else
					PUtil.LogDebug("Discovered planet: {0:D}, but none left!".F(oldID));
#endif
				if (next != null) {
					toSet = next.id;
				}
				// Add notification "Starmap destination discovered"
				var obj = instance.gameObject;
				if (obj != null) {
					obj.AddOrGet<Notifier>()?.Add(new MessageNotification(
						new StarmapDiscoveryMessage(oldDest)));
				}
			}
			instance.SetStarmapAnalysisDestinationID(toSet);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to SpacecraftManager to queue up a new destination when one is discovered.
		/// </summary>
		[HarmonyPatch(typeof(SpacecraftManager), nameof(SpacecraftManager.
			EarnDestinationAnalysisPoints))]
		public static class SpacecraftManager_EarnDestinationAnalysisPoints_Patch {
			/// <summary>
			/// Transpiles EarnDestinationAnalysisPoints to replace the cancel destination call
			/// with our queue call.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var oldMethod = PPatchTools.GetMethodSafe(typeof(SpacecraftManager),
					"SetStarmapAnalysisDestinationID", false, typeof(int));
				var newMethod = PPatchTools.GetMethodSafe(typeof(StarmapQueuePatches),
					nameof(QueueNextDestination), true, typeof(SpacecraftManager), typeof(int));
				if (oldMethod != null && newMethod != null)
					return PPatchTools.ReplaceMethodCall(method, oldMethod, newMethod);
				else {
					PUtil.LogWarning("Unable to patch starmap queue: method not found");
					return method;
				}
			}
		}
	}
}
