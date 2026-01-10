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
using System;
using System.Collections.Generic;
using System.Text;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to BreathabilityTracker to reduce queries and speed up updating the
	/// suffocation status of Duplicants.
	/// </summary>
	[HarmonyPatch(typeof(BreathabilityTracker), nameof(BreathabilityTracker.UpdateData))]
	public static class BreathabilityTracker_UpdateData_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before UpdateData runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(BreathabilityTracker __instance) {
			var duplicants = Components.LiveMinionIdentities.GetWorldItems(__instance.WorldID);
			int n = duplicants.Count, total = 0, denom = 0;
			if (n == 0)
				__instance.AddPoint(0f);
			else {
				for (int i = 0; i < n; i++)
					if (duplicants[i].TryGetComponent(out OxygenBreather breather)) {
						var provider = breather.GetCurrentGasProvider();
						denom++;
						if (!breather.IsOutOfOxygen)
							total += provider.IsLowOxygen() ? 50 : 100;
					}
				__instance.AddPoint(denom == 0 ? 0f : Mathf.Round((float)total / denom));
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to ColonyDiagnosticScreen.DiagnosticRow to suppress SparkChart updates if
	/// not visible.
	/// </summary>
	[HarmonyPatch(typeof(ColonyDiagnosticScreen.DiagnosticRow), nameof(ColonyDiagnosticScreen.
		DiagnosticRow.Sim4000ms))]
	public static class DiagnosticRow_Sim4000ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before Sim4000ms runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(KMonoBehaviour ___sparkLayer) {
			return ___sparkLayer == null || ___sparkLayer.isActiveAndEnabled;
		}
	}

	/// <summary>
	/// Applied to MeterScreen to refresh the living Duplicant population much faster.
	/// </summary>
	[HarmonyPatch(typeof(MeterScreen), nameof(MeterScreen.RefreshMinions))]
	public static class MeterScreen_RefreshMinions_Patch {
		/// <summary>
		/// Avoid allocating many strings every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before RefreshMinions runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(MeterScreen __instance) {
			int living = Components.LiveMinionIdentities.Count;
			int identities = __instance.GetWorldMinionIdentities().Count;
			var currentMinions = __instance.currentMinions;
			var tt = __instance.MinionsTooltip;
			if (identities != __instance.cachedMinionCount && currentMinions != null && tt !=
					null) {
				string ttText;
				WorldContainer activeWorld;
				__instance.cachedMinionCount = identities;
				string alive = living.ToString();
				if (DlcManager.FeatureClusterSpaceEnabled() && (activeWorld = ClusterManager.
						Instance.activeWorld) != null && activeWorld.TryGetComponent(
						out ClusterGridEntity world)) {
					var text = CACHED_BUILDER;
					string ids = identities.ToString();
					text.Clear().Append(STRINGS.UI.TOOLTIPS.METERSCREEN_POPULATION_CLUSTER);
					ttText = text.Replace("{0}", world.Name).Replace("{1}", ids).Replace(
						"{2}", alive).ToString();
					text.Clear().Append(ids).Append('/').Append(alive);
					currentMinions.SetText(text);
				} else {
					ttText = STRINGS.UI.TOOLTIPS.METERSCREEN_POPULATION.Format(alive);
					currentMinions.SetText(alive);
				}
				tt.ClearMultiStringTooltip();
				tt.AddMultiStringTooltip(ttText, __instance.ToolTipStyle_Header);
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to MeterScreen to get rid of LINQ when calculating the Duplicants alive on
	/// the current world.
	/// </summary>
	[HarmonyPatch(typeof(MeterScreen), nameof(MeterScreen.RefreshWorldMinionIdentities))]
	public static class MeterScreen_RefreshWorldMinionIdentities_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before RefreshWorldMinionIdentities runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref IList<MinionIdentity> ___worldLiveMinionIdentities) {
			var identities = ___worldLiveMinionIdentities;
			var living = Components.LiveMinionIdentities.Items;
			int n = living.Count, worldId = ClusterManager.Instance.activeWorldId;
			if (identities == null)
				___worldLiveMinionIdentities = identities = new List<MinionIdentity>(24);
			else
				identities.Clear();
			// Avoid allocating a new list
			for (int i = 0; i < n; i++) {
				var duplicant = living[i];
				if (!duplicant.IsNullOrDestroyed() && duplicant.GetMyWorldId() == worldId)
					identities.Add(duplicant);
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to TrappedDuplicantDiagnostic to fix the only nested GetWorldItems call in the
	/// base game, required to allow pooling the return values of the call.
	/// </summary>
	public static class TrappedDuplicantPatch {
		/// <summary>
		/// The tags applied to Duplicants that are doing idle-type things (namely not work).
		/// </summary>
		private static readonly Tag[] BASICALLY_IDLE = new[] {
			GameTags.Idle, GameTags.RecoveringBreath, GameTags.MakingMess
		};

		/// <summary>
		/// Applies all Trapped diagnostic patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			harmony.Patch(typeof(TrappedDuplicantDiagnostic), nameof(TrappedDuplicantDiagnostic.
				CheckTrapped), prefix: new HarmonyMethod(typeof(TrappedDuplicantPatch),
				nameof(CheckTrapped)));
		}

		/// <summary>
		/// Checks to see if a Duplicant can reach any of the items on the list.
		/// </summary>
		/// <param name="navigator">The Duplicant's current navigator.</param>
		/// <param name="items">The items to query.</param>
		/// <returns>true if the Duplicant can reach any of the items, or false otherwise.</returns>
		private static bool CanReach<T>(Navigator navigator, IList<T> items) where T :
				Component {
			bool canReach = false;
			if (items != null) {
				int n = items.Count;
				for (int i = 0; i < n && !canReach; i++) {
					var target = items[i];
					if (target != null && (target is IApproachable ia || target.
							TryGetComponent(out ia)))
						canReach = navigator.CanReach(ia);
				}
			}
			return canReach;
		}

		/// <summary>
		/// Checks to see if a Duplicant is idle, or doing idle-type things.
		/// </summary>
		/// <param name="minion">The Duplicant to query.</param>
		/// <returns>true if they should be considered Idle for the Trapped diagnostic.</returns>
		private static bool IsBasicallyIdle(MinionIdentity minion) {
			return minion.TryGetComponent(out KPrefabID id) && id.HasAnyTags(BASICALLY_IDLE);
		}

		/// <summary>
		/// Checks to see if a Duplicant is stuck.
		/// </summary>
		/// <param name="duplicant">The Duplicant to query.</param>
		/// <param name="navigator">The Duplicant's current navigator.</param>
		/// <param name="duplicants">The other Duplicants on its world.</param>
		/// <param name="pods">The Printing Pods in this world.</param>
		/// <param name="teleporters">The teleporter exits on this world.</param>
		/// <param name="beds">The beds in this world.</param>
		/// <returns>true if the Duplicant is stuck, or false otherwise.</returns>
		private static bool IsTrapped(MinionIdentity duplicant, Navigator navigator,
				IList<MinionIdentity> duplicants, IList<Telepad> pods,
				IList<WarpReceiver> teleporters, IList<Sleepable> beds) {
			bool trapped = true;
			int n = duplicants.Count;
			// If it can reach another non-Idle Duplicant in the same world, do not mark as
			// trapped
			for (int i = 0; i < n && trapped; i++) {
				var otherDupe = duplicants[i];
				if (otherDupe != duplicant && !IsBasicallyIdle(otherDupe) && otherDupe.
						TryGetComponent(out IApproachable ia) && navigator.CanReach(ia))
					trapped = false;
			}
			trapped = trapped && !CanReach(navigator, pods);
			trapped = trapped && !CanReach(navigator, teleporters);
			if (trapped)
				foreach (var bed in beds)
					if (bed.TryGetComponent(out Assignable assignable) && assignable.
							IsAssignedTo(duplicant) && bed.TryGetComponent(
							out IApproachable ia)) {
						trapped = !navigator.CanReach(ia);
						// Only one bed can be assigned per Duplicant, so exit even if not
						// reachable
						break;
					}
			return trapped;
		}

		/// <summary>
		/// Applied before CheckTrapped runs.
		/// </summary>
		private static bool CheckTrapped(TrappedDuplicantDiagnostic __instance,
				ref ColonyDiagnostic.DiagnosticResult __result) {
			bool trapped = false;
			int worldID = __instance.worldID;
			bool isRocket = ClusterManager.Instance.GetWorld(worldID).IsModuleInterior;
			// Diagnostic does nothing on rockets
			if (!isRocket) {
				var duplicants = Components.LiveMinionIdentities.GetWorldItems(worldID);
				int n = duplicants.Count;
				var pods = Components.Telepads.GetWorldItems(worldID);
				var teleporters = Components.WarpReceivers.GetWorldItems(worldID);
				var beds = ListPool<Sleepable, TrappedDuplicantDiagnostic>.Allocate();
				beds.AddRange(Components.NormalBeds.WorldItemsEnumerate(worldID, true));
				for (int i = 0; i < n && !trapped; i++) {
					var duplicant = duplicants[i];
					// The criteria used by Trapped basically requires idle Duplicants who
					// cannot reach a friend with tasks, and cannot get to their bed or the
					// printing pod / teleporter. Ideally this would go off even if the
					// Duplicant is not idle, but this patch tries not to change the behavior.
					if (IsBasicallyIdle(duplicant) && duplicant.TryGetComponent(
							out Navigator navigator) && IsTrapped(duplicant, navigator,
							duplicants, pods, teleporters, beds)) {
						__result.clickThroughTarget = new Tuple<Vector3, GameObject>(
							duplicant.transform.position, duplicant.gameObject);
						trapped = true;
					}
				}
				beds.Recycle();
			}
			if (!trapped)
				__result.clickThroughTarget = null;
			__result.opinion = trapped ? ColonyDiagnostic.DiagnosticResult.Opinion.Bad :
				ColonyDiagnostic.DiagnosticResult.Opinion.Normal;
			__result.Message = trapped ? STRINGS.UI.COLONY_DIAGNOSTICS.
				TRAPPEDDUPLICANTDIAGNOSTIC.STUCK : STRINGS.UI.COLONY_DIAGNOSTICS.
				TRAPPEDDUPLICANTDIAGNOSTIC.NORMAL;
			return false;
		}
	}
}
