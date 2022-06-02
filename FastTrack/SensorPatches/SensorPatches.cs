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
using Klei.AI;
using UnityEngine;

namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// Patches applied to optimize Duplicant Sensors which are quite slow and run every frame.
	/// ToiletSensor and AssignableReachabilitySensor are very cheap so no big deal
	/// IdleCellSensor already only runs if the Duplicant has the idle tag
	/// Anything using PathFinder.RunQuery (idle cell, safe cell) has to be foreground as
	///   those use shared/static path grid state
	/// </summary>
	public static class SensorPatches {
		/// <summary>
		/// Compatibility for Rest for the Weary, allow mingle cell sensor during
		/// Finish Tasks time.
		/// </summary>
		private static ScheduleBlockType finishTasks;

		/// <summary>
		/// Stores the schedule block type used for recreation chores.
		/// </summary>
		private static ScheduleBlockType recreation;

		/// <summary>
		/// Initializes after the Db is loaded.
		/// </summary>
		internal static void Init() {
			finishTasks = Db.Get().ScheduleBlockTypes.TryGet("FinishTask");
			recreation = Db.Get().ScheduleBlockTypes.Recreation;
		}

		/// <summary>
		/// Removes the BalloonStandCellSensor if the Duplicant is not a balloon artist.
		/// </summary>
		/// <param name="go">The Duplicant to check.</param>
		internal static void RemoveBalloonArtistSensor(GameObject go) {
			if (go.TryGetComponent(out Traits traits) && !traits.HasTrait("BalloonArtist") &&
					go.TryGetComponent(out Sensors sensors))
				// Destroy the sensor if not a balloon artist
				sensors.sensors.RemoveAll((sensor) => sensor is BalloonStandCellSensor);
		}

		/// <summary>
		/// Applied to BalloonStandCellSensor to only look for a cell during recreation time.
		/// </summary>
		[HarmonyPatch(typeof(BalloonStandCellSensor), nameof(BalloonStandCellSensor.Update))]
		internal static class BalloonStandCellSensor_Update {
			internal static bool Prepare() => FastTrackOptions.Instance.SensorOpts;

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static bool Prefix(MinionBrain ___brain) {
				// A slim bit slow, but only run 1-2 times a frame, and way faster than what
				// the sensor does by default
				return ___brain != null && recreation != null && (!___brain.TryGetComponent(
					out Schedulable schedulable) || ScheduleManager.Instance.IsAllowed(
					schedulable, recreation));
			}
		}

		/// <summary>
		/// Applied to MingleCellSensor to only look for a mingle cell during recreation time.
		/// </summary>
		[HarmonyPatch(typeof(MingleCellSensor), nameof(MingleCellSensor.Update))]
		internal static class MingleCellSensor_Update {
			internal static bool Prepare() => FastTrackOptions.Instance.SensorOpts;

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static bool Prefix(MinionBrain ___brain) {
				var inst = ScheduleManager.Instance;
				bool run = ___brain != null;
				if (run && ___brain.TryGetComponent(out Schedulable schedulable) && inst !=
						null && recreation != null) {
					var block = schedulable.GetSchedule().GetBlock(Schedule.GetBlockIdx());
					run = block.IsAllowed(recreation) || (finishTasks != null && block.
						IsAllowed(finishTasks));
				}
				return run;
			}
		}
	}

	/// <summary>
	/// Applied to PathProberSensor to shut it off unconditionally if pickup opts have been
	/// moved to a background task.
	/// </summary>
	[HarmonyPatch(typeof(PathProberSensor), nameof(PathProberSensor.Update))]
	public static class PathProberSensor_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to PickupableSensor to shut it off unconditionally if pickup opts have been
	/// moved to a background task.
	/// </summary>
	[HarmonyPatch(typeof(PickupableSensor), nameof(PickupableSensor.Update))]
	public static class PickupableSensor_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to SafeCellQuery to dramatically speed it up by cancelling the query once
	/// a target cell has been found.
	/// </summary>
	[HarmonyPatch(typeof(SafeCellQuery), nameof(SafeCellQuery.IsMatch))]
	public static class SafeCellQuery_IsMatch_Patch {
		/// <summary>
		/// Applied after IsMatch runs.
		/// </summary>
		internal static void Postfix(ref bool __result, int cost, int ___targetCost) {
			if (cost >= ___targetCost)
				__result = true;
		}
	}
}
