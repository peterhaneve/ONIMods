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
using UnityEngine;

namespace PeterHan.FastTrack.CritterPatches {
	/// <summary>
	/// Applied to HugMonitor.Instance to optimize some horribly slow code that finds eggs
	/// to hug.
	/// </summary>
	[HarmonyPatch(typeof(HugMonitor.Instance), nameof(HugMonitor.Instance.FindEgg))]
	public static class HugMonitor_Instance_FindEgg_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ThreatOvercrowding;

		/// <summary>
		/// Checks to see if a target object is an egg that can be hugged.
		/// </summary>
		/// <param name="egg">The egg to check.</param>
		/// <returns>true if the object is an egg and can be hugged; or false otherwise.</returns>
		private static bool IsValidEgg(GameObject egg) {
			return egg != null && egg.TryGetComponent(out KPrefabID prefabID) && !prefabID.
				HasTag(GameTags.Creatures.ReservedByCreature) && prefabID.HasTag(GameTags.
				Egg) && egg.TryGetComponent(out Klei.AI.Effects effects) && !effects.
				HasEffect("EggHug");
		}

		/// <summary>
		/// Applied before FindEgg runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(HugMonitor.Instance __instance) {
			GameObject hugTarget = null;
			var master = __instance.GetMaster();
			var targets = ListPool<ScenePartitionerEntry, HugMonitor>.Allocate();
			var validEggs = ListPool<GameObject, HugMonitor>.Allocate();
			var extents = new Extents(Grid.PosToCell(master.transform.position), 10);
			var navigator = master.GetComponent<Navigator>();                                                      
			var gsp = GameScenePartitioner.Instance;
			// Look for incubators
			gsp.GatherEntries(extents, gsp.completeBuildings, targets);
			int n = targets.Count;
			for (int i = 0; i < n; i++)
				if (targets[i].obj is KMonoBehaviour target && target != null && target.
						TryGetComponent(out KPrefabID prefabID) && !prefabID.HasTag(GameTags.
						Creatures.ReservedByCreature) && target.TryGetComponent(
						out EggIncubator incubator)) {
					// With valid eggs inside
					int cell = Grid.PosToCell(target.transform.position);
					var egg = incubator.Occupant;
					if (Grid.IsValidCell(cell) && navigator.CanReach(cell) && IsValidEgg(egg))
						validEggs.Add(target.gameObject);
				}
			targets.Clear();
			gsp.GatherEntries(extents, gsp.pickupablesLayer, targets);
			n = targets.Count;
			// Look for eggs on the ground
			for (int i = 0; i < n; i++)
				if (targets[i].obj is KMonoBehaviour target && target != null) {
					var go = target.gameObject;
					if (IsValidEgg(go))
						validEggs.Add(go);
				}
			if (validEggs.Count > 0)
				hugTarget = validEggs[Random.Range(0, validEggs.Count)];
			targets.Recycle();
			validEggs.Recycle();
			__instance.hugTarget = hugTarget;
			return false;
		}
	}
}
