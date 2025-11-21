/*
 * Copyright 2025 Peter Han
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
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.AIImprovements {
	/// <summary>
	/// Patches which will be applied via annotations for AI Improvements.
	/// </summary>
	public sealed class AIImprovementsPatches : KMod.UserMod2 {
		private static readonly IDetouredField<Constructable, Building> CONS_BUILDING =
			PDetours.DetourFieldLazy<Constructable, Building>("building");

		// TODO Remove when versions prior to U57-699077 no longer need to be supported
		private delegate bool NavigatorStop(Navigator navigator);

		private static readonly NavigatorStop NAVIGATOR_STOP = typeof(Navigator).
			Detour<NavigatorStop>(nameof(Navigator.Stop));

		/// <summary>
		/// The chore type used for Build chores.
		/// </summary>
		internal static ChoreType BuildChore { get; private set; }

		/// <summary>
		/// The chore type used for Deconstruct chores.
		/// </summary>
		internal static ChoreType DeconstructChore { get; private set; }

		/// <summary>
		/// The options to use.
		/// </summary>
		internal static AIImprovementsOptionsInstance Options { get; private set; }

		/// <summary>
		/// Queues up chores that need to be reprioritized on the main thread.
		/// </summary>
		internal static readonly ConcurrentDictionary<Chore, bool> reprioritize =
			new ConcurrentDictionary<Chore, bool>(4, 256);

		/// <summary>
		/// Adjusts build priorities based on the options.
		/// </summary>
		/// <param name="priorityMod">The priority to modify.</param>
		/// <param name="chore">The parent chore.</param>
		private static void AdjustBuildPriority(ref int priorityMod, Chore chore) {
			BuildingDef def;
			Building building;
			// The queue would work, but the Building field is required and faster
			if (chore.target is Constructable target && target != null && (building =
					CONS_BUILDING.Get(target)) != null && (def = building.Def) != null) {
				string id = def.PrefabID;
				if (Options.PrioritizeBuildings.Contains(id))
					priorityMod += AIImprovementsOptions.BUILD_PRIORIY_MOD;
				else if (Options.DeprioritizeBuildings.Contains(id))
					priorityMod -= AIImprovementsOptions.BUILD_PRIORIY_MOD;
				if (def.IsFoundation) {
					if (Game.IsOnMainThread()) {
						// Avoid building a tile which would block a location recently used by
						// a dupe
						int cell = Grid.PosToCell(target);
						if (AllMinionsLocationHistory.Instance.WasRecentlyOccupied(cell))
							priorityMod -= AIImprovementsOptions.BLOCK_PRIORITY_MOD;
					} else
						reprioritize.TryAdd(chore, true);
				}
			}
		}

		/// <summary>
		/// Adjusts deconstruct priorities based on the options.
		/// </summary>
		/// <param name="priorityMod">The priority to modify.</param>
		/// <param name="chore">The parent chore.</param>
		private static void AdjustDeconstructPriority(ref int priorityMod, Chore chore) {
			BuildingDef def;
			if (chore.target is Deconstructable target && target != null) {
				if (!Game.IsOnMainThread())
					// Reprioritize the chore
					reprioritize.TryAdd(chore, true);
				else if (target.TryGetComponent(out Building building) &&
						(def = building.Def) != null) {
					string id = def.PrefabID;
					if (Options.PrioritizeBuildings.Contains(id))
						priorityMod -= AIImprovementsOptions.BUILD_PRIORIY_MOD;
					else if (Options.DeprioritizeBuildings.Contains(id))
						priorityMod += AIImprovementsOptions.BUILD_PRIORIY_MOD;
					if (def.IsFoundation) {
						// Avoid destroying a tile recently stood on by a dupe
						int cell = Grid.CellAbove(Grid.PosToCell(target));
						if (AllMinionsLocationHistory.Instance.WasRecentlyOccupied(cell))
							priorityMod -= AIImprovementsOptions.BLOCK_PRIORITY_MOD;
					}
				}
			}
		}

		/// <summary>
		/// Moves the Duplicant from the cell to a destination cell, using a smooth transition
		/// if possible.
		/// </summary>
		/// <param name="instance">The fall monitor to update if successful.</param>
		/// <param name="destination">The destination cell.</param>
		/// <param name="navigator">The navigator to move.</param>
		/// <param name="flipEmote">Returns whether the emote should be reversed.</param>
		private static void ForceMoveTo(FallMonitor.Instance instance, int destination,
				Navigator navigator, ref bool flipEmote) {
			var transform = instance.transform;
			// Teleport to the new location
			transform.SetPosition(Grid.CellToPosCBC(destination, Grid.SceneLayer.Move));
			NAVIGATOR_STOP.Invoke(navigator);
			if (instance.gameObject.HasTag(GameTags.Incapacitated))
				navigator.SetCurrentNavType(NavType.Floor);
			instance.UpdateFalling();
			// If they get pushed into entombment, start entombment animation
			instance.GoTo(instance.sm.isEntombed.Get(instance) ? instance.sm.entombed.
				stuck : instance.sm.standing);
		}

		/// <summary>
		/// Determines whether a Duplicant could plausibly navigate to the target cell. Now
		/// also checks the cell above due to how entombment works post Mergedown.
		/// </summary>
		/// <param name="navigator">The Duplicant to check.</param>
		/// <param name="cell">The destination cell.</param>
		/// <returns>true if the Duplicant could move there, or false otherwise.</returns>
		internal static bool IsValidNavCell(Navigator navigator, int cell) {
			var navType = navigator.CurrentNavType;
			int above = Grid.CellAbove(cell);
			return navigator.NavGrid.NavTable.IsValid(cell, navType) && !Grid.
				Solid[cell] && !Grid.DupeImpassable[cell] && Grid.IsValidCell(above) &&
				!Grid.Solid[above] && !Grid.DupeImpassable[above] && !Grid.HasDoor[cell];
		}

		/// <summary>
		/// Applied to MinionConfig to add the navigator transition for keeping track of
		/// their location history. Big Brother is watching...
		/// </summary>
		[PLibPatch(RunAt.AfterDbInit, nameof(MinionConfig.OnSpawn),
			RequireType = nameof(MinionConfig), PatchType = HarmonyPatchType.Postfix)]
		internal static void MinionSpawn_Postfix(GameObject go) {
			if (go.TryGetComponent(out Navigator nav))
				nav.transitionDriver.overrideLayers.Add(new LocationHistoryTransitionLayer(
					nav));
		}

		[PLibMethod(RunAt.AfterDbInit)]
		internal static void OnDbInit() {
			var db = Db.Get();
			BuildChore = db.ChoreTypes.Build;
			DeconstructChore = db.ChoreTypes.Deconstruct;
		}

		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
#if DEBUG
			PUtil.LogDebug("Destroying AllMinionsLocationHistory");
#endif
			AllMinionsLocationHistory.DestroyInstance();
			reprioritize.Clear();
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			Options = AIImprovementsOptionsInstance.Create(POptions.ReadSettings<
				AIImprovementsOptions>() ?? new AIImprovementsOptions());
#if DEBUG
			PUtil.LogDebug("Creating AllMinionsLocationHistory");
#endif
			AllMinionsLocationHistory.InitInstance();
		}

		/// <summary>
		/// Tries to move a Duplicant to a more sensible location when entombed or falling.
		/// </summary>
		/// <param name="layer">The location history of the Duplicant.</param>
		/// <param name="navigator">The Duplicant to check.</param>
		/// <param name="instance">The fall monitor to update if successful.</param>
		/// <param name="flipEmote">Returns whether the emote should be reversed.</param>
		/// <returns>true if the Duplicant was successfully moved away, or false otherwise.</returns>
		private static bool TryEscape(LocationHistoryTransitionLayer layer,
				Navigator navigator, FallMonitor.Instance instance, ref bool flipEmote) {
			bool moved = false;
			for (int i = 0; i < LocationHistoryTransitionLayer.TRACK_CELLS && !moved; i++) {
				int last = layer.VisitedCells[i];
				if (Grid.IsValidCell(last) && IsValidNavCell(navigator, last)) {
#if DEBUG
					PUtil.LogDebug("{0} is in trouble, trying to escape to {1:D}".F(navigator.
						gameObject?.name, last));
#endif
					ForceMoveTo(instance, last, navigator, ref flipEmote);
					// Prevents a loop back and forth between two cells in the history
					layer.Reset();
					moved = true;
				}
			}
			return moved;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			Options = new AIImprovementsOptionsInstance();
			new POptions().RegisterOptions(this, typeof(AIImprovementsOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(AIImprovementsPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}
		
		/// <summary>
		/// Applied to Chore.Precondition.Context to finish required chore priority
		/// evaluations on the main thread.
		/// </summary>
		[HarmonyPatch(typeof(Chore.Precondition.Context), nameof(Chore.Precondition.Context.
			FinishPreconditions))]
		public static class Chore_Context_FinishPreconditions_Patch {
			/// <summary>
			/// Applied after FinishPreconditions runs.
			/// </summary>
			internal static void Postfix(ref int ___priorityMod, Chore ___chore) {
				if (BuildChore != null && ___chore.choreType == BuildChore && reprioritize.
						TryRemove(___chore, out _))
					AdjustBuildPriority(ref ___priorityMod, ___chore);
				else if (DeconstructChore != null && ___chore.choreType == DeconstructChore &&
						reprioritize.TryRemove(___chore, out _))
					AdjustDeconstructPriority(ref ___priorityMod, ___chore);
			}
		}

		/// <summary>
		/// Applied to Chore.Precondition.Context to adjust the priority modifier on chores
		/// slightly for specific chore classes.
		/// </summary>
		[HarmonyPatch(typeof(Chore.Precondition.Context), nameof(Chore.Precondition.Context.
			SetPriority))]
		public static class Chore_Context_SetPriority_Patch {
			/// <summary>
			/// Applied after SetPriority runs.
			/// </summary>
			internal static void Postfix(ref int ___priorityMod, Chore chore) {
				if (BuildChore != null && chore.choreType == BuildChore)
					AdjustBuildPriority(ref ___priorityMod, chore);
				else if (DeconstructChore != null && chore.choreType == DeconstructChore)
					AdjustDeconstructPriority(ref ___priorityMod, chore);
			}
		}

		/// <summary>
		/// Applied to ChoreConsumer to clean up the reprioritize list after all chores are
		/// collected.
		/// </summary>
		[HarmonyPatch(typeof(ChoreConsumer), nameof(ChoreConsumer.FindNextChore))]
		public static class ChoreConsumer_FindNextChore_Patch {
			/// <summary>
			/// Applied after FindNextChore runs.
			/// </summary>
			internal static void Postfix() {
				reprioritize.Clear();
			}
		}

		/// <summary>
		/// Applied to FallMonitor.Instance to try and back out to a previously visited tile
		/// if the floor is removed from under a Duplicant or they become entombed.
		/// </summary>
		[HarmonyPatch]
		public static class FallMonitor_Instance_Recover_Patch {
			internal static IEnumerable<MethodBase> TargetMethods() {
				yield return typeof(FallMonitor.Instance).GetMethodSafe(nameof(FallMonitor.
					Instance.Recover), false, PPatchTools.AnyArguments);
				yield return typeof(FallMonitor.Instance).GetMethodSafe(nameof(FallMonitor.
					Instance.TryEntombedEscape), false, PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Applied before Recover runs.
			/// </summary>
			internal static bool Prefix(FallMonitor.Instance __instance,
					Navigator ___navigator, ref bool ___flipRecoverEmote) {
				// This is not run too often so searching is fine
				bool moved = false;
				if (___navigator != null) {
					var layers = ___navigator.transitionDriver?.overrideLayers;
					if (layers != null)
						foreach (var layer in layers)
							if (layer is LocationHistoryTransitionLayer lhs) {
								moved = TryEscape(lhs, ___navigator, __instance,
									ref ___flipRecoverEmote);
								if (moved) break;
							}
				}
				return !moved;
			}
		}
	}
}
