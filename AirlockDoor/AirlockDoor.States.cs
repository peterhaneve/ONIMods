/*
 * Copyright 2020 Peter Han
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

using PeterHan.PLib;
using System;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// A version of Door that never permits gas and liquids to pass unless set to open.
	/// </summary>
	public sealed partial class AirlockDoor {
		public sealed class States : GameStateMachine<States, Instance, AirlockDoor> {
			/// <summary>
			/// Sets up common parameters for door closing states.
			/// </summary>
			/// <param name="suffix">The anim name suffix for this side.</param>
			/// <param name="state">The state to configure.</param>
			/// <returns>The configured state.</returns>
			private static State ConfigureClosingState(string suffix, State state) {
				return state.ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Closes", (smi) => smi.master.doorClosingSound, (smi) => !string.IsNullOrEmpty(smi.master.doorClosingSound)).
					Update((smi, dt) => {
						if (smi.master.doorClosingSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorClosingSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("close" + suffix);
			}

			/// <summary>
			/// Sets up common parameters for door opening states.
			/// </summary>
			/// <param name="suffix">The anim name suffix for this side.</param>
			/// <param name="state">The state to configure.</param>
			/// <returns>The configured state.</returns>
			private static State ConfigureOpeningState(string suffix, State state) {
				return state.ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Opens", (smi) => smi.master.doorOpeningSound, (smi) => !string.IsNullOrEmpty(smi.master.doorOpeningSound)).
					Update((smi, dt) => {
						if (smi.master.doorOpeningSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorOpeningSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("open" + suffix);
			}

			public override void InitializeStates(out BaseState default_state) {
				serializable = true;
				default_state = notOperational;
				notOperational.PlayAnim("off").
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
					ParamTransition(isLocked, locking, IsTrue).
					Transition(closed, (smi) => smi.master.IsUsable(), UpdateRate.SIM_200ms);
				// Start opening if waiting, lock if requested
				closed.PlayAnim("idle").
					EventTransition(GameHashes.OperationalChanged, notOperational, (smi) => !smi.master.IsUsable()).
					ParamTransition(waitEnterLeft, left.enter, IsTrue).
					ParamTransition(waitEnterRight, right.enter, IsTrue).
					// If someone teleports a dupe into the airlock...
					ParamTransition(waitExitLeft, left.exit, IsTrue).
					ParamTransition(waitExitRight, right.exit, IsTrue).
					ParamTransition(isLocked, locking, IsTrue).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				// The locked state displays the "no" icon on the door
				locking.PlayAnim("locked_pre").OnAnimQueueComplete(locked).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				locked.PlayAnim("locked").
					ParamTransition(isLocked, unlocking, IsFalse);
				unlocking.PlayAnim("locked_pst").OnAnimQueueComplete(notOperational);
				left.ConfigureStates("_left", isTraversingLeft, vacuum);
				right.ConfigureStates("_right", isTraversingRight, vacuum);
				// Clear contaminants, wait for anim, and check
				vacuum.PlayAnim("vacuum", KAnim.PlayMode.Loop).
					ScheduleGoTo(3.0f, vacuum_check).
					Enter("ClearContaminants", (smi) => {
						smi.master.UpdateWorldState();
						smi.ClearContaminants();
					}).Exit("ClearContaminants", (smi) => {
						smi.ClearContaminants();
						Game.Instance.userMenu.Refresh(smi.master.gameObject);
					});
				vacuum_check.ParamTransition(waitExitLeft, left.exit, IsTrue).
					ParamTransition(waitExitRight, right.exit, IsTrue).
					Transition(closed, (smi) => !waitExitLeft.Get(smi) && !waitExitRight.Get(smi), UpdateRate.SIM_200ms);
			}

			/// <summary>
			/// Not operational / broken down / no charge. Door is considered closed.
			/// </summary>
			public State notOperational;

			/// <summary>
			/// Both sides closed in automatic mode.
			/// </summary>
			public State closed;

			/// <summary>
			/// Both sides closed and cosmetic vacuuming animation is playing.
			/// </summary>
			public State vacuum;

			/// <summary>
			/// Both sides closed and cosmetic vacuuming animation is complete. Only for one
			/// instant, transitions to the correct state.
			/// </summary>
			public State vacuum_check;

			/// <summary>
			/// Both sides closed and lock process started.
			/// </summary>
			public State locking;

			/// <summary>
			/// Both sides closed and locked to access.
			/// </summary>
			public State locked;

			/// <summary>
			/// Both sides closed and unlock process started.
			/// </summary>
			public State unlocking;

			/// <summary>
			/// States for the left door.
			/// </summary>
			public SingleSideStates left;

			/// <summary>
			/// States for the right door.
			/// </summary>
			public SingleSideStates right;

			/// <summary>
			/// True if a Duplicant is waiting to enter from the left.
			/// </summary>
			public BoolParameter waitEnterLeft;

			/// <summary>
			/// True if a Duplicant is waiting to enter from the right.
			/// </summary>
			public BoolParameter waitEnterRight;

			/// <summary>
			/// True if a Duplicant is waiting to exit to the left.
			/// </summary>
			public BoolParameter waitExitLeft;

			/// <summary>
			/// True if a Duplicant is waiting to exit to the right.
			/// </summary>
			public BoolParameter waitExitRight;

			/// <summary>
			/// True if the door is currently locked by automation or toggle.
			/// No Duplicants can pass if locked.
			/// </summary>
			public BoolParameter isLocked;

			/// <summary>
			/// True if a Duplicant is traversing the left door.
			/// </summary>
			public BoolParameter isTraversingLeft;

			/// <summary>
			/// True if a Duplicant is traversing the right door.
			/// </summary>
			public BoolParameter isTraversingRight;

			/// <summary>
			/// States with one door locked and the other door operating.
			/// </summary>
			public sealed class SingleSideStates : State {
				/// <summary>
				/// How long to wait before closing the door.
				/// </summary>
				public const float EXIT_DELAY = 0.5f;

				/// <summary>
				/// Opening pod bay door for entry.
				/// </summary>
				public State enter;

				/// <summary>
				/// Open and waiting for Duplicants to pass.
				/// </summary>
				public State waitEnter;

				/// <summary>
				/// Open and waiting to close, 0.5s with no Duplicant passing.
				/// </summary>
				public State waitEnterClose;

				/// <summary>
				/// Closed and close animation playing.
				/// </summary>
				public State closing;

				/// <summary>
				/// Opening door for exit.
				/// </summary>
				public State exit;

				/// <summary>
				/// Open and waiting for Duplicants to pass.
				/// </summary>
				public State waitExit;

				/// <summary>
				/// Open and waiting to close, 0.5s with no Duplicant passing.
				/// </summary>
				public State waitExitClose;

				/// <summary>
				/// Closed and close animation playing.
				/// </summary>
				public State clearing;

				/// <summary>
				/// Initializes all states for one side.
				/// </summary>
				/// <param name="suffix">The anim name suffix for this side.</param>
				/// <param name="isTraversing">The parameter controlling if a Duplicant is traversing this side.</param>
				/// <param name="vacuum">The state to go to when finished.</param>
				internal void ConfigureStates(string suffix, BoolParameter isTraversing, State vacuum) {
					string open = "static" + suffix;

					ConfigureOpeningState(suffix, enter).
						Enter("RemoveEnergy", (smi) => {
							smi.WithdrawEnergy();
							Game.Instance.userMenu.Refresh(smi.master.gameObject);
						}).OnAnimQueueComplete(waitEnter);
					waitEnter.PlayAnim(open).
						Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
						Enter("EnterCheckBlocked", (smi) => smi.CheckDuplicantStatus()).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						ParamTransition(isTraversing, waitEnterClose, IsFalse);
					waitEnterClose.PlayAnim(open).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						ScheduleGoTo(EXIT_DELAY, closing).
						ParamTransition(isTraversing, waitEnter, IsTrue);
					ConfigureClosingState(suffix, closing).
						Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
						ParamTransition(isTraversing, waitEnter, IsTrue).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						OnAnimQueueComplete(vacuum);
					ConfigureOpeningState(suffix, exit).
						OnAnimQueueComplete(waitExit);
					waitExit.PlayAnim(open).
						Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
						Enter("EnterCheckBlocked", (smi) => smi.CheckDuplicantStatus()).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						ParamTransition(isTraversing, waitExitClose, IsFalse);
					waitExitClose.PlayAnim(open).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						ScheduleGoTo(EXIT_DELAY, closing).
						ParamTransition(isTraversing, waitExit, IsTrue);
					ConfigureClosingState(suffix, clearing).
						Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
						ParamTransition(isTraversing, waitExit, IsTrue).
						Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
						OnAnimQueueComplete(vacuum);
				}
			}
		}

		/// <summary>
		/// The instance parameters of this state machine.
		/// </summary>
		public sealed class Instance : States.GameInstance {
			/// <summary>
			/// Checks the cell for a Duplicant.
			/// </summary>
			/// <param name="cell">The cell to check.</param>
			/// <returns>Whether a Duplicant occupies this cell.</returns>
			private static bool HasMinion(int cell) {
				return Grid.IsValidBuildingCell(cell) && Grid.Objects[cell, (int)ObjectLayer.
					Minion] != null;
			}

			public Instance(AirlockDoor door) : base(door) { }

			/// <summary>
			/// Updates the traversing parameter if a Duplicant is currently passing
			/// through the door.
			/// </summary>
			public void CheckDuplicantStatus() {
				int baseCell = master.building.GetCell();
				sm.isTraversingLeft.Set(HasMinion(Grid.CellLeft(baseCell)) || HasMinion(Grid.
					CellUpLeft(baseCell)), smi);
				sm.isTraversingRight.Set(HasMinion(Grid.CellRight(baseCell)) || HasMinion(Grid.
					CellUpRight(baseCell)), smi);
			}

			/// <summary>
			/// Ejects other pickupables and unwanted elements from the door.
			/// </summary>
			internal void ClearContaminants() {
				int baseCell = master.building.GetCell();
				int cellFarLeft = Grid.OffsetCell(baseCell, -2, 0), cellFarRight = Grid.
					OffsetCell(baseCell, 2, 0);
				bool validLeft = Grid.IsValidCell(cellFarLeft), validRight = Grid.IsValidCell(
					cellFarRight);
				if (validLeft && !validRight)
					cellFarLeft = cellFarRight;
				else if (!validLeft && validRight)
					cellFarRight = cellFarLeft;
				if (validLeft || validRight) {
					// Middle
					EjectAll(baseCell, cellFarRight);
					EjectAll(Grid.CellAbove(baseCell), cellFarRight);
					// Left
					EjectAll(Grid.CellLeft(baseCell), cellFarLeft);
					EjectAll(Grid.CellUpLeft(baseCell), cellFarLeft);
					// Right
					EjectAll(Grid.CellRight(baseCell), cellFarRight);
					EjectAll(Grid.CellUpRight(baseCell), cellFarRight);
				}
			}

			/// <summary>
			/// Ejects all dropped items in the specified cell to the specified new cell.
			/// </summary>
			/// <param name="cell">The cell to check for items.</param>
			/// <param name="newCell">The location to move the items.</param>
			private void EjectAll(int cell, int newCell) {
				if (Grid.IsValidCell(cell)) {
					var node = Grid.Objects[cell, (int)ObjectLayer.Pickupables].
						GetComponentSafe<Pickupable>()?.objectLayerListItem;
					while (node != null) {
						var item = node.gameObject;
						node = node.nextItem;
						// Ignore living entities
						if (item != null && item.GetSMI<DeathMonitor.Instance>()?.IsDead() !=
								false) {
							var position = Grid.CellToPosCCC(newCell, Grid.SceneLayer.Move);
							var collider = item.GetComponent<KCollider2D>();
							// Adjust for material's bounding box
							if (collider != null)
								position.y += item.transform.GetPosition().y - collider.
									bounds.min.y;
							item.transform.SetPosition(position);
							// Start falling if pushed off the edge
							if (GameComps.Fallers.Has(item))
								GameComps.Fallers.Remove(item);
							GameComps.Fallers.Add(item, Vector2.zero);
							item.GetComponent<Pickupable>()?.TryToOffsetIfBuried();
						}
					}
				}
			}

			/// <summary>
			/// Withdraws energy when the door opens to admit a Duplicant.
			/// </summary>
			internal void WithdrawEnergy() {
				// Door was closed and has started to open, withdraw energy
				master.energyAvailable = Math.Max(0.0f, master.energyAvailable - master.
					EnergyPerUse);
				master.UpdateMeter();
			}
		}
	}
}
