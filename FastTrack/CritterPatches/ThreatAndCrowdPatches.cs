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
using Klei.AI;
using System.Collections.Generic;
#if DEBUG
using PeterHan.PLib.Core;
#endif
using UnityEngine;

using FactionID = FactionManager.FactionID;

namespace PeterHan.FastTrack.CritterPatches {
	/// <summary>
	/// Applied to OvercrowdingMonitor to replace UpdateState with a faster version.
	/// </summary>
	[HarmonyPatch(typeof(OvercrowdingMonitor), nameof(OvercrowdingMonitor.UpdateState))]
	public static class OvercrowdingMonitor_UpdateState_Patch {
		private static readonly Tag[] IMMUNE_CONFINEMENT = {
			GameTags.Creatures.Burrowed, GameTags.Creatures.Digger
		};

		internal static bool Prepare() => FastTrackOptions.Instance.ThreatOvercrowding;

		/// <summary>
		/// A faster Overcrowded, Cramped, and Confined check that also updates the current
		/// room.
		/// </summary>
		/// <param name="smi">The overcrowding monitor to update.</param>
		/// <param name="prefabID">The critter to be updated.</param>
		/// <param name="confined">Will be true if the critter is now Confined.</param>
		/// <param name="cramped">Will be true if the critter is now Cramped.</param>
		/// <returns>The number of creatures in excess of the Overcrowded limit if the
		/// critter is now Overcrowded, or zero if the critter is not overcrowded.</returns>
		private static int CheckOvercrowding(OvercrowdingMonitor.Instance smi,
				KPrefabID prefabID, out bool confined, out bool cramped) {
			var room = UpdateRoom(smi, prefabID);
			int requiredSpace = smi.def.spaceRequiredPerCreature, overcrowded;
			// Share some checks for simplicity
			if (requiredSpace < 1) {
				confined = false;
				overcrowded = 0;
				cramped = false;
			} else {
				int eggs = 0, critters = 0;
				if (room != null) {
					eggs = room.eggs.Count;
					critters = room.creatures.Count;
				}
				if (smi.isFish) {
					var fishMonitor = smi.GetSMI<FishOvercrowdingMonitor.Instance>();
					int fishCount = fishMonitor.fishCount, water = fishMonitor.cellCount;
					confined = IsConfined(smi, prefabID, water);
					if (fishCount > 0)
						overcrowded = Mathf.Max(0, fishCount - water / requiredSpace);
					else {
						int cell = Grid.PosToCell(smi.transform.position);
						overcrowded = Grid.IsValidCell(cell) && Grid.IsLiquid(cell) ? 0 : 1;
					}
				} else {
					confined = IsConfined(smi, prefabID, room?.numCells ?? 0);
					overcrowded = (room != null && critters > 1) ? Mathf.Max(0, critters -
						room.numCells / requiredSpace) : 0;
				}
				cramped = room != null && eggs > 0 && room.numCells < (eggs + critters) *
					requiredSpace && !smi.isBaby;
			}
			return overcrowded;
		}

		/// <summary>
		/// Checks to see if a critter is Confined. There is a lot of intricate new logic for
		/// handling Pacu, so this method has gotten quite complex...
		/// </summary>
		/// <param name="smi">The overcrowding monitor to check.</param>
		/// <param name="prefabID">The critter to be updated.</param>
		/// <param name="availableCells">The number of water/air cells in the appropriate environment.</param>
		/// <returns>true if the critter is Confined, or false otherwise.</returns>
		private static bool IsConfined(OvercrowdingMonitor.Instance smi, KPrefabID prefabID,
				int availableCells) {
			bool confined = false;
			// Voles/burrowed Hatches cannot be confined, otherwise check for either
			// no room (stuck in wall) or tiny room < 1 critter space
			// Use HasAnyTags(Tag[]) here because the burrowed/digger tags will never
			// be a prefab ID
			if (!prefabID.HasAnyTags(IMMUNE_CONFINEMENT)) {
				int requiredSpace = smi.def.spaceRequiredPerCreature;
				confined = availableCells < requiredSpace;
				if (!confined && smi.isFish) {
					int cell = Grid.PosToCell(smi.transform.position);
					confined = Grid.IsValidCell(cell) && !Grid.IsLiquid(cell);
				}
			}
			return confined;
		}

		/// <summary>
		/// Applied before UpdateState runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(OvercrowdingMonitor.Instance smi) {
			var prefabID = smi.GetComponent<KPrefabID>();
			int overcrowded = CheckOvercrowding(smi, prefabID, out bool confined,
				out bool cramped);
			bool wasConfined = prefabID.HasTag(GameTags.Creatures.Confined);
			bool wasCramped = prefabID.HasTag(GameTags.Creatures.Expecting);
			bool wasOvercrowded = prefabID.HasTag(GameTags.Creatures.Overcrowded);
			smi.overcrowdedModifier.SetValue(-overcrowded);
			if (wasCramped != cramped || wasConfined != confined || wasOvercrowded !=
					overcrowded > 0) {
				// Status has actually changed
				var effects = smi.GetComponent<Effects>();
				var overcrowdedEffect = smi.isFish ? smi.fishOvercrowdedEffect : smi.
					overcrowdedEffect;
				prefabID.SetTag(GameTags.Creatures.Confined, confined);
				prefabID.SetTag(GameTags.Creatures.Overcrowded, overcrowded > 0);
				prefabID.SetTag(GameTags.Creatures.Expecting, cramped);
				if (confined) {
					effects.Add(smi.stuckEffect, false);
					effects.Remove(overcrowdedEffect);
					effects.Remove(smi.futureOvercrowdedEffect);
				} else {
					effects.Remove(smi.stuckEffect);
					if (overcrowded > 0)
						effects.Add(overcrowdedEffect, false);
					else
						effects.Remove(overcrowdedEffect);
					if (cramped)
						effects.Add(smi.futureOvercrowdedEffect, false);
					else
						effects.Remove(smi.futureOvercrowdedEffect);
				}
			}
			return false;
		}

		/// <summary>
		/// A version of OvercrowdingMonitor.UpdateCavity that updates the correct rooms.
		/// </summary>
		/// <param name="smi">The overcrowding monitor to update.</param>
		/// <param name="prefabID">The critter to be updated.</param>
		/// <returns>The new room of the critter.</returns>
		private static CavityInfo UpdateRoom(OvercrowdingMonitor.Instance smi,
				KPrefabID prefabID) {
			var room = smi.cavity;
			bool background = FastTrackOptions.Instance.BackgroundRoomRebuild;
			int cell = Grid.PosToCell(smi.transform.position);
			var newRoom = background ? GamePatches.BackgroundRoomProber.Instance.
				GetCavityForCell(cell) : Game.Instance.roomProber.GetCavityForCell(cell);
			if (newRoom != room) {
				bool isEgg = prefabID.HasTag(GameTags.Egg), light = smi.
					GetComponent<Light2D>() != null;
				// Currently no rooms (checked I Love Slicksters, Butcher Stations, and Rooms
				// Expanded) use the WILDANIMAL criterion, so only light emitting critters
				// would need to actually update the rooms.
				if (room != null) {
					if (isEgg)
						room.RemoveFromCavity(prefabID, room.eggs);
					else {
						var creatures = room.creatures;
						lock (creatures) {
							room.RemoveFromCavity(prefabID, creatures);
						}
					}
					if (light) {
						if (background)
							GamePatches.BackgroundRoomProber.Instance.UpdateRoom(room);
						else
							Game.Instance.roomProber.UpdateRoom(room);
					}
				}
				smi.cavity = newRoom;
				if (newRoom != null) {
					if (isEgg)
						newRoom.eggs.Add(prefabID);
					else {
						// Avoid a race on the room prober thread
						var creatures = newRoom.creatures;
						lock (creatures) {
							creatures.Add(prefabID);
						}
					}
					if (light) {
						if (background)
							GamePatches.BackgroundRoomProber.Instance.UpdateRoom(newRoom);
						else
							Game.Instance.roomProber.UpdateRoom(newRoom);
					}
				}
			}
			return newRoom;
		}
	}

	/// <summary>
	/// Applied to ThreatMonitor.Instance to rewrite the body and avoid needless checks,
	/// based on the reality of how factions work in ONI.
	/// </summary>
	[HarmonyPatch(typeof(ThreatMonitor.Instance), nameof(ThreatMonitor.Instance.FindThreat))]
	public static class ThreatMonitor_Instance_FindThreat_Patch {
		private const int MAX_FACTION = (int)FactionID.NumberOfFactions;

		/// <summary>
		/// The set of alignments that actually need to look for threats. Most critters are
		/// in the Pest faction which is all neutral, so the Attack disposition is unreachable,
		/// saving lots of work.
		/// </summary>
		private static ISet<FactionID> NEEDS_THREAT_SEARCH;

		internal static bool Prepare() => FastTrackOptions.Instance.ThreatOvercrowding;

		/// <summary>
		/// Finds threats for a critter.
		/// </summary>
		/// <param name="instance">The threat monitor which is looking for threats.</param>
		/// <param name="threats">The location where threats will be stored.</param>
		/// <param name="navigator">The navigator to check threat reachability.</param>
		private static void FindThreatCritter(ThreatMonitor.Instance instance,
				ICollection<FactionAlignment> threats, Navigator navigator) {
			// Base game uses hard coded 20 here
			var extents = new Extents(Grid.PosToCell(instance.transform.position), 20);
			var myAlign = instance.alignment;
			var friendly = instance.def.friendlyCreatureTags;
			var gsp = GameScenePartitioner.Instance;
			var inst = FactionManager.Instance;
			var attackables = ListPool<ScenePartitionerEntry, ThreatMonitor>.Allocate();
			gsp.GatherEntries(extents, gsp.attackableEntitiesLayer, attackables);
			int n = attackables.Count;
			for (int i = 0; i < n; i++)
				if (attackables[i]?.obj is FactionAlignment alignment && alignment !=
						myAlign && alignment.IsAlignmentActive() && inst.GetDisposition(
						myAlign.Alignment, alignment.Alignment) == FactionManager.Disposition.
						Attack) {
					bool isFriendly = false;
					if (friendly != null) {
						int f = friendly.Length;
						// This list is only ever null or 1 element long in ONI
						for (int j = 0; j < f; j++)
							if (alignment.HasTag(friendly[j])) {
								isFriendly = true;
								break;
							}
					}
					if (!isFriendly && navigator.CanReach(alignment.attackable))
						threats.Add(alignment);
				}
			attackables.Recycle();
		}

		/// <summary>
		/// Finds threats for a Duplicant.
		/// </summary>
		/// <param name="instance">The threat monitor which is looking for threats.</param>
		/// <param name="threats">The location where threats will be stored.</param>
		/// <param name="navigator">The navigator to check threat reachability.</param>
		private static void FindThreatDuplicant(ThreatMonitor.Instance instance,
				ICollection<FactionAlignment> threats, Navigator navigator) {
			if (instance.WillFight()) {
				var inst = FactionManager.Instance;
				// Skip faction 0 (Duplicant) as the Klei code does
				for (int i = (int)FactionID.Friendly; i < MAX_FACTION; i++) {
					foreach (var alignment in inst.GetFaction((FactionID)i).Members)
						if (alignment.IsPlayerTargeted() && !alignment.health.IsDefeated() &&
								navigator.CanReach(alignment.attackable))
							threats.Add(alignment);
				}
			}
		}

		/// <summary>
		/// Scans all factions to see if they could actually be threats, and record the ones
		/// that need checks.
		/// </summary>
		internal static void InitThreatList() {
			if (NEEDS_THREAT_SEARCH == null) {
				var inst = FactionManager.Instance;
				NEEDS_THREAT_SEARCH = new HashSet<FactionID>();
				for (int i = (int)FactionID.Duplicant; i < MAX_FACTION; i++) {
					var alignment = (FactionID)i;
					// If anything is set to attack, add it to the set
					for (int j = (int)FactionID.Duplicant; j < MAX_FACTION; j++)
						if (inst.GetDisposition(alignment, (FactionID)j) ==
							FactionManager.Disposition.Attack) {
							NEEDS_THREAT_SEARCH.Add(alignment);
							break;
						}
				}
#if DEBUG
				PUtil.LogDebug("Factions using threat search: " + NEEDS_THREAT_SEARCH.Join());
#endif
			}
		}

		/// <summary>
		/// Applied before FindThreat runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ThreatMonitor.Instance __instance,
				ref GameObject __result) {
			var threats = __instance.threats;
			if (__instance.isMasterNull || threats == null)
				__result = null;
			else {
				var navigator = __instance.navigator;
				threats.Clear();
				InitThreatList();
				if (__instance.IAmADuplicant)
					FindThreatDuplicant(__instance, threats, navigator);
				else if (NEEDS_THREAT_SEARCH.Contains(__instance.alignment.Alignment))
					// This branch is never reachable as Duplicant, because the Duplicant
					// faction has Attack disposition to nothing
					FindThreatCritter(__instance, threats, navigator);
				__result = threats.Count < 1 ? null : __instance.PickBestTarget(threats);
			}
			return false;
		}
	}
}
