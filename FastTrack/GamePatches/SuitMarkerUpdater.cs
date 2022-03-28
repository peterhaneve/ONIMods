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
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A component added to SuitMarker instances to update things slower.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class SuitMarkerUpdater : KMonoBehaviour, ISim1000ms, ISim200ms {
		/// <summary>
		/// Grid flags include "vacancy only" and "has a suit".
		/// </summary>
		private static readonly IDetouredField<SuitMarker, Grid.SuitMarker.Flags> GRID_FLAGS =
			PDetours.DetourField<SuitMarker, Grid.SuitMarker.Flags>("gridFlags");

		/// <summary>
		/// Checks the status of a suit locker to see if the suit can be used.
		/// </summary>
		/// <param name="locker">The suit dock to check.</param>
		/// <param name="fullyCharged">Will contain with the suit if fully charged.</param>
		/// <param name="partiallyCharged">Will contain the suit if partially charged.</param>
		/// <param name="any">Will contain any suit inside.</param>
		/// <returns>true if the locker is vacant, or false if it is occupied.</returns>
		internal static bool GetSuitStatus(SuitLocker locker, out KPrefabID fullyCharged,
				out KPrefabID partiallyCharged, out KPrefabID any) {
			var smi = locker.smi;
			bool vacant = false;
			float minCharge = TUNING.EQUIPMENT.SUITS.MINIMUM_USABLE_SUIT_CHARGE;
			any = locker.GetStoredOutfit();
			// CanDropOffSuit calls GetStoredOutfit again, avoid!
			if (any == null) {
				if (smi.sm.isConfigured.Get(locker.smi) && !smi.sm.isWaitingForSuit.Get(
						locker.smi))
					vacant = true;
				fullyCharged = null;
				partiallyCharged = null;
			} else {
				var tank = any.GetComponent<SuitTank>();
				if (tank.PercentFull() >= minCharge) {
					// Check for jet suit tank of petroleum
					var petroTank = any.GetComponent<JetSuitTank>();
					if (petroTank == null) {
						fullyCharged = tank.IsFull() ? any : null;
						partiallyCharged = any;
					} else {
						fullyCharged = (tank.IsFull() && petroTank.IsFull()) ? any : null;
						partiallyCharged = (petroTank.PercentFull() >= minCharge) ? any : null;
					}
				} else {
					fullyCharged = null;
					partiallyCharged = null;
				}
			}
			return vacant;
		}

		/// <summary>
		/// Processes a Duplicant walking by the checkpoint.
		/// </summary>
		/// <param name="checkpoint">The checkpoint to walk by.</param>
		/// <param name="reactor">The Duplicant that is reacting.</param>
		internal static void React(SuitMarker checkpoint, GameObject reactor) {
			var equipment = reactor.GetComponent<MinionIdentity>().GetEquipment();
			bool hasSuit = equipment.IsSlotOccupied(Db.Get().AssignableSlots.Suit);
			var navigator = reactor.GetComponent<Navigator>();
			bool changed = false;
			reactor.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(checkpoint.
				interactAnim);
			// If not wearing a suit, or the navigator can pass this checkpoint
			if (!hasSuit || (navigator != null && (navigator.flags & checkpoint.PathFlag) >
					PathFinder.PotentialPath.Flags.None)) {
				var updater = checkpoint.GetComponent<SuitMarkerUpdater>();
				foreach (var dock in updater.docks)
					if (GetSuitStatus(dock, out KPrefabID fullyCharged, out _, out _) &&
							hasSuit) {
						dock.UnequipFrom(equipment);
						changed = true;
						break;
					} else if (!hasSuit && fullyCharged != null) {
						dock.EquipTo(equipment);
						changed = true;
						break;
					}
				if (!hasSuit && !changed) {
					// Give it the best we have
					SuitLocker bestAvailable = null;
					float maxScore = 0f;
					foreach (var dock in updater.docks)
						if (dock.GetSuitScore() > maxScore) {
							bestAvailable = dock;
							maxScore = dock.GetSuitScore();
						}
					if (bestAvailable != null) {
						bestAvailable.EquipTo(equipment);
						changed = true;
					}
				}
				if (changed)
					updater.UpdateSuitStatus();
			}
			// Dump on floor, if they pass by with a suit and taking it off is impossible
			if (!changed && hasSuit) {
				var assignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
				var notification = new Notification(STRINGS.MISC.NOTIFICATIONS.SUIT_DROPPED.
					NAME, NotificationType.BadMinor, (_, data) => STRINGS.MISC.NOTIFICATIONS.
					SUIT_DROPPED.TOOLTIP);
				assignable.Unassign();
				assignable.GetComponent<Notifier>().Add(notification);
			}
		}

		/// <summary>
		/// The current location of the dock.
		/// </summary>
		private int cell;

		/// <summary>
		/// Whether there was a suit available last frame.
		/// </summary>
		private bool hadAvailableSuit;

		/// <summary>
		/// The cached list of suit docks next to the checkpoint.
		/// </summary>
		private readonly List<SuitLocker> docks;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private KAnimControllerBase anim;

		[MyCmpReq]
		private SuitMarker suitCheckpoint;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		internal SuitMarkerUpdater() {
			docks = new List<SuitLocker>();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			hadAvailableSuit = false;
			cell = Grid.PosToCell(this);
			if (suitCheckpoint != null)
				suitCheckpoint.GetAttachedLockers(docks);
			UpdateSuitStatus();
		}

		/// <summary>
		/// Updates the status of nearby suits.
		/// </summary>
		public void Sim200ms(float _) {
			UpdateSuitStatus();
		}

		/// <summary>
		/// Only update the nearby lockers every second, as they rarely change.
		/// </summary>
		public void Sim1000ms(float _) {
			if (suitCheckpoint != null) {
				docks.Clear();
				suitCheckpoint.GetAttachedLockers(docks);
			}
		}

		/// <summary>
		/// Updates the status of the suits in nearby suit docks for pathfinding and
		/// animation purposes.
		/// </summary>
		internal void UpdateSuitStatus() {
			if (suitCheckpoint != null) {
				KPrefabID availableSuit = null;
				int charged = 0, vacancies = 0;
				foreach (var dock in docks)
					if (dock != null) {
						if (GetSuitStatus(dock, out _, out KPrefabID partiallyCharged,
								out KPrefabID outfit))
							vacancies++;
						else if (partiallyCharged != null)
							charged++;
						if (availableSuit == null)
							availableSuit = outfit;
					}
				bool hasSuit = availableSuit != null;
				if (hasSuit != hadAvailableSuit) {
					anim.Play(hasSuit ? "off" : "no_suit", KAnim.PlayMode.Once, 1f, 0f);
					hadAvailableSuit = hasSuit;
				}
				Grid.UpdateSuitMarker(cell, charged, vacancies, GRID_FLAGS.Get(suitCheckpoint),
					suitCheckpoint.PathFlag);
			}
		}
	}

	/// <summary>
	/// Applied to SuitMarker to add an improved updater to each instance.
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker), "OnSpawn")]
	public static class SuitMarker_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(SuitMarker __instance) {
			var go = __instance.gameObject;
			if (go != null)
				go.AddOrGet<SuitMarkerUpdater>();
		}
	}

	/// <summary>
	/// Applied to SuitMarker.SuitMarkerReactable to make the Run method more efficient and
	/// use the SuitMarkerUpdater..
	/// </summary>
	[HarmonyPatch]
	public static class SuitMarker_SuitMarkerReactable_Run_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// SuitMarker.SuitMarkerReactable is a private class, so calculate the target with
		/// reflection.
		/// </summary>
		internal static MethodBase TargetMethod() {
			return typeof(SuitMarker).GetNestedType("SuitMarkerReactable", BindingFlags.
				Instance | PPatchTools.BASE_FLAGS)?.GetMethodSafe("Run", false);
		}

		/// <summary>
		/// Applied before Run runs.
		/// </summary>
		internal static bool Prefix(GameObject ___reactor, SuitMarker ___suitMarker) {
			if (___reactor != null && ___suitMarker != null)
				SuitMarkerUpdater.React(___suitMarker, ___reactor);
			return false;
		}
	}

	/// <summary>
	/// Applied to SuitMarker to turn off the expensive Update method. The SuitMarkerUpdater
	/// component can update the SuitMarker at more appropriate rates.
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker), "Update")]
	public static class SuitMarker_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
