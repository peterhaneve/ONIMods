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

using UnityEngine;

using MingleState = GameStateMachine<PeterHan.FinishTasks.FinishMingleChore.States,
	PeterHan.FinishTasks.FinishMingleChore.StatesInstance,
	PeterHan.FinishTasks.FinishMingleChore, object>.State;

namespace PeterHan.FinishTasks {
	/// <summary>
	/// A variation of MingleChore that can run during Finish Tasks schedule blocks. Dupes
	/// with no tasks near the mingle location to do, will mingle there instead.
	/// </summary>
	public sealed class FinishMingleChore : Chore<FinishMingleChore.StatesInstance>,
			IWorkerPrioritizable {
		/// <summary>
		/// A precondition that checks for a valid mingling cell.
		/// </summary>
		private static readonly Precondition HAS_MINGLE_CELL = new Precondition {
			id = "PeterHan.FinishTasks.HasMingleCell",
			description = STRINGS.DUPLICANTS.CHORES.PRECONDITIONS.HAS_MINGLE_CELL,
			fn = HasMingleCell
		};

		/// <summary>
		/// Checks for a destination cell to mingle. The printing pod is the backup if no
		/// rec rooms are found.
		/// </summary>
		/// <returns>true if a mingle cell is available, or false otherwise.</returns>
		private static bool HasMingleCell(ref Precondition.Context context, object data) {
			bool hasCell = false;
			if (data is FinishMingleChore mc) {
				int target = mc.smi.GetMingleCell();
				var user = context.consumerState?.navigator;
				hasCell = Grid.IsValidCell(target) && user != null && user.GetNavigationCost(
					target) >= 0;
			}
			return hasCell;
		}

		// Idle Priority 9 puts it above Idle task but below the task the Dupe is finishing
		public FinishMingleChore(IStateMachineTarget target) : base(Db.Get().ChoreTypes.Relax,
				target, target.GetComponent<ChoreProvider>(), false, null, null, null,
				PriorityScreen.PriorityClass.idle, 9, false, true, 0, false,
				ReportManager.ReportType.PersonalTime) {
			showAvailabilityInHoverText = false;
			smi = new StatesInstance(this, target.gameObject);
			AddPrecondition(HAS_MINGLE_CELL, this);
			AddPrecondition(ChorePreconditions.instance.IsNotRedAlert);
			AddPrecondition(ChorePreconditions.instance.IsScheduledTime, FinishTasksPatches.
				FinishBlock);
			AddPrecondition(ChorePreconditions.instance.CanDoWorkerPrioritizable, this);
		}

		protected override StatusItem GetStatusItem() {
			return Db.Get().DuplicantStatusItems.Mingling;
		}

		public bool GetWorkerPriority(Worker worker, out int priority) {
			// Same value as MingleChore
			priority = TUNING.RELAXATION.PRIORITY.TIER1;
			return true;
		}

		/// <summary>
		/// A variation of MingleChore.States, that can run during finish time.
		/// </summary>
		public sealed class States : GameStateMachine<States, StatesInstance, FinishMingleChore> {
			/// <summary>
			/// The Duplicant which is returning to the mingle cell.
			/// </summary>
			public TargetParameter mingler;

			/// <summary>
			/// State where the Duplicant converses with nearby Duplicants.
			/// </summary>
			public MingleState mingle;

			/// <summary>
			/// State where the Duplicant returns home to base.
			/// </summary>
			public MingleState move;

			public override void InitializeStates(out BaseState default_state) {
				default_state = move;
				Target(mingler);
				root.EventTransition(GameHashes.ScheduleBlocksChanged, null, smi => !smi.IsFinishTasksTime()).
					Transition(null, smi => !Grid.IsValidCell(smi.GetMingleCell()));
				move.MoveTo(smi => smi.GetMingleCell(), mingle);
				// Will be cancelled when schedule blocks change or the mingle cell is invalid
				mingle.ToggleAnims("anim_generic_convo_kanim").
					ToggleTag(GameTags.AlwaysConverse).
					PlayAnim("idle", KAnim.PlayMode.Loop);
			}
		}

		/// <summary>
		/// A variation of MingleChore.StatesInstance to track the destination for each
		/// Duplicant after the day ends.
		/// </summary>
		public class StatesInstance : GameStateMachine<States, StatesInstance,
				FinishMingleChore, object>.GameInstance {
			/// <summary>
			/// The sensor which detects a location to mingle.
			/// </summary>
			private readonly MingleCellSensor mingleCellSensor;

			/// <summary>
			/// A cached reference to the Duplicant's scheduler.
			/// </summary>
			private readonly Schedulable schedule;

			public StatesInstance(FinishMingleChore master, GameObject mingler) : base(master)
			{
				schedule = master.GetComponent<Schedulable>();
				sm.mingler.Set(mingler, smi);
				mingleCellSensor = GetComponent<Sensors>().GetSensor<MingleCellSensor>();
			}

			/// <summary>
			/// Gets the destination cell for the Duplicant to mingle.
			/// </summary>
			/// <returns>The destination where the Duplicant will go when the busy day is done.</returns>
			public int GetMingleCell() {
				int cell = mingleCellSensor.GetCell();
				if (!Grid.IsValidCell(cell)) {
					var mingler = sm.mingler.Get(smi);
					GameObject telepad;
					if (mingler != null && (telepad = GameUtil.GetTelepad(mingler.
							GetMyWorldId())) != null)
						cell = Grid.PosToCell(telepad);
					else
						cell = Grid.InvalidCell;
				}
				return cell;
			}

			/// <summary>
			/// Returns true if this chore is still allowed.
			/// </summary>
			/// <returns>true if the Duplicant is during finish time, or false otherwise.</returns>
			public bool IsFinishTasksTime() {
				return schedule.IsAllowed(FinishTasksPatches.FinishBlock);
			}
		}
	}
}
