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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// A version of Door that never permits gas and liquids to pass unless set to open.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class AirlockDoor : Workable, ISaveLoadable, ISim200ms {
		/// <summary>
		/// The status item showing the door's requested state.
		/// </summary>
		private static StatusItem changeControlState;

		/// <summary>
		/// The status item showing the door's current state.
		/// </summary>
		private static StatusItem doorControlState;

		/// <summary>
		/// Prevents initialization from multiple threads at once.
		/// </summary>
		private static readonly object INIT_LOCK = new object();

		private static readonly EventSystem.IntraObjectHandler<AirlockDoor> OnCopySettingsDelegate =
			new EventSystem.IntraObjectHandler<AirlockDoor>(OnCopySettings);

		/// <summary>
		/// The basic sim flags applied to the door while it is open and isolating gas/liquid.
		/// </summary>
		private const int SIM_FLAGS_AIRLOCK = (int)(Sim.Cell.Properties.GasImpermeable |
			Sim.Cell.Properties.LiquidImpermeable | Sim.Cell.Properties.SolidImpermeable);

		/// <summary>
		/// The basic sim flags applied to the door.
		/// </summary>
		private const int SIM_FLAGS_BASE = (int)(Sim.Cell.Properties.Unbreakable);

		/// <summary>
		/// The parameter for the sound indicating whether the door has power.
		/// </summary>
		private static readonly HashedString SOUND_POWERED_PARAMETER = "doorPowered";

		/// <summary>
		/// The parameter for the sound indicating the progress of the door close.
		/// </summary>
		private static readonly HashedString SOUND_PROGRESS_PARAMETER = "doorProgress";

		/// <summary>
		/// Cycles to the next door control state.
		/// </summary>
		/// <param name="state">The current control state.</param>
		/// <returns>The next control state, wrapping around if necessary.</returns>
		private static Door.ControlState GetNextState(Door.ControlState state) {
			return (Door.ControlState)(((int)state + 1) % (int)Door.ControlState.NumStates);
		}

		private static void OnCopySettings(AirlockDoor target, object data) {
			var otherDoor = (data as GameObject).GetComponentSafe<AirlockDoor>();
			if (otherDoor != null)
				target.QueueStateChange(otherDoor.RequestedState);
		}

		/// <summary>
		/// When the door is first instantiated, initializes the static fields, to avoid the
		/// crash that the stock Door has if it is loaded too early.
		/// </summary>
		private static void StaticInit() {
			changeControlState = new StatusItem("ChangeDoorControlState", "BUILDING",
					"status_item_pending_switch_toggle", StatusItem.IconType.Custom,
					NotificationType.Neutral, false, OverlayModes.None.ID) {
				resolveStringCallback = delegate (string str, object data) {
					var door = data as AirlockDoor;
					return str.Replace("{ControlState}", Strings.Get(
						"STRINGS.BUILDING.STATUSITEMS.CURRENTDOORCONTROLSTATE." + (door ==
						null ? "UNKNOWN" : door.RequestedState.ToString().ToUpperInvariant())));
				}
			};
			doorControlState = new StatusItem("CurrentDoorControlState", "BUILDING", "",
					StatusItem.IconType.Info, NotificationType.Neutral, false,
					OverlayModes.None.ID) {
				resolveStringCallback = delegate (string str, object data) {
					var door = data as AirlockDoor;
					return str.Replace("{ControlState}", Strings.Get(
						"STRINGS.BUILDING.STATUSITEMS.CURRENTDOORCONTROLSTATE." + (door ==
						null ? "UNKNOWN" : door.CurrentState.ToString().ToUpperInvariant())));
				}
			};
		}

		/// <summary>
		/// The current door state.
		/// </summary>
		public Door.ControlState CurrentState {
			get {
				return controlState;
			}
		}

		/// <summary>
		/// Returns true if the airlock door is currently open (set open, or open in auto mode).
		/// </summary>
		/// <returns>Whether the door is open.</returns>
		public bool IsOpen {
			get {
				return controller.IsInsideState(controller.sm.open) ||
					controller.IsInsideState(controller.sm.closeDelay) ||
					controller.IsInsideState(controller.sm.closeWaiting);
			}
		}

		/// <summary>
		/// The state that will be set when the errand to toggle settings completes.
		/// </summary>
		public Door.ControlState RequestedState { get; private set; }

		/// <summary>
		/// Whether a state change from automation is pending.
		/// </summary>
		private bool autoChangePending;

		/// <summary>
		/// The queued chore if a toggle errand is pending to open/close the door.
		/// </summary>
		private Chore changeStateChore;

		/// <summary>
		/// Whether this door will automatically self-destruct if it thinks it has melted.
		/// </summary>
		private bool checkForMelt;

		/// <summary>
		/// The state machine controlling this airlock door.
		/// Cannot extend StateMachineComponent because we already must extend Workable...
		/// </summary>
		private Controller.Instance controller;

		/// <summary>
		/// The current door state.
		/// </summary>
		[Serialize]
		[SerializeField]
		private Door.ControlState controlState;

		/// <summary>
		/// The sound played while the door is closing.
		/// </summary>
		private string doorClosingSound;

		/// <summary>
		/// The sound played while the door is opening.
		/// </summary>
		private string doorOpeningSound;

		/// <summary>
		/// A reference counter of how many times Open()/Close() have been called.
		/// </summary>
		private int openCount;

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

		[MyCmpReq]
		private Rotatable rotatable;

		[MyCmpGet]
		private KSelectable selectable;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		internal AirlockDoor() {
			autoChangePending = false;
			SetOffsetTable(OffsetGroups.InvertedStandardTable);
		}

		/// <summary>
		/// Applies the door settings queued by the work chore.
		/// </summary>
		/// <param name="force">Whether to force apply the changes.</param>
		private void ApplyRequestedControlState(bool force = false) {
			if (RequestedState != controlState || force) {
				controlState = RequestedState;
				RefreshControlState();
				OnOperationalChanged(null);
				selectable.RemoveStatusItem(changeControlState);
				Trigger((int)GameHashes.DoorStateChanged, this);
				if (!force) {
					Open();
					Close();
				}
			}
		}

		/// <summary>
		/// Closes the doors.
		/// </summary>
		public void Close() {
			openCount = Mathf.Max(0, openCount - 1);
			// Set temperature of the primary element equal to the structure's overall temp
			if (openCount == 0) {
				var structureTemperatures = GameComps.StructureTemperatures;
				var handle = structureTemperatures.GetHandle(gameObject);
				if (handle.IsValid() && !structureTemperatures.IsBypassed(handle))
					pe.Temperature = structureTemperatures.GetPayload(handle).Temperature;
			}
			switch (controlState) {
			case Door.ControlState.Auto:
				if (openCount == 0) {
					controller.sm.isOpen.Set(false, controller);
					Game.Instance.userMenu.Refresh(gameObject);
				}
				break;
			case Door.ControlState.Locked:
				controller.sm.isOpen.Set(false, controller);
				return;
			case Door.ControlState.Opened:
			default:
				return;
			}
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			overrideAnims = new KAnimFile[] { Assets.GetAnim("anim_use_remote_kanim") };
			lock (INIT_LOCK) {
				if (doorControlState == null)
					StaticInit();
			}
			synchronizeAnims = false;
			// Adding new sounds is actually very challenging
			doorClosingSound = GlobalAssets.GetSound("MechanizedAirlock_closing");
			doorOpeningSound = GlobalAssets.GetSound("MechanizedAirlock_opening");
			Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			var structureTemperatures = GameComps.StructureTemperatures;
			var handle = structureTemperatures.GetHandle(gameObject);
			structureTemperatures.Bypass(handle);
			openCount = 0;
			controller = new Controller.Instance(this);
			controller.StartSM();
			Subscribe((int)GameHashes.OperationalChanged, OnOperationalChanged);
			Subscribe((int)GameHashes.ActiveChanged, OnOperationalChanged);
			Subscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);
			RequestedState = CurrentState;
			ApplyRequestedControlState(true);
			var access = GetComponent<AccessControl>() != null;
			foreach (int cell in building.PlacementCells) {
				Grid.FakeFloor[cell] = true;
				Grid.HasDoor[cell] = true;
				Grid.HasAccessDoor[cell] = access;
				Grid.RenderedByWorld[cell] = false;
				SimMessages.SetCellProperties(cell, SIM_FLAGS_BASE);
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
			// Door is always powered
			if (doorClosingSound != null)
				loopingSounds.UpdateFirstParameter(doorClosingSound, SOUND_POWERED_PARAMETER, 1f);
			if (doorOpeningSound != null)
				loopingSounds.UpdateFirstParameter(doorOpeningSound, SOUND_POWERED_PARAMETER, 1f);
		}

		protected override void OnCleanUp() {
			foreach (int cell in building.PlacementCells) {
				// Clear the airlock flags, render critter and duplicant passable
				var element = Grid.Element[cell];
				SimMessages.ClearCellProperties(cell, SIM_FLAGS_AIRLOCK | SIM_FLAGS_BASE);
				Grid.RenderedByWorld[cell] = Traverse.Create(element.substance).
					GetField<bool>("renderedByWorld");
				if (element.IsSolid)
					// Not sure how a solid got inside of our door
					SimMessages.ReplaceAndDisplaceElement(cell, SimHashes.Vacuum,
						CellEventLogger.Instance.DoorOpen, 0f, -1f, byte.MaxValue, 0, -1);
				Grid.FakeFloor[cell] = false;
				Grid.HasDoor[cell] = false;
				Grid.HasAccessDoor[cell] = false;
				Game.Instance.SetDupePassableSolid(cell, false, Grid.Solid[cell]);
				Grid.CritterImpassable[cell] = false;
				Grid.DupeImpassable[cell] = false;
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
			Unsubscribe((int)GameHashes.OperationalChanged, OnOperationalChanged);
			Unsubscribe((int)GameHashes.ActiveChanged, OnOperationalChanged);
			Unsubscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);
			base.OnCleanUp();
		}

		protected override void OnCompleteWork(Worker worker) {
			// Toggle errand completed
			base.OnCompleteWork(worker);
			changeStateChore = null;
			ApplyRequestedControlState(false);
		}

		private void OnLogicValueChanged(object data) {
			var logicValueChanged = (LogicValueChanged)data;
			if (logicValueChanged.portID == Door.OPEN_CLOSE_PORT_ID) {
				int newValue = logicValueChanged.newValue;
				if (changeStateChore != null) {
					changeStateChore.Cancel("Automation state change");
					changeStateChore = null;
				}
				// Bit 0 green: automatic, bit 0 red: lock the door
				RequestedState = LogicCircuitNetwork.IsBitActive(0, newValue) ? Door.
					ControlState.Auto : Door.ControlState.Locked;
				autoChangePending = true;
			}
		}

		private void OnOperationalChanged(object data) {
			bool isOperational = operational.IsOperational;
		}

		/// <summary>
		/// When the airlock opens, make sure that the sim treats it like a building.
		/// </summary>
		private void OnSimDoorOpened() {
			if (this != null) {
				var structureTemperatures = GameComps.StructureTemperatures;
				var handle = structureTemperatures.GetHandle(gameObject);
				structureTemperatures.UnBypass(handle);
				checkForMelt = false;
			}
		}

		/// <summary>
		/// When the airlock opens, make sure that the sim treats it like solid tiles.
		/// </summary>
		private void OnSimDoorClosed() {
			if (this != null) {
				var structureTemperatures = GameComps.StructureTemperatures;
				var handle = structureTemperatures.GetHandle(gameObject);
				structureTemperatures.Bypass(handle);
				checkForMelt = true;
			}
		}

		/// <summary>
		/// Opens the pod bay doors!
		/// </summary>
		public void Open() {
			if (openCount == 0) {
				var structureTemperatures = GameComps.StructureTemperatures;
				var handle = structureTemperatures.GetHandle(gameObject);
				if (handle.IsValid() && structureTemperatures.IsBypassed(handle)) {
					int[] placementCells = building.PlacementCells;
					// Average the temperatures of each cell in the door
					float totalTemperature = 0f;
					int numTemperatures = 0;
					foreach (int cell in placementCells)
						if (Grid.Mass[cell] > 0.0f) {
							numTemperatures++;
							totalTemperature += Grid.Temperature[cell];
						}
					if (numTemperatures > 0)
						pe.Temperature = totalTemperature / numTemperatures;
				}
			}
			openCount++;
			if (controlState == Door.ControlState.Opened || controlState == Door.ControlState.
					Auto)
				controller.sm.isOpen.Set(true, controller);
		}

		/// <summary>
		/// Queues a door state change.
		/// </summary>
		/// <param name="nextState">The state which the door should use.</param>
		public void QueueStateChange(Door.ControlState nextState) {
			RequestedState = (RequestedState == nextState) ? controlState : nextState;
			if (RequestedState == controlState) {
				if (changeStateChore != null) {
					changeStateChore.Cancel("State changed");
					changeStateChore = null;
					selectable.RemoveStatusItem(changeControlState);
				}
			} else if (DebugHandler.InstantBuildMode)
				// Instantly open/close the door
				ApplyRequestedControlState(true);
			else {
				if (changeStateChore != null)
					changeStateChore.Cancel("State changed");
				selectable.AddStatusItem(changeControlState, this);
				// Create a toggle errand to change the door settings
				changeStateChore = new WorkChore<AirlockDoor>(Db.Get().ChoreTypes.Toggle, this,
					only_when_operational: false);
			}
		}

		/// <summary>
		/// Updates the locked/open/auto state in the UI and the state machine.
		/// </summary>
		private void RefreshControlState() {
			controller.sm.isLocked.Set(controlState == Door.ControlState.Locked, controller);
			Trigger((int)GameHashes.DoorControlStateChanged, controlState);
			UpdateWorldState();
			selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, doorControlState,
				this);
		}

		/// <summary>
		/// Sets the specified cells to be passable or impassable.
		/// </summary>
		/// <param name="open">Whether the door is open.</param>
		/// <param name="cells">The cells to modify.</param>
		private void SetPassableState(bool open, IList<int> cells) {
			for (int i = 0; i < cells.Count; i++) {
				int num = cells[i];
				Grid.CritterImpassable[num] = controlState != Door.ControlState.Opened;
				Game.Instance.SetDupePassableSolid(num, controlState != Door.ControlState.
					Locked, !open);
				Pathfinding.Instance.AddDirtyNavGridCell(num);
			}
		}

		/// <summary>
		/// Sets the state of this airlock door to the simulation (simdll).
		/// </summary>
		/// <param name="open">Whether the door is currently open.</param>
		/// <param name="isolate">If open, whether to block liquid and gas passage.</param>
		/// <param name="cells">The cells to modify.</param>
		private void SetSimState(bool open, bool isolate, IList<int> cells) {
			float mass = pe.Mass / cells.Count;
			foreach (int cell in cells) {
				World.Instance.groundRenderer.MarkDirty(cell);
				if (open) {
					// Remove the solids that make up this door
					SimMessages.Dig(cell, Game.Instance.callbackManager.Add(
						new Game.CallbackInfo(OnSimDoorOpened)).index);
					// Remove flags to allow liquids and gases to pass
					if (isolate) {
						SimMessages.ClearCellProperties(cell, SIM_FLAGS_BASE);
						SimMessages.SetCellProperties(cell, SIM_FLAGS_AIRLOCK);
					} else
						SimMessages.ClearCellProperties(cell, SIM_FLAGS_AIRLOCK | SIM_FLAGS_BASE);
				} else {
					var handle = Game.Instance.callbackManager.Add(new Game.CallbackInfo(
						OnSimDoorClosed));
					float temperature = pe.Temperature;
					// Avoid absolute zero crash
					if (temperature <= 0.0f)
						temperature = 0.1f;
					SimMessages.ReplaceAndDisplaceElement(cell, pe.ElementID, CellEventLogger.
						Instance.DoorClose, mass, temperature, callbackIdx: handle.index);
					// Set default flags but clear airlock flags (should be blocked anyways)
					SimMessages.ClearCellProperties(cell, SIM_FLAGS_AIRLOCK);
					SimMessages.SetCellProperties(cell, SIM_FLAGS_BASE);
				}
			}
		}

		/// <summary>
		/// Updates the state of the door's cells in the game.
		/// </summary>
		private void UpdateWorldState() {
			int[] placementCells = building.PlacementCells;
			bool open = IsOpen;
			SetPassableState(open, placementCells);
			SetSimState(open, controlState == Door.ControlState.Auto, placementCells);
		}

		// Token: 0x06000E55 RID: 3669 RVA: 0x0004AB1C File Offset: 0x00048D1C
		public void Sim200ms(float dt) {
			if (autoChangePending) {
				autoChangePending = false;
				ApplyRequestedControlState(false);
			}
			if (checkForMelt) {
				// If this door is shut but the cells have gone away (melt, rocket damage,
				// overpressure, and so forth), then destroy it
				var structureTemperatures = GameComps.StructureTemperatures;
				var handle = structureTemperatures.GetHandle(gameObject);
				if (handle.IsValid() && structureTemperatures.IsBypassed(handle))
					foreach (int cell in building.PlacementCells)
						if (!Grid.Solid[cell]) {
							Util.KDestroyGameObject(this);
							break;
						}
			}
		}

		/// <summary>
		/// Controls the airlock door's state.
		/// </summary>
		public sealed class Controller : GameStateMachine<Controller, Controller.Instance, AirlockDoor> {
			public override void InitializeStates(out BaseState default_state) {
				serializable = true;
				default_state = closed;
				root.Update("CheckIsBlocked", (smi, dt) => smi.CheckDuplicantStatus(), UpdateRate.SIM_200ms, false);
				// If it cannot close because of Duplicants in the door, wait for them to clear
				closeWaiting.PlayAnim("open").
					ParamTransition(isOpen, open, IsTrue).
					ParamTransition(isTraversing, closeDelay, IsFalse);
				closeDelay.PlayAnim("open").
					ScheduleGoTo(0.5f, closing).
					ParamTransition(isOpen, open, IsTrue).
					ParamTransition(isTraversing, closeWaiting, IsTrue);
				// Door is being closed
				closing.ParamTransition(isTraversing, closeWaiting, IsTrue).
					ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Closes", (smi) => smi.master.doorClosingSound, (smi) => !string.IsNullOrEmpty(smi.master.doorClosingSound)).
					Update((smi, dt) => {
						if (smi.master.doorClosingSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorClosingSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("closing").OnAnimQueueComplete(closed);
				open.PlayAnim("open").
					ParamTransition(isOpen, closeWaiting, IsFalse).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				// Start opening if requested, lock if requested
				closed.PlayAnim("closed").
					ParamTransition(isOpen, opening, IsTrue).
					ParamTransition(isLocked, locking, IsTrue).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				// The locked state displays the "no" icon on the door
				locking.PlayAnim("locked_pre").OnAnimQueueComplete(locked).
					Enter("UpdateWorldState", (smi) => smi.master.UpdateWorldState());
				locked.PlayAnim("locked").ParamTransition(isLocked, unlocking, IsFalse);
				unlocking.PlayAnim("locked_pst").OnAnimQueueComplete(closed);
				// Door is being opened
				opening.ToggleTag(GameTags.Transition).
					ToggleLoopingSound("Airlock Opens", (smi) => smi.master.doorOpeningSound, (smi) => !string.IsNullOrEmpty(smi.master.doorOpeningSound)).
					Update((smi, dt) => {
						if (smi.master.doorOpeningSound != null)
							smi.master.loopingSounds.UpdateSecondParameter(smi.master.doorOpeningSound, SOUND_PROGRESS_PARAMETER, smi.Get<KBatchedAnimController>().GetPositionPercent());
					}, UpdateRate.SIM_33ms, false).
					PlayAnim("opening").OnAnimQueueComplete(open);
			}

			/// <summary>
			/// Open in open mode. Does not isolate liquids and gases in this state.
			/// </summary>
			public State open;

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
			public State closeWaiting;

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

			/// <summary>
			/// The instance parameters of this state machine.
			/// </summary>
			public new class Instance : GameInstance {
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
			}
		}
	}
}
