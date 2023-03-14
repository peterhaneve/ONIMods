/*
 * Copyright 2023 Peter Han
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
using System;
using UnityEngine;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// A pump component which fixes the issue of the detect and absorb ranges being different
	/// on liquid pumps.
	/// </summary>
	public class PumpFixed : KMonoBehaviour, ISim1000ms {
		/// <summary>
		/// The interval between updates.
		/// </summary>
		private const float OPERATIONAL_UPDATE_INTERVAL = 1.0f;

		/// <summary>
		/// A fixed version of Pump.IsPumpable that matches the detect and absorb radii.
		/// </summary>
		/// <param name="pump">The pump which will be pumping the material.</param>
		/// <param name="state">The element state to pump.</param>
		/// <param name="element">The element to pump, or SimHashes.Vacuum to pump any
		/// element.</param>
		/// <param name="radius">The pump radius.</param>
		/// <returns>true if there is matching material to pump, or false otherwise.</returns>
		protected static bool IsPumpableFixed(GameObject pump, Element.State state,
				SimHashes element, int radius) {
			bool hasMatch = false;
			if (radius < 1)
				throw new ArgumentException("radius");
			int offset = Math.Max(0, radius - 1), diameter = 2 * radius - 1;
			// Adjust for larger than radius 1 (same tile) or 2 (2x2)
			int baseCell = Grid.PosToCell(pump);
			if (offset > 0)
				baseCell = Grid.OffsetCell(baseCell, new CellOffset(-offset, -offset));
			for (int i = 0; i < diameter && !hasMatch; i++)
				for (int j = 0; j < diameter && !hasMatch; j++) {
					int cell = baseCell + j + Grid.WidthInCells * i;
					// Added this valid cell check to avoid pumping off the map
					if (Grid.IsValidCell(cell) && Math.Abs(i - offset) + Math.Abs(j -
							offset) <= offset) {
						var ge = Grid.Element[cell];
						hasMatch = ge.IsState(state) && (element == SimHashes.Vacuum ||
							element == ge.id);
					}
				}
			return hasMatch;
		}

		// These components are automatically populated by KMonoBehaviour
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpReq]
		protected ElementConsumer consumer;

		[MyCmpReq]
		protected ConduitDispenser dispenser;

		[MyCmpReq]
		protected Operational operational;

		[MyCmpReq]
		protected KSelectable selectable;

		[MyCmpReq]
		protected Storage storage;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// The detect radius. If 0 or less, uses the elment consumer's absorb radius.
		/// 
		/// The element consumer's absorb radius is still used for absorbing.
		/// </summary>
		[SerializeField]
		public int detectRadius;

		/// <summary>
		/// The GUID for "pipe blocked".
		/// </summary>
		protected Guid conduitBlockedGuid;

		/// <summary>
		/// The elapsed time spent pumping.
		/// </summary>
		private float elapsedTime;

		/// <summary>
		/// The GUID for "no gas/liquid".
		/// </summary>
		protected Guid noElementGuid;

		/// <summary>
		/// The status item indicating that no gas is available to pump.
		/// </summary>
		protected StatusItem noGasAvailable;

		/// <summary>
		/// The status item indicating that no liquid is available to pump.
		/// </summary>
		protected StatusItem noLiquidAvailable;

		/// <summary>
		/// Whether filtered elements exist to be pumped.
		/// </summary>
		protected bool pumpable;

		/// <summary>
		/// A delegate which invokes SimRegister() to register the sim element consumer.
		/// </summary>
		private System.Action simRegister;

		/// <summary>
		/// A delegate which invokes SimUnregister() to remove the sim element consumer.
		/// </summary>
		private System.Action simUnregister;

		internal PumpFixed() {
			simRegister = null;
			simUnregister = null;
		}

		/// <summary>
		/// Checks for pumpable media of the right type in the pump's radius.
		/// 
		/// This version has the detect radius synchronized with the absorb radius.
		/// </summary>
		/// <param name="state">The media state required.</param>
		/// <param name="radius">The radius to check.</param>
		/// <returns>Whether the pump can run.</returns>
		protected virtual bool IsPumpable(Element.State state, int radius) {
			return IsPumpableFixed(gameObject, state, SimHashes.Vacuum, radius);
		}

		protected override void OnCleanUp() {
			dispenser.GetConduitManager()?.RemoveConduitUpdater(OnConduitUpdate);
			base.OnCleanUp();
		}

		// Called when conduits are updated.
		private void OnConduitUpdate(float dt) {
			conduitBlockedGuid = selectable.ToggleStatusItem(Db.Get().BuildingStatusItems.
				ConduitBlocked, conduitBlockedGuid, dispenser.blocked);
		}

		protected override void OnPrefabInit() {
			var statusItems = Db.Get().BuildingStatusItems;
			base.OnPrefabInit();
			consumer.EnableConsumption(false);
			// Create delegates
			simRegister = typeof(SimComponent).CreateDelegate<System.Action>("SimRegister",
				consumer);
			simUnregister = typeof(SimComponent).CreateDelegate<System.Action>("SimUnregister",
				consumer);
			// These can be replaced by subclasses
			noGasAvailable = statusItems.NoGasElementToPump;
			noLiquidAvailable = statusItems.NoLiquidElementToPump;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			elapsedTime = 0f;
			pumpable = UpdateOperational();
			dispenser.GetConduitManager()?.AddConduitUpdater(OnConduitUpdate,
				ConduitFlowPriority.LastPostUpdate);
		}

		/// <summary>
		/// Re-creates the SimHandle when the element changes.
		/// </summary>
		protected void RecreateSimHandle() {
			// These already check for validity
			if (simRegister == null || simUnregister == null)
				PUtil.LogWarning("Unable to register sim component of ElementConsumer");
			else {
				simUnregister.Invoke();
				simRegister.Invoke();
			}
		}

		public void Sim1000ms(float dt) {
			elapsedTime += dt;
			if (elapsedTime >= OPERATIONAL_UPDATE_INTERVAL) {
				// Once a second, check for pumpable materials
				pumpable = UpdateOperational();
				elapsedTime = 0f;
			}
			if (operational.IsOperational && pumpable)
				operational.SetActive(true, false);
			else
				operational.SetActive(false, false);
		}

		/// <summary>
		/// Updates the operational status of this pump.
		/// </summary>
		/// <returns>true if the pump has media to pump, or false otherwise.</returns>
		private bool UpdateOperational() {
			var state = Element.State.Vacuum;
			// Determine state to pump
			switch (dispenser.conduitType) {
			case ConduitType.Gas:
				state = Element.State.Gas;
				break;
			case ConduitType.Liquid:
				state = Element.State.Liquid;
				break;
			}
			bool hasMedia = IsPumpable(state, (detectRadius > 0) ? detectRadius : consumer.
				consumptionRadius);
			var statusItem = (state == Element.State.Gas) ? noGasAvailable : noLiquidAvailable;
			noElementGuid = selectable.ToggleStatusItem(statusItem, noElementGuid,
				!hasMedia, null);
			operational.SetFlag(Pump.PumpableFlag, !storage.IsFull() && hasMedia);
			return hasMedia;
		}
	}
}
