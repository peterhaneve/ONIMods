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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FallingSand {
	/// <summary>
	/// Patches which will be applied via annotations for Falling Sand.
	/// </summary>
	public sealed class FallingSandPatches : KMod.UserMod2 {
		/// <summary>
		/// Checks a falling object to see if a dig errand must be placed.
		/// </summary>
		/// <param name="obj">The object which is falling.</param>
		private static void CheckFallingObject(GameObject obj) {
			var dig = obj.GetComponentSafe<FallFromDigging>();
			int cell;
			if (dig != null && Grid.IsValidCell(cell = Grid.PosToCell(obj.transform.
					GetPosition())) && Grid.IsVisible(cell)) {
				// Did it land somewhere visible?
				int below = Grid.CellBelow(cell);
				if (Grid.IsValidCell(below) && (Grid.Element[below].IsSolid || (Grid.
						Properties[below] & (int)Sim.Cell.Properties.SolidImpermeable) != 0))
					FallingSandManager.Instance.QueueDig(cell, dig.Priority);
			}
		}

		/// <summary>
		/// Stops tracking all digging errands.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void Cleanup() {
			FallingSandManager.Instance.ClearAll();
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(FallingSandPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to Diggable to add a tracking component to objects which fall when
		/// digging.
		/// </summary>
		[HarmonyPatch(typeof(Diggable), "OnWorkTick")]
		public static class Diggable_OnWorkTick_Patch {
			internal static void Postfix(Diggable __instance) {
				FallingSandManager.Instance.TrackDiggable(__instance);
			}
		}

		/// <summary>
		/// Applied to Diggable to stop tracking digs which are destroyed.
		/// </summary>
		[HarmonyPatch(typeof(Diggable), "OnCleanUp")]
		public static class Diggable_OnCleanUp_Patch {
			internal static void Postfix(Diggable __instance) {
				FallingSandManager.Instance.UntrackDiggable(__instance);
			}
		}

		/// <summary>
		/// Applied to UnstableGroundManager to actually place the digs, now that the blocks
		/// are solidified.
		/// </summary>
		[HarmonyPatch(typeof(UnstableGroundManager), "RemoveFromPending")]
		public static class UnstableGroundManager_RemoveFromPending_Patch {
			internal static void Postfix(int cell, List<int> ___pendingCells) {
				FallingSandManager.Instance.CheckDigQueue(cell);
				if (___pendingCells.Count < 1)
					FallingSandManager.Instance.ClearDig();
			}
		}

		/// <summary>
		/// Applied to UnstableGroundManager to flag spawned falling objects with the
		/// appropriate component.
		/// </summary>
		[HarmonyPatch(typeof(UnstableGroundManager), nameof(UnstableGroundManager.Spawn),
			typeof(int), typeof(Element), typeof(float), typeof(float), typeof(byte),
			typeof(int))]
		public static class UnstableGroundManager_Spawn_Patch {
			internal static void Postfix(List<GameObject> ___fallingObjects, int cell) {
				int n = ___fallingObjects.Count;
				GameObject obj;
				Diggable cause;
				// Actually caused by digging?
				if (n > 0 && (obj = ___fallingObjects[n - 1].gameObject) != null &&
						(cause = FallingSandManager.Instance.FindDigErrand(cell)) != null) {
					// Should never be null since object was just spawned
					var component = obj.AddComponent<FallFromDigging>();
					var xy = Grid.CellToXY(cell);
#if DEBUG
					PUtil.LogDebug("Digging induced: {0} @ ({1:D},{2:D})".F(obj.name,
						xy.X, xy.Y));
#endif
					// Unity equals operator strikes again
					var digPri = cause.gameObject.GetComponentSafe<Prioritizable>();
					if (digPri != null)
						component.Priority = digPri.GetMasterPriority();
				}
			}
		}

		/// <summary>
		/// Applied to UnstableGroundManager to queue up dig errands on falling objects which
		/// are about to become solid.
		/// </summary>
		[HarmonyPatch(typeof(UnstableGroundManager), "Update")]
		public static class UnstableGroundManager_Update_Patch {
			internal static void Prefix(List<GameObject> ___fallingObjects) {
				foreach (var obj in ___fallingObjects)
					if (obj != null)
						CheckFallingObject(obj);
			}
		}
	}
}
