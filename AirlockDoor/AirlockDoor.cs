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

using KSerialization;
using PeterHan.PLib.Detours;
using System;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// A version of Door that never permits gas and liquids to pass unless set to open.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed partial class AirlockDoor : StateMachineComponent<AirlockDoor.Instance>,
			ISaveLoadable, ISim200ms {
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
			doorControlState = new StatusItem("CurrentDoorControlState", "BUILDING",
				"", StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.
				None.ID);
			doorControlState.resolveStringCallback = (str, data) => {
				bool locked = (data as AirlockDoor)?.locked ?? true;
				return str.Replace("{ControlState}", Strings.Get(
					"STRINGS.BUILDING.STATUSITEMS.CURRENTDOORCONTROLSTATE." + (locked ?
					"LOCKED" : "AUTO")));
			};
			storedCharge = new StatusItem("AirlockStoredCharge", "BUILDING", "",
				StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.None.
				ID);
			storedCharge.resolveStringCallback = (str, data) => {
				if (data is AirlockDoor door)
					str = string.Format(str, GameUtil.GetFormattedRoundedJoules(door.
						EnergyAvailable), GameUtil.GetFormattedRoundedJoules(door.
						EnergyCapacity), GameUtil.GetFormattedRoundedJoules(door.
						EnergyPerUse));
				return str;
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
		/// Counts Duplicants entering the airlock travelling left to right.
		/// </summary>
		internal SideReferenceCounter EnterLeft { get; private set; }

		/// <summary>
		/// Counts Duplicants entering the airlock travelling right to left.
		/// </summary>
		internal SideReferenceCounter EnterRight { get; private set; }

		/// <summary>
		/// Counts Duplicants leaving the airlock travelling right to left.
		/// </summary>
		internal SideReferenceCounter ExitLeft { get; private set; }

		/// <summary>
		/// Counts Duplicants leaving the airlock travelling left to right.
		/// </summary>
		internal SideReferenceCounter ExitRight { get; private set; }

		/// <summary>
		/// Returns true if the airlock door is currently open for entry or exit from the left.
		/// </summary>
		/// <returns>Whether the door is open on the left.</returns>
		public bool IsLeftOpen {
			get {
				return smi.IsInsideState(smi.sm.left.waitEnter) || smi.IsInsideState(smi.sm.
					left.waitEnterClose) || smi.IsInsideState(smi.sm.left.waitExit) ||
					smi.IsInsideState(smi.sm.left.waitExitClose);
			}
		}

		/// <summary>
		/// Returns true if the airlock door is currently open for entry or exit from the right.
		/// </summary>
		/// <returns>Whether the door is open on the right.</returns>
		public bool IsRightOpen {
			get {
				return smi.IsInsideState(smi.sm.right.waitEnter) || smi.IsInsideState(smi.sm.
					right.waitEnterClose) || smi.IsInsideState(smi.sm.right.waitExit) ||
					smi.IsInsideState(smi.sm.right.waitExitClose);
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
		/// The energy meter.
		/// </summary>
		private MeterController meter;

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
		/// Gets the base cell (center bottom) of this door.
		/// </summary>
		/// <returns>The door's foundation cell.</returns>
		public int GetBaseCell() {
			return building.GetCell();
		}

		public override int GetHashCode() {
			return building.GetCell();
		}

		/// <summary>
		/// Whether the door has enough energy for one use.
		/// </summary>
		/// <returns>true if there is energy for a Duplicant to pass, or false otherwise.</returns>
		public bool HasEnergy() {
			return EnergyAvailable >= EnergyPerUse;
		}

		/// <summary>
		/// Whether the door is currently passing Duplicants.
		/// </summary>
		/// <returns>true if the door is active, or false if it is idle / disabled / out of power.</returns>
		public bool IsDoorActive() {
			return smi.IsInsideState(smi.sm.left) || smi.IsInsideState(smi.sm.
				vacuum) || smi.IsInsideState(smi.sm.right) || smi.IsInsideState(smi.sm.
				vacuum_check);
		}

		/// <summary>
		/// Whether a Duplicant can traverse the door.
		/// </summary>
		/// <returns>true if the door is passable, or false otherwise.</returns>
		public bool IsUsable() {
			return operational.IsFunctional && HasEnergy();
		}

		/// <summary>
		/// Whether a Duplicant can traverse the door. Also true if the door is currently
		/// operating.
		/// </summary>
		/// <returns>true if the door is passable, or false otherwise.</returns>
		private bool IsUsableOrActive() {
			return IsUsable() || IsDoorActive();
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
			Subscribe((int)GameHashes.LogicEvent, OnLogicValueChanged);
			// Handle transitions
			EnterLeft = new SideReferenceCounter(this, smi.sm.waitEnterLeft);
			EnterRight = new SideReferenceCounter(this, smi.sm.waitEnterRight);
			ExitLeft = new SideReferenceCounter(this, smi.sm.waitExitLeft);
			ExitRight = new SideReferenceCounter(this, smi.sm.waitExitRight);
			requestedState = locked;
			smi.StartSM();
			RefreshControlState();
			SetFakeFloor(true);
			// Lock out the critters
			foreach (int cell in building.PlacementCells) {
				Grid.CritterImpassable[cell] = true;
				Grid.HasDoor[cell] = true;
				Pathfinding.Instance.AddDirtyNavGridCell(cell);
			}
			var kac = GetComponent<KAnimControllerBase>();
			if (kac != null)
				// Stock mechanized airlocks are 5.0f powered
				kac.PlaySpeedMultiplier = 4.0f;
			// Layer is ignored if you use infront
			meter = new MeterController(kac, "meter_target", "meter", Meter.Offset.
				Infront, Grid.SceneLayer.NoLayer);
			// Door is always powered when used
			if (doorClosingSound != null)
				loopingSounds.UpdateFirstParameter(doorClosingSound, SOUND_POWERED_PARAMETER,
					1.0f);
			if (doorOpeningSound != null)
				loopingSounds.UpdateFirstParameter(doorOpeningSound, SOUND_POWERED_PARAMETER,
					1.0f);
			selectable.SetStatusItem(Db.Get().StatusItemCategories.OperatingEnergy,
				storedCharge, this);
		}

		protected override void OnCleanUp() {
			SetFakeFloor(false);
			foreach (int cell in building.PlacementCells) {
				// Clear the airlock flags, render critter and duplicant passable
				Grid.HasDoor[cell] = false;
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
		/// Enables or disables the fake floor along the top of the door.
		/// </summary>
		/// <param name="enable">true to add a fake floor, or false to remove it.</param>
		private void SetFakeFloor(bool enable) {
			// Place fake floor along the top
			int width = building.Def.WidthInCells, start = Grid.PosToCell(this), height =
				building.Def.HeightInCells;
			for (int i = 0; i < width; i++) {
				int target = Grid.OffsetCell(start, i, height);
				if (Grid.IsValidCell(target)) {
					Grid.FakeFloor[target] = enable;
					Pathfinding.Instance.AddDirtyNavGridCell(target);
				}
			}
		}

		public void Sim200ms(float dt) {
			if (requestedState != locked && !IsDoorActive()) {
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
					UpdateMeter();
				} else
					// Not charging
					operational.SetActive(false);
			} else
				operational.SetActive(false);
		}

		/// <summary>
		/// Updates the energy meter.
		/// </summary>
		private void UpdateMeter() {
			meter?.SetPositionPercent(energyAvailable / EnergyCapacity);
		}

		/// <summary>
		/// Updates the state of the door's cells in the game.
		/// </summary>
		private void UpdateWorldState() {
			bool usable = IsUsableOrActive(), openLeft = IsLeftOpen, openRight = IsRightOpen;
			int baseCell = building.GetCell(), centerUpCell = Grid.CellAbove(baseCell);
			if (Grid.IsValidBuildingCell(baseCell))
				Game.Instance.SetDupePassableSolid(baseCell, !locked && usable, false);
			if (Grid.IsValidBuildingCell(centerUpCell))
				Game.Instance.SetDupePassableSolid(centerUpCell, !locked && usable, false);
			// Left side cells controlled by left open
			UpdateWorldState(Grid.CellLeft(baseCell), usable, openLeft);
			UpdateWorldState(Grid.CellUpLeft(baseCell), usable, openLeft);
			// Right side cells controlled by right open
			UpdateWorldState(Grid.CellRight(baseCell), usable, openRight);
			UpdateWorldState(Grid.CellUpRight(baseCell), usable, openRight);
			var inst = Pathfinding.Instance;
			foreach (var cell in building.PlacementCells)
				inst.AddDirtyNavGridCell(cell);
		}

		/// <summary>
		/// Updates the world state of one cell in the airlock.
		/// </summary>
		/// <param name="cell">The cell to update.</param>
		/// <param name="usable">Whether the door is currently usable.</param>
		/// <param name="open">Whether that cell is currently open.</param>
		private void UpdateWorldState(int cell, bool usable, bool open) {
			if (Grid.IsValidBuildingCell(cell))
				Game.Instance.SetDupePassableSolid(cell, !locked && usable, !open || !usable);
		}

		/// <summary>
		/// Counts the number of Duplicants waiting at transition points.
		/// </summary>
		internal sealed class SideReferenceCounter {
			/// <summary>
			/// How many Duplicants are currently waiting.
			/// </summary>
			public int WaitingCount { get; private set; }

			/// <summary>
			/// The master door.
			/// </summary>
			private readonly AirlockDoor door;

			/// <summary>
			/// The parameter to set if a request is pending.
			/// </summary>
			private readonly States.BoolParameter parameter;

			internal SideReferenceCounter(AirlockDoor door, States.BoolParameter parameter) {
				this.door = door ?? throw new ArgumentNullException("door");
				this.parameter = parameter ?? throw new ArgumentNullException("parameter");
			}

			/// <summary>
			/// Completes transition through the point.
			/// </summary>
			public void Finish() {
				if (door != null) {
					if (door.locked)
						parameter.Set(false, door.smi);
					else {
						int count = Math.Max(0, WaitingCount - 1);
						WaitingCount = count;
						if (count == 0)
							parameter.Set(false, door.smi);
					}
				}
			}

			/// <summary>
			/// Queues a request to traverse the transition point.
			/// </summary>
			public void Queue() {
				if (door != null && !door.locked && door.IsUsableOrActive()) {
					WaitingCount++;
					parameter.Set(true, door.smi);
				}
			}
		}
	}
}
