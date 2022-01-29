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

using UnityEngine;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// Chore states for a critter giving live birth.
	/// </summary>
	public class GiveBirthStates : GameStateMachine<GiveBirthStates, GiveBirthStates.Instance,
			IStateMachineTarget, GiveBirthStates.Def> {
		/// <summary>
		/// Turns the adult critter to face its baby.
		/// </summary>
		/// <param name="smi">The state machine of the adult.</param>
		private static void FaceBaby(Instance smi) {
			smi.Get<Facing>().Face(smi.babyPos);
		}

		/// <summary>
		/// Gets the cell where the adult will move after giving birth.
		/// </summary>
		/// <param name="smi">The state machine of the adult.</param>
		/// <returns>The cell where the adult should move after birth.</returns>
		private static int GetMoveAsideCell(Instance smi) {
			int x_offset = Klei.GenericGameSettings.instance.acceleratedLifecycle ? 8 : 1;
			int cell = Grid.PosToCell(smi);
			if (Grid.IsValidCell(cell)) {
				int cell_right = Grid.OffsetCell(cell, x_offset, 0);
				if (Grid.IsValidCell(cell_right) && !Grid.Solid[cell_right])
					return cell_right;
				int cell_left = Grid.OffsetCell(cell, -x_offset, 0);
				if (Grid.IsValidCell(cell_left) && !Grid.Solid[cell_left])
					return cell_left;
			}
			return Grid.InvalidCell;
		}

		/// <summary>
		/// Spawns the baby critter at the current adult location.
		/// </summary>
		/// <param name="smi">The state machine spawning the baby.</param>
		private static void SpawnBaby(Instance smi) {
			smi.babyPos = smi.transform.GetPosition();
			smi.GetSMI<LiveFertilityMonitor.Instance>()?.GiveBirth();
		}

		/// <summary>
		/// End state when the chore is complete.
		/// </summary>
		public State behaviourcomplete;

		/// <summary>
		/// Initial state which lays the egg
		/// </summary>
		public State birthpre;

		/// <summary>
		/// Moves aside and turns to face the new child!
		/// </summary>
		public State lookatbaby;

		public override void InitializeStates(out BaseState default_state) {
			default_state = birthpre;
			root.ToggleStatusItem(MooReproductionStrings.CREATURES.STATUSITEMS.GIVINGBIRTH.
				NAME, MooReproductionStrings.CREATURES.STATUSITEMS.GIVINGBIRTH.TOOLTIP, "",
				StatusItem.IconType.Info, NotificationType.Neutral, false, default, 129022,
				null, null, Db.Get().StatusItemCategories.Main);
			birthpre.Enter(SpawnBaby).
				MoveTo(GetMoveAsideCell, lookatbaby, behaviourcomplete, false);
			lookatbaby.Enter(FaceBaby).
				GoTo(behaviourcomplete);
			behaviourcomplete.QueueAnim("idle_loop", true, null).
				BehaviourComplete(GameTags.Creatures.Fertile, false);
		}

		public class Def : BaseDef { }

		// Token: 0x02000C63 RID: 3171
		public new class Instance : GameInstance {
			public Vector3 babyPos;

			public Instance(Chore<Instance> chore, Def def) : base(chore, def) {
				chore.AddPrecondition(ChorePreconditions.instance.CheckBehaviourPrecondition,
					GameTags.Creatures.Fertile);
			}
		}
	}
}
