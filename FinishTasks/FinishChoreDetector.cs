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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;

using PriorityClass = PriorityScreen.PriorityClass;

namespace PeterHan.FinishTasks {
	/// <summary>
	/// A per-Duplicant component that detects when schedule blocks change and remembers the
	/// chore that the Duplicant is allowed to finish in case it gets interrupts by an emote.
	/// </summary>
	public sealed class FinishChoreDetector : KMonoBehaviour {
		/// <summary>
		/// Retrieves the ID of the current schedule block.
		///
		/// <param name="schedule">The active schedule.</param>
		/// </summary>
		public static string GetScheduleBlock(Schedule schedule) {
			string block = "";
			// ChoreConsumerState.scheduleBlock is only updated on check for new chore
			if (schedule != null)
				block = schedule.GetCurrentScheduleBlock()?.GroupId ?? "";
			return block;
		}

		/// <summary>
		/// Reports whether the Duplicant is still looking for a task to finish.
		/// </summary>
		public bool IsAcquiringChore => acquireChore;

		/// <summary>
		/// Reports the task that the Duplicant may finish. If null and IsAcquiringChore is
		/// false, no new work task may be started. If null and IsAcquiringChore is true, any
		/// work task may be started.
		/// </summary>
		public Chore TaskToFinish => acquireChore ? null : allowedChore;

		/// <summary>
		/// Whether the behavior is still looking for a valid chore to "finish".
		/// </summary>
		private bool acquireChore;

		/// <summary>
		/// The current chore driver. Cannot be populated with [MyCmpGet] because that force
		/// spawns the component if it exists, which crashes on dead Dupes.
		/// </summary>
		private ChoreDriver driver;

		/// <summary>
		/// The chore that the Duplicant may perform during this Finish Tasks block.
		/// </summary>
		private Chore allowedChore;

		/// <summary>
		/// The schedule block ID when the changed handler was last executed.
		/// </summary>
		private string lastGroupID;

		/// <summary>
		/// If the Duplicant has not yet acquired a task, checks for a task that they can
		/// complete during this Finish Tasks block.
		/// </summary>
		private void CheckAcquireChore() {
			if (acquireChore && driver != null) {
				var currentChore = driver.GetCurrentChore();
				PriorityClass pc;
				// Allow acquiring the current chore if it is above idle and below urgent
				if (currentChore != null && (pc = currentChore.masterPriority.priority_class) >
						PriorityClass.idle && pc < PriorityClass.personalNeeds) {
#if DEBUG
					PUtil.LogDebug("{0} may finish {1}".F(gameObject.name, currentChore.
						GetType().FullName));
#endif
					acquireChore = false;
					allowedChore = currentChore;
				}
			}
		}

		public override void OnCleanUp() {
			Unsubscribe((int)GameHashes.ScheduleChanged, OnScheduleChanged);
			Unsubscribe((int)GameHashes.ScheduleBlocksChanged, OnScheduleChanged);
			base.OnCleanUp();
		}

		/// <summary>
		/// Fired when a schedule block boundary is reached, or the overall schedule changes.
		/// 
		/// <param name="parameter">The new schedule.</param>
		/// </summary>
		private void OnScheduleChanged(object parameter) {
			if (driver != null && parameter is Schedule schedule) {
				string groupID = GetScheduleBlock(schedule), ft = FinishTasksPatches.
					FinishTask.Id;
#if DEBUG
				PUtil.LogDebug("{0}: Schedule change from {1} to {2}".F(gameObject.name,
					lastGroupID, groupID));
#endif
				if (groupID == ft && lastGroupID != null && lastGroupID != ft) {
					// Entered Finish Tasks from a non-finish tasks block
					acquireChore = true;
					CheckAcquireChore();
				} else if (groupID != ft) {
					// Allow GC of the chore
					allowedChore = null;
					acquireChore = false;
				}
				lastGroupID = groupID;
			}
		}

		public override void OnSpawn() {
			base.OnSpawn();
			TryGetComponent(out driver);
			Subscribe((int)GameHashes.ScheduleBlocksChanged, OnScheduleChanged);
			Subscribe((int)GameHashes.ScheduleChanged, OnScheduleChanged);
			lastGroupID = null;
			acquireChore = lastGroupID == FinishTasksPatches.FinishTask.Id;
			allowedChore = null;
		}
		
		public void Update() {
			CheckAcquireChore();
		}
	}
}
