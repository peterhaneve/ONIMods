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
using STRINGS;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A component added to SuitMarker instances to update things slower.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class SuitMarkerUpdater : KMonoBehaviour, ISim1000ms, ISim200ms {
		/// <summary>
		/// Drops the currently worn suit on the floor and emits a notification that a suit
		/// was dropped due to lack of space.
		/// </summary>
		/// <param name="equipment">The assignables containing the suit to drop.</param>
		private static void DropSuit(Assignables equipment) {
			var assignable = equipment.GetAssignable(Db.Get().AssignableSlots.Suit);
			var notification = new Notification(STRINGS.MISC.NOTIFICATIONS.SUIT_DROPPED.NAME,
				NotificationType.BadMinor, (_, data) => STRINGS. MISC.NOTIFICATIONS.
				SUIT_DROPPED.TOOLTIP);
			assignable.Unassign();
			if (assignable.TryGetComponent(out Notifier notifier))
				notifier.Add(notification);
		}
		
		/// <summary>
		/// Processes a Duplicant putting on a suit.
		/// </summary>
		/// <param name="checkpoint">The checkpoint to walk by.</param>
		/// <param name="reactor">The Duplicant that is reacting.</param>
		/// <returns>true if the reaction was processed, or false otherwise.</returns>
		internal static bool EquipReact(SuitMarker checkpoint, GameObject reactor) {
			bool react = false;
			if (reactor.TryGetComponent(out MinionIdentity id) && checkpoint.TryGetComponent(
					out SuitMarkerUpdater updater)) {
				var lockers = updater.docks;
				int n = lockers.Count;
				float bestScore = -float.MaxValue;
				SuitLocker target = null;
				for (int i = 0; i < n && bestScore < 1.0f; i++) {
					var locker = lockers[i];
					float score = TryGetStoredOutfit(locker, out var suit) ?
						GetSuitScore(suit) : -1.0f;
					if (score >= 0.0f && (target == null || score > bestScore)) {
						target = locker;
						bestScore = score;
					}
				}
				if (target != null) {
					target.EquipTo(id.GetEquipment());
					updater.UpdateSuitStatus();
				}
				react = true;
			}
			return react;
		}

		/// <summary>
		/// A much faster version of SuitLocker.GetSuitScore to evaluate whether a suit in a
		/// dock can be used.
		/// </summary>
		/// <param name="outfit">The suit to check.</param>
		/// <returns>The score of the suit, which must be 0.0f or more to be used.</returns>
		private static float GetSuitScore(KPrefabID outfit) {
			float score = -1.0f, charge, min = TUNING.EQUIPMENT.SUITS.
				MINIMUM_USABLE_SUIT_CHARGE;
			if (outfit.TryGetComponent(out SuitTank tank) && (charge = tank.PercentFull()) >=
					min) {
				if (outfit.TryGetComponent(out JetSuitTank jetTank)) {
					float jetCharge = jetTank.PercentFull();
					if (jetCharge >= min)
						score = Mathf.Min(charge, jetCharge);
				} else
					score = charge;
			}
			return score;
		}

		/// <summary>
		/// Looks for the stored suit in a suit dock.
		/// </summary>
		/// <param name="locker">The suit dock to search.</param>
		/// <param name="suit">The location where the suit will be stored.</param>
		/// <returns>true if a suit was found, or false otherwise.</returns>
		private static bool TryGetStoredOutfit(SuitLocker locker, out KPrefabID suit) {
			var tags = locker.OutfitTags;
			bool found = false;
			KPrefabID result = null;
			if (locker.TryGetComponent(out Storage storage)) {
				var items = storage.items;
				int n = items.Count;
				for (int i = 0; i < n && !found; i++) {
					var go = items[i];
					if (go != null && go.TryGetComponent(out KPrefabID id) && id.
							IsAnyPrefabID(tags)) {
						result = id;
						found = true;
					}
				}
			}
			suit = result;
			return found;
		}
		
		/// <summary>
		/// Processes a Duplicant taking off a suit.
		/// </summary>
		/// <param name="checkpoint">The checkpoint to walk by.</param>
		/// <param name="reactor">The Duplicant that is reacting.</param>
		/// <returns>true if the reaction was processed, or false otherwise.</returns>
		internal static bool UnequipReact(SuitMarker checkpoint, GameObject reactor) {
			bool react = false;
			if (reactor.TryGetComponent(out MinionIdentity id) && reactor.TryGetComponent(
					out Navigator nav) && checkpoint.TryGetComponent(out SuitMarkerUpdater
					updater)) {
				var equipment = id.GetEquipment();
				if ((nav.flags & checkpoint.PathFlag) > PathFinder.PotentialPath.Flags.None) {
					var lockers = updater.docks;
					int n = lockers.Count;
					SuitLocker target = null;
					for (int i = 0; i < n; i++) {
						var locker = lockers[i];
						if (locker.CanDropOffSuit()) {
							target = locker;
							break;
						}
					}
					react = target != null;
					if (react) {
						target.UnequipFrom(equipment);
						updater.UpdateSuitStatus();
					}
				}
				if (!react)
					DropSuit(equipment);
				react = true;
			}
			return react;
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

		/// <summary>
		/// Triggered when the checkpoint becomes operational or non-operational.
		/// </summary>
		private void OnOperationalChanged(object _) {
			UpdateSuitStatus();
		}

		public override void OnCleanUp() {
			Unsubscribe((int)GameHashes.OperationalChanged, OnOperationalChanged);
			base.OnCleanUp();
		}

		public override void OnSpawn() {
			base.OnSpawn();
			hadAvailableSuit = false;
			cell = Grid.PosToCell(transform.position);
			if (suitCheckpoint != null)
				suitCheckpoint.GetAttachedLockers(docks);
			Subscribe((int)GameHashes.OperationalChanged, OnOperationalChanged);
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
				int charged = 0, vacancies = 0, n = docks.Count;
				for (int i = 0; i < n; i++) {
					var dock = docks[i];
					if (dock != null) {
						var smi = dock.smi;
						if (TryGetStoredOutfit(dock, out var outfit)) {
							if (GetSuitScore(outfit) >= 0.0f) {
								charged++;
								if (availableSuit == null)
									availableSuit = outfit;
							}
						} else if (smi.sm.isConfigured.Get(smi) && !smi.sm.isWaitingForSuit.
								Get(smi))
							vacancies++;
					}
				}
				bool hasSuit = availableSuit != null;
				if (hasSuit != hadAvailableSuit) {
					anim.Play(hasSuit ? "off" : "no_suit");
					hadAvailableSuit = hasSuit;
				}
				Grid.UpdateSuitMarker(cell, charged, vacancies, suitCheckpoint.gridFlags,
					suitCheckpoint.PathFlag);
			}
		}
	}
	
	/// <summary>
	/// Applied to SuitMarker.EquipSuitReactable to make the Run method more efficient and
	/// use the SuitMarkerUpdater..
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker.EquipSuitReactable), nameof(SuitMarker.
		EquipSuitReactable.Run))]
	public static class SuitMarker_EquipSuitReactable_Run_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before Run runs.
		/// </summary>
		internal static bool Prefix(GameObject ___reactor, SuitMarker ___suitMarker) {
			return ___reactor != null && ___suitMarker != null && !SuitMarkerUpdater.
				EquipReact(___suitMarker, ___reactor);
		}
	}

	/// <summary>
	/// Applied to SuitMarker to add an improved updater to each instance.
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker), nameof(SuitMarker.OnSpawn))]
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
	/// Applied to SuitMarker to turn off the expensive Update method. The SuitMarkerUpdater
	/// component can update the SuitMarker at more appropriate rates.
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker), nameof(SuitMarker.Update))]
	public static class SuitMarker_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
	
	/// <summary>
	/// Applied to SuitMarker.UnequipSuitReactable to make the Run method more efficient and
	/// use the SuitMarkerUpdater..
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker.UnequipSuitReactable), nameof(SuitMarker.
		UnequipSuitReactable.Run))]
	public static class SuitMarker_UnequipSuitReactable_Run_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before Run runs.
		/// </summary>
		internal static bool Prefix(GameObject ___reactor, SuitMarker ___suitMarker) {
			return ___reactor != null && ___suitMarker != null && !SuitMarkerUpdater.
				UnequipReact(___suitMarker, ___reactor);
		}
	}
}
