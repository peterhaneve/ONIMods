/*
 * Copyright 2021 Peter Han
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

namespace PeterHan.MooReproduction {
	/// <summary>
	/// A version of GrowUpStates that does not play the grow up animation for moos.
	/// </summary>
	public class MooGrowUpStates : GameStateMachine<MooGrowUpStates, MooGrowUpStates.Instance,
			IStateMachineTarget, MooGrowUpStates.Def> {
		private static void SpawnAdult(Instance smi) {
			// Eventually, if we get a growing up animation, then this class will go away so
			// it is better to reuse SpawnAdult with the patch and not reimplement
			smi.GetSMI<BabyMonitor.Instance>().SpawnAdult();
		}

		public override void InitializeStates(out BaseState default_state) {
			default_state = spawn_adult;
			root.ToggleStatusItem(STRINGS.CREATURES.STATUSITEMS.GROWINGUP.NAME, STRINGS.
				CREATURES.STATUSITEMS.GROWINGUP.TOOLTIP, "", StatusItem.IconType.Info,
				NotificationType.Neutral, false, default, 129022, null, null,
				Db.Get().StatusItemCategories.Main);
			spawn_adult.Enter(SpawnAdult);
		}

		/// <summary>
		/// Not used, until moos get a growing up animation.
		/// </summary>
		public State grow_up_pre;

		/// <summary>
		/// Adult is being spawned.
		/// </summary>
		public State spawn_adult;

		public class Def : BaseDef { }

		public new class Instance : GameInstance {
			public Instance(Chore<Instance> chore, Def def) : base(chore, def) {
				chore.AddPrecondition(ChorePreconditions.instance.CheckBehaviourPrecondition,
					GameTags.Creatures.Behaviours.GrowUpBehaviour);
			}
		}
	}
}
