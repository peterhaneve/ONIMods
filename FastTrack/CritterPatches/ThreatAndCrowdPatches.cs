/*
 * Copyright 2024 Peter Han
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
#if DEBUG
using PeterHan.PLib.Core;
#endif
using UnityEngine;

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
		/// <param name="confined">Will be true if the critter is now Confined.</param>
		/// <param name="cramped">Will be true if the critter is now Cramped.</param>
		/// <returns>The number of creatures in excess of the Overcrowded limit if the
		/// critter is now Overcrowded, or zero if the critter is not overcrowded.</returns>
		private static int CheckOvercrowding(OvercrowdingMonitor.Instance smi,
				out bool confined, out bool cramped) {
			var room = UpdateRoom(smi);
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
					var fishMonitor = smi.fishOvercrowdingMonitor;
					int fishCount = fishMonitor.fishCount, water = fishMonitor.cellCount;
					confined = IsConfined(smi, water);
					if (fishCount > 0)
						overcrowded = Mathf.Max(0, fishCount - water / requiredSpace);
					else {
						int cell = Grid.PosToCell(smi.transform.position);
						overcrowded = Grid.IsValidCell(cell) && Grid.IsLiquid(cell) ? 0 : 1;
					}
				} else {
					confined = IsConfined(smi, room?.numCells ?? 0);
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
		/// <param name="availableCells">The number of water/air cells in the appropriate environment.</param>
		/// <returns>true if the critter is Confined, or false otherwise.</returns>
		private static bool IsConfined(OvercrowdingMonitor.Instance smi, int availableCells) {
			bool confined = false;
			// Voles/burrowed Hatches cannot be confined, otherwise check for either
			// no room (stuck in wall) or tiny room < 1 critter space
			// Use HasAnyTags(Tag[]) here because the burrowed/digger tags will never
			// be a prefab ID
			if (!smi.kpid.HasAnyTags(IMMUNE_CONFINEMENT)) {
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
			var prefabID = smi.kpid;
			if (smi.def.spaceRequiredPerCreature <= 0)
				// No calculation required, just update room and leave
				UpdateRoom(smi);
			else {
				int overcrowded = CheckOvercrowding(smi, out bool confined, out bool cramped);
				bool wasConfined = prefabID.HasTag(GameTags.Creatures.Confined);
				bool wasCramped = prefabID.HasTag(GameTags.Creatures.Expecting);
				bool wasOvercrowded = prefabID.HasTag(GameTags.Creatures.Overcrowded);
				if (smi.isFish)
					smi.fishOvercrowdedModifier.SetValue(-overcrowded);
				else
					smi.overcrowdedModifier.SetValue(-overcrowded);
				if (wasCramped != cramped || wasConfined != confined || wasOvercrowded !=
						overcrowded > 0) {
					// Status has actually changed
					var effects = smi.effects;
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
			}
			return false;
		}

		/// <summary>
		/// A version of OvercrowdingMonitor.UpdateCavity that updates the correct rooms.
		/// </summary>
		/// <param name="smi">The overcrowding monitor to update.</param>
		/// <returns>The new room of the critter.</returns>
		private static CavityInfo UpdateRoom(OvercrowdingMonitor.Instance smi) {
			var room = smi.cavity;
			bool background = FastTrackOptions.Instance.BackgroundRoomRebuild;
			int cell = Grid.PosToCell(smi.transform.position);
			var newRoom = background ? GamePatches.BackgroundRoomProber.Instance.
				GetCavityForCell(cell) : Game.Instance.roomProber.GetCavityForCell(cell);
			if (newRoom != room) {
				var prefabID = smi.kpid;
				// Currently no rooms (checked I Love Slicksters, Butcher Stations, and Rooms
				// Expanded) use the WILDANIMAL criterion, so only light emitting critters
				// would need to actually update the rooms
				bool isEgg = prefabID.HasTag(GameTags.Egg);
				var rp = Game.Instance.roomProber;
				var brp = GamePatches.BackgroundRoomProber.Instance;
				if (room != null) {
					if (isEgg)
						room.RemoveFromCavity(prefabID, room.eggs);
					else {
						// Avoid a race on the room prober thread
						var creatures = room.creatures;
						lock (creatures) {
							room.RemoveFromCavity(prefabID, creatures);
						}
					}
					if (background)
						brp.UpdateRoom(room);
					else
						rp.UpdateRoom(room);
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
					if (background)
						brp.UpdateRoom(newRoom);
					else
						rp.UpdateRoom(newRoom);
				}
			}
			return newRoom;
		}
	}
}
