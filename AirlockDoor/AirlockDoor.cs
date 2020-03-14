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

using Harmony;
using KSerialization;
using PeterHan.PLib;
using System;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// A version of Door that never permits gas and liquids to pass unless set to open.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class AirlockDoor : StateMachineComponent<AirlockDoor.Instance>, ISaveLoadable, ISim200ms {
		/// <summary>
		/// The status item showing the door's current state.
		/// </summary>
		private static StatusItem doorControlState;

		/// <summary>
		/// The status item showing the door's stored charge in kJ.
		/// </summary>
		private static StatusItem storedCharge;

		/// <summary>
		/// Prevents initialization from multiple threads at once.
		/// </summary>
		private static readonly object INIT_LOCK = new object();

		/// <summary>
		/// The port ID of the automation input port.
		/// </summary>
		internal static readonly HashedString OPEN_CLOSE_PORT_ID = "DoorOpenClose";

		/// <summary>
		/// The parameter for the sound indicating whether the door has power.
		/// </summary>
		private static readonly HashedString SOUND_POWERED_PARAMETER = "doorPowered";

		/// <summary>
		/// The parameter for the sound indicating the progress of the door close.
		/// </summary>
		private static readonly HashedString SOUND_PROGRESS_PARAMETER = "doorProgress";

		/// <summary>
		/// When the door is first instantiated, initializes the static fields, to avoid the
		/// crash that the stock Door has if it is loaded too early.
		/// </summary>
		private static void StaticInit() {
			doorControlState = new StatusItem("CurrentDoorControlState", "BUILDING", "",
				StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.
				None.ID) {
				resolveStringCallback = (str, data) => {
					bool locked = (data as AirlockDoor)?.locked ?? true;
					return str.Replace("{ControlState}", Strings.Get(
						"STRINGS.BUILDING.STATUSITEMS.CURRENTDOORCONTROLSTATE." + (locked ?
						"LOCKED" : "AUTO")));
				}
			};
			storedCharge = new StatusItem("AirlockStoredCharge", "BUILDING", "", StatusItem.
				IconType.Info, NotificationType.Neutral, false, OverlayModes.None.ID) {
				resolveStringCallback = (str, data) => {
					if (data is AirlockDoor door)
						str = string.Format(str, GameUtil.GetFormattedRoundedJoules(door.
							EnergyAvailable), GameUtil.GetFormattedRoundedJoules(door.
							EnergyCapacity), GameUtil.GetFormattedRoundedJoules(door.
							EnergyPerUse));
					return str;
				}
			};
		}

		/// <summary>
		/// The energy available to use for transiting Duplicants.
		/// </summary>
		public float EnergyAvailable {
			get {
				return energyAvailable;
			}
		}

		/// <summary>
		/// The maximum energy capacity.
		/// </summary>
		[SerializeField]
		public float EnergyCapacity;

		/// <summary>
		/// The energy consumed per use.
		/// </summary>
		[SerializeField]
		public float EnergyPerUse;

		/// <summary>
		/// Returns true if the airlock door is currently open (set open, or open in auto mode).
		/// </summary>
		/// <returns>Whether the door is open.</returns>
		public bool IsOpen {
			get {
				return smi.IsInsideState(smi.sm.closeDelay) || smi.IsInsideState(smi.sm.open);
			}
		}

		/// <summary>
		/// The queued chore if a toggle errand is pending to open/close the door.
		/// </summary>
		private Chore changeStateChore;

		/// <summary>
		/// The sound played while the door is closing.
		/// </summary>
		private string doorClosingSound;

		/// <summary>
		/// The sound played while the door is opening.
		/// </summary>
		private string doorOpeningSound;

		[Serialize]
		private float energyAvailable;

		/// <summary>
		/// The current door state.
		/// </summary>
		[Serialize]
		[SerializeField]
		private bool locked;

		/// <summary>
		/// A reference counter of how many times Open()/Close() have been called.
		/// </summary>
		private int openCount;

		/// <summary>
		/// The door state requested by automation.
		/// </summary>
		private bool requestedState;

		// These fields are populated automatically by KMonoBehaviour
#pragma warning disable IDE0044
#pragma warning disable CS0649
		[MyCmpReq]
		public Building building;

		[MyCmpGet]
		private EnergyConsumer consumer;

		[MyCmpAdd]
		private LoopingSounds loopingSounds;

		[MyCmpReq]
		private Operational operational;

		[MyCmpReq]
		private PrimaryElement pe;

		[MyCmpGet]
		private KSelectable selectable;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		internal AirlockDoor() {
			locked = requestedState = false;
			energyAvailable = 0.0f;
			EnergyCapacity = 1000.0f;
			EnergyPerUse = 0.0f;
		}

		/// <summary>
		/// Closes the door.
		/// </summary>
		public void Close() {
			if (locked)
				smi.sm.isOpen.Set(false, smi);
			else {
				openCount = Math.Max(0, openCount - 1);
				if (openCount == 0) {
					smi.sm.isOpen.Set(false, smi);
					Game.Instance.userMenu.Refresh(gameObject);
				}
			}
		}

		/// <summary>
		/// Whether the door has enough energy for one use.
		/// </summary>
		/// <returns>true if there is energy for a Duplicant to pass, or false otherwise.</returns>
		public bool HasEnergy() {
			return EnergyAvailable >= EnergyPerUse;
		}

		/// <summary>
		/// Whether a Duplicant can traverse the door.
		/// </summary>
		/// <returns>true if the door is passable, or false otherwise.</returns>
		public bool IsUsable() {
			return operational.IsOperational && HasEnergy();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			lock (INIT_LOCK) {
				if (doorControlState == null)
					StaticInit();
			}
			// Adding new sounds is actually very challenging
			doorClosingSound = GlobalAssets.GetSound("MechanizedAirlock_closing");
			doorOpeningSound = GlobalAssets.GetSound("MechanizedAirlock_opening");
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			var structureTemperatures = GameComps.StructureTemperatures;
			var handle = structureTemperatures.GetHandle(gameObject);
			structureTemperatures.Bypass(handle);
			openCount = 0;
			Subscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);
			requestedState = locked;
			smi.StartSM();
			RefreshControlState();
			var access = GetComponent<AccessControl>() != null;
			float massPerCell = pe.Mass / building.PlacementCells.Length;
			foreach (int cell in building.PlacementCells) {
				Grid.CritterImpassable[cell] = true;
				Grid.HasDoor[cell] = true;
				Grid.HasAccessDoor[cell] = access;
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
			// Door is always powered when used
			if (doorClosingSound != null)
				loopingSounds.UpdateFirstParameter(doorClosingSound, SOUND_POWERED_PARAMETER, 1f);
			if (doorOpeningSound != null)
				loopingSounds.UpdateFirstParameter(doorOpeningSound, SOUND_POWERED_PARAMETER, 1f);
			selectable.SetStatusItem(Db.Get().StatusItemCategories.OperatingEnergy,
				storedCharge, this);
		}

		protected override void OnCleanUp() {
			foreach (int cell in building.PlacementCells) {
				// Clear the airlock flags, render critter and duplicant passable
				Grid.HasDoor[cell] = false;
				Grid.HasAccessDoor[cell] = false;
				Game.Instance.SetDupePassableSolid(cell, false, Grid.Solid[cell]);
				Grid.CritterImpassable[cell] = false;
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
			Unsubscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);
			base.OnCleanUp();
		}

		private void OnLogicValueChanged(object data) {
			var logicValueChanged = (LogicValueChanged)data;
			if (logicValueChanged.portID == OPEN_CLOSE_PORT_ID) {
				int newValue = logicValueChanged.newValue;
				if (changeStateChore != null) {
					changeStateChore.Cancel("Automation state change");
					changeStateChore = null;
				}
				// Bit 0 green: automatic, bit 0 red: lock the door
				requestedState = !LogicCircuitNetwork.IsBitActive(0, newValue);
			}
		}

		/// <summary>
		/// Opens the pod bay doors!
		/// </summary>
		public void Open() {
			if (!locked) {
				smi.sm.isOpen.Set(true, smi);
				openCount++;
			}
		}

		/// <summary>
		/// Updates the locked/open/auto state in the UI and the state machine.
		/// </summary>
		private void RefreshControlState() {
			smi.sm.isLocked.Set(locked, smi);
			Trigger((int)GameHashes.DoorControlStateChanged, locked ? Door.ControlState.
				Locked : Door.ControlState.Auto);
			UpdateWorldState();
			selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, doorControlState,
				this);
		}

		/// <summary>
		/// Updates the state of the door's cells in the game.
		/// </summary>
		private void UpdateWorldState() {
			bool open = IsOpen, usable = IsUsable();
			foreach (var cell in building.PlacementCells) {
				Game.Instance.SetDupePassableSolid(cell, !locked && usable, !open || !usable);
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
		}

		// Token: 0x06000E55 RID: 3669 RVA: 0x0004AB1C File Offset: 0x00048D1C
		public void Sim200ms(float dt) {
			if (requestedState != locked) {
				// Automation locked or unlocked the door
				locked = requestedState;
				RefreshControlState();
				Trigger((int)GameHashes.DoorStateChanged, this);
			}
			if (operational.IsOperational) {
				float power = energyAvailable, capacity = EnergyCapacity;
				// Update active status
				if (consumer.IsPowered && power < capacity) {
					// Charging
					bool wasUsable = HasEnergy();
					operational.SetActive(true);
					energyAvailable = Math.Min(capacity, power + consumer.WattsUsed * dt);
					if (HasEnergy() != wasUsable) {
						UpdateWorldState();
						Trigger((int)GameHashes.OperationalFlagChanged, this);
					}
				} else
					// Not charging
					operational.SetActive(false);
			} else
				operational.SetActive(false);
		}

		public sealed class States : GameStateMachine<States, Instance, AirlockDoor> {
			public override void InitializeStates(out BaseState default_state) {
				serializable = true;
				default_state = notOperational;
				notOperational.PlayAnim("locked").
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState()).
					EventTransition(GameHashes.OperationalFlagChanged, closed, (smi) => smi.master.IsUsable());
				// If it cannot close because of Duplicants in the door, wait for them to clear
				open.PlayAnim("open").
					Enter("EnterCheckBlocked", (smi) => smi.CheckDuplicantStatus()).
					Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
					ParamTransition(isTraversing, closeDelay, IsFalse);
				closeDelay.PlayAnim("open").
					Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
					ScheduleGoTo(0.5f, closing).
					ParamTransition(isTraversing, open, IsTrue);
				// Door is being closed
				closing.ParamTransition(isTraversing, open, IsTrue).
					Update("CheckIsBlocked", (smi, _) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms).
					ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Closes", (smi) => smi.master.doorClosingSound, (smi) => !string.IsNullOrEmpty(smi.master.doorClosingSound)).
					Update((smi, dt) => {
						if (smi.master.doorClosingSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorClosingSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("closing").OnAnimQueueComplete(closed);
				// Start opening if requested, lock if requested
				closed.PlayAnim("closed").
					EventTransition(GameHashes.OperationalFlagChanged, notOperational, (smi) => !smi.master.IsUsable()).
					ParamTransition(isOpen, opening, IsTrue).
					ParamTransition(isLocked, locking, IsTrue).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				// The locked state displays the "no" icon on the door
				locking.PlayAnim("locked_pre").OnAnimQueueComplete(locked).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				locked.PlayAnim("locked").
					ParamTransition(isLocked, unlocking, IsFalse).
					EventTransition(GameHashes.OperationalFlagChanged, notOperational, (smi) => !smi.master.IsUsable());
				unlocking.PlayAnim("locked_pst").OnAnimQueueComplete(closed);
				// Door is being opened
				opening.ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Opens", (smi) => smi.master.doorOpeningSound, (smi) => !string.IsNullOrEmpty(smi.master.doorOpeningSound)).
					Enter("RemoveEnergy", (smi) => smi.WithdrawEnergy()).
					Update((smi, dt) => {
						if (smi.master.doorOpeningSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorOpeningSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("opening").OnAnimQueueComplete(open);
			}

			/// <summary>
			/// Not operational / broken down / no charge. Door is considered closed.
			/// </summary>
			public State notOperational;

			/// <summary>
			/// Open animation playing.
			/// </summary>
			public State opening;

			/// <summary>
			/// Closed in automatic mode.
			/// </summary>
			public State closed;

			/// <summary>
			/// Close animation playing.
			/// </summary>
			public State closing;

			/// <summary>
			/// Waiting to close, 0.5s with no Duplicants passing then closes.
			/// </summary>
			public State closeDelay;

			/// <summary>
			/// Waiting to close, Duplicant is still traversing the door.
			/// </summary>
			public State open;

			/// <summary>
			/// Closed and lock process started.
			/// </summary>
			public State locking;

			/// <summary>
			/// Closed and locked to access.
			/// </summary>
			public State locked;

			/// <summary>
			/// Closed and unlock process started.
			/// </summary>
			public State unlocking;

			/// <summary>
			/// True if the door is open.
			/// </summary>
			public BoolParameter isOpen;

			/// <summary>
			/// True if the door is currently locked by automation or toggle.
			/// No Duplicants can pass if locked.
			/// </summary>
			public BoolParameter isLocked;

			/// <summary>
			/// True if a Duplicant is passing through.
			/// </summary>
			public BoolParameter isTraversing;
		}

		/// <summary>
		/// The instance parameters of this state machine.
		/// </summary>
		public sealed class Instance : States.GameInstance {
			public Instance(AirlockDoor door) : base(door) { }

			/// <summary>
			/// Updates the traversing parameter if a Duplicant is currently passing
			/// through the door.
			/// </summary>
			public void CheckDuplicantStatus() {
				bool value = false;
				foreach (int cell in master.building.PlacementCells)
					if (Grid.Objects[cell, (int)ObjectLayer.Minion] != null) {
						value = true;
						break;
					}
				sm.isTraversing.Set(value, smi);
			}

			/// <summary>
			/// Withdraws energy when the door opens to admit a Duplicant.
			/// </summary>
			public void WithdrawEnergy() {
				// Door was closed and has started to open, withdraw energy
				master.energyAvailable = Math.Max(0.0f, master.energyAvailable - master.
					EnergyPerUse);
			}
		}
	}
}
