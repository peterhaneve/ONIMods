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
using UnityEngine;

namespace PeterHan.CleanDrop {
	/// <summary>
	/// Patches which will be applied via annotations for CleanDrop.
	/// </summary>
	public sealed class CleanDropPatches : KMod.UserMod2 {
		/// <summary>
		/// Creates the drop manager based on the current world size.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void CreateCleanDrop() {
			CleanDropManager.CreateInstance();
			PUtil.LogDebug("Created CleanDropManager");
		}

		/// <summary>
		/// Destroy the drop manager when the game is closed.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void DestroyCleanDrop() {
			PUtil.LogDebug("Destroying CleanDropManager");
			CleanDropManager.DestroyInstance();
		}

		/// <summary>
		/// Marks the direction where a worker was standing.
		/// </summary>
		/// <param name="instance">The target workable.</param>
		/// <param name="worker">The Duplicant working the task.</param>
		private static void MarkDirection(Workable instance, Worker worker) {
			var inst = CleanDropManager.Instance;
			if (inst != null && worker != null) {
				int targetCell = instance.GetCell();
				inst[targetCell] = CleanDropManager.GetWorkerDirection(Grid.PosToCell(worker),
					targetCell);
#if DEBUG
				Grid.CellToXY(targetCell, out int x, out int y);
				PUtil.LogDebug("Mark workable {0} in cell ({1:D}, {2:D}) direction = {3}".F(
					instance.GetType().FullName, x, y, inst[targetCell]));
#endif
			}
		}

		/// <summary>
		/// Attempts to move the pickupable to a position better suited for accessibility. If
		/// the pickupable is not inside a solid (and thus does not need moving), nothing
		/// happens.
		/// </summary>
		/// <param name="material">The pickupable to move.</param>
		private static void MovePreferredPosition(Pickupable material) {
			int cell = Grid.PosToCell(material);
			var inst = CleanDropManager.Instance;
			var obj = material.gameObject;
			LastUsedDirection direction;
			if (inst != null && Grid.IsValidCell(cell) && (direction = inst[cell]) !=
					LastUsedDirection.None && obj.GetSMI<DeathMonitor.Instance>()?.IsDead() !=
					false && ((Grid.Solid[cell] && Grid.Foundation[cell]) || Grid.Properties[
					cell] != 0)) {
				var tryFirst = ListPool<int, CleanDropManager>.Allocate();
#if DEBUG
				Grid.CellToXY(cell, out int x, out int y);
				PUtil.LogDebug("Item {0} in cell ({1:D}, {2:D}) last direction = {2}".F(
					material.PrimaryElement.Element.name, x, y, direction));
#endif
				// Direction based on workable cell; default direction is U D R L
				switch (direction) {
				case LastUsedDirection.Down:
					tryFirst.Add(Grid.CellBelow(cell));
					tryFirst.Add(Grid.CellDownRight(cell));
					tryFirst.Add(Grid.CellDownLeft(cell));
					break;
				case LastUsedDirection.DownLeft:
					tryFirst.Add(Grid.CellDownLeft(cell));
					tryFirst.Add(Grid.CellBelow(cell));
					tryFirst.Add(Grid.CellLeft(cell));
					break;
				case LastUsedDirection.DownRight:
					tryFirst.Add(Grid.CellDownRight(cell));
					tryFirst.Add(Grid.CellBelow(cell));
					tryFirst.Add(Grid.CellRight(cell));
					break;
				case LastUsedDirection.Left:
					tryFirst.Add(Grid.CellLeft(cell));
					tryFirst.Add(Grid.CellUpLeft(cell));
					tryFirst.Add(Grid.CellDownLeft(cell));
					break;
				case LastUsedDirection.Right:
					tryFirst.Add(Grid.CellRight(cell));
					tryFirst.Add(Grid.CellUpRight(cell));
					tryFirst.Add(Grid.CellDownRight(cell));
					break;
				case LastUsedDirection.Up:
					tryFirst.Add(Grid.CellAbove(cell));
					tryFirst.Add(Grid.CellUpRight(cell));
					tryFirst.Add(Grid.CellUpLeft(cell));
					break;
				case LastUsedDirection.UpLeft:
					tryFirst.Add(Grid.CellUpLeft(cell));
					tryFirst.Add(Grid.CellAbove(cell));
					tryFirst.Add(Grid.CellLeft(cell));
					break;
				case LastUsedDirection.UpRight:
					tryFirst.Add(Grid.CellUpRight(cell));
					tryFirst.Add(Grid.CellAbove(cell));
					tryFirst.Add(Grid.CellRight(cell));
					break;
				default:
					break;
				}
				foreach (int tryCell in tryFirst)
					if (Grid.IsValidCell(tryCell) && !Grid.Solid[tryCell]) {
						var position = Grid.CellToPosCBC(tryCell, Grid.SceneLayer.Move);
						var collider = obj.GetComponent<KCollider2D>();
						// Adjust for material's bounding box
						if (collider != null)
							position.y += obj.transform.GetPosition().y - collider.bounds.min.y;
						obj.transform.SetPosition(position);
						// Make the pickupable start falling if not a dupe/critter
						if (obj.GetComponent<Health>() == null) {
							if (GameComps.Fallers.Has(obj))
								GameComps.Fallers.Remove(obj);
							GameComps.Fallers.Add(obj, Vector2.zero);
						}
						break;
					}
				// Do not reset the direction to None since multiple items could drop from
				// one workable
				tryFirst.Recycle();
			}
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(CleanDropPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to Constructable.
		/// </summary>
		[HarmonyPatch(typeof(Constructable), "OnCompleteWork")]
		public static class Constructable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(Constructable __instance, Worker worker) {
				if (__instance.IsReplacementTile)
					MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to Deconstructable to mark the direction where the worker was standing
		/// when the deconstruction occurs.
		/// </summary>
		[HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
		public static class Deconstructable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(Deconstructable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to DropAllWorkable to mark the direction where the worker was standing
		/// when the drop occurs.
		/// </summary>
		[HarmonyPatch(typeof(DropAllWorkable), "OnCompleteWork")]
		public static class DropAllWorkable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(DropAllWorkable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to EmptyConduitWorkable to mark the direction where the worker was standing
		/// during the job. Note that since the errand can continuously emit bottles/canisters,
		/// the location is saved while work is in progress.
		/// </summary>
		[HarmonyPatch(typeof(EmptyConduitWorkable), "OnWorkTick")]
		public static class EmptyConduitWorkable_OnWorkTick_Patch {
			/// <summary>
			/// Applied before OnWorkTick runs.
			/// </summary>
			internal static void Prefix(EmptyConduitWorkable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to Harvestable to mark the direction where the worker was standing when
		/// the harvest occurs.
		/// </summary>
		[HarmonyPatch(typeof(Harvestable), "OnCompleteWork")]
		public static class Harvestable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(Harvestable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to Pickupable to pick a preferred offset direction if there is context
		/// available about a direction that might be better.
		/// </summary>
		[HarmonyPatch(typeof(Pickupable), nameof(Pickupable.TryToOffsetIfBuried))]
		public static class Pickupable_TryToOffsetIfBuried_Patch {
			/// <summary>
			/// Applied before TryToOffsetIfBuried runs.
			/// </summary>
			internal static void Prefix(Pickupable __instance) {
				if (__instance != null) {
					var prefabID = __instance.KPrefabID;
					// Must not be in a storage or attached to a Duplicant/critter
					if (!prefabID.HasTag(GameTags.Stored) && !prefabID.HasTag(GameTags.
							Equipped))
						MovePreferredPosition(__instance);
				}
			}
		}

		/// <summary>
		/// Applied to Repairable to mark the direction where the worker was standing during
		/// repair. Note that since the storage might get destroyed before the repair errand
		/// is stopped or completed, the location is saved when work begins.
		/// </summary>
		[HarmonyPatch(typeof(Repairable), "OnStartWork")]
		public static class Repairable_OnStartWork_Patch {
			/// <summary>
			/// Applied after OnStartWork runs.
			/// </summary>
			internal static void Postfix(Repairable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}

		/// <summary>
		/// Applied to Uprootable to mark the direction where the worker was standing when
		/// the uproot occurs.
		/// </summary>
		[HarmonyPatch(typeof(Uprootable), "OnCompleteWork")]
		public static class Uprootable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(Uprootable __instance, Worker worker) {
				MarkDirection(__instance, worker);
			}
		}
	}
}
