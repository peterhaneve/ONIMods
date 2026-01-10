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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using ConduitUtilityNetworkManager = UtilityNetworkManager<FlowUtilityNetwork, Vent>;
using SolidUtilityNetworkManager = UtilityNetworkManager<FlowUtilityNetwork, SolidConduit>;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.ConduitPatches {
	/// <summary>
	/// Updates the liquid, gas, and solid conduits in the background.
	/// </summary>
	public sealed class BackgroundConduitUpdater : IWorkItemCollection, AsyncJobManager.IWork,
			IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BackgroundConduitUpdater Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		public static void CreateInstance() {
			Instance?.Dispose();
			Instance = new BackgroundConduitUpdater();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		public static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// Starts updating all three conduit networks in the background.
		/// </summary>
		public static void StartUpdateAll() {
			Instance?.StartUpdate();
		}

		/// <summary>
		/// Waits for all networks to complete and fires the event handlers if they rebuilt.
		/// </summary>
		public static void FinishUpdateAll() {
			Instance?.WaitForCompletion();
		}

		/// <summary>
		/// Triggered when all jobs complete.
		/// </summary>
		private readonly EventWaitHandle onComplete;

		/// <summary>
		/// Whether WaitForCompletion needs to actually wait.
		/// </summary>
		private volatile bool running;

		/// <summary>
		/// Set to true if each system was dirty.
		/// </summary>
		private readonly bool[] updated;

		public int Count => 3;

		public IWorkItemCollection Jobs => this;

		public BackgroundConduitUpdater() {
			onComplete = new AutoResetEvent(false);
			running = false;
			updated = new bool[Count];
		}

		public void Dispose() {
			onComplete.Dispose();
		}

		public void InternalDoWorkItem(int index, int threadIndex) {
			var inst = Game.Instance;
			if (inst != null && inst.IsInitialized() && !inst.IsLoading())
				switch (index) {
				case 0:
					updated[0] = inst.gasConduitSystem.IsDirty;
					ConduitUtilityNetworkUpdater.Update(inst.gasConduitSystem);
					break;
				case 1:
					updated[1] = inst.liquidConduitSystem.IsDirty;
					ConduitUtilityNetworkUpdater.Update(inst.liquidConduitSystem);
					break;
				case 2:
					updated[2] = inst.solidConduitSystem.IsDirty;
					SolidUtilityNetworkUpdater.Update(inst.solidConduitSystem);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(index), index,
						"Must be from 0 to " + (Count - 1));
				}
		}

		/// <summary>
		/// Starts updating the liquid and gas conduit systems.
		/// </summary>
		private void StartUpdate() {
			var inst = Game.Instance;
			bool startUpdate = inst.solidConduitSystem.IsDirty;
			running = false;
			if (inst.gasConduitSystem.IsDirty) {
				// Invalidate: gas
				startUpdate = true;
				ConduitFlowVisualizerPatches.ForceUpdate(inst.gasFlowVisualizer);
			}
			if (inst.liquidConduitSystem.IsDirty) {
				// Invalidate: liquid
				startUpdate = true;
				ConduitFlowVisualizerPatches.ForceUpdate(inst.liquidFlowVisualizer);
			}
			if (inst != null && !inst.IsLoading() && startUpdate) {
				var jobManager = AsyncJobManager.Instance;
				if (jobManager == null) {
					// Trigger a synchronous update if job manager is somehow unavailable
					inst.gasConduitSystem.Update();
					inst.liquidConduitSystem.Update();
					inst.solidConduitSystem.Update();
				} else {
					onComplete.Reset();
					jobManager.Run(this);
					running = true;
				}
			}
		}

		public void TriggerAbort() {
			onComplete.Set();
		}

		public void TriggerComplete() {
			onComplete.Set();
		}

		public void TriggerStart() { }

		/// <summary>
		/// Waits for completion of all updates and then fires the rebuild events if
		/// necessary.
		/// </summary>
		private void WaitForCompletion() {
			var inst = Game.Instance;
			if (running && inst != null && inst.IsInitialized() && !inst.IsLoading()) {
				if (!onComplete.WaitAndMeasure(FastTrackMod.MAX_TIMEOUT))
					PUtil.LogWarning("Conduit updates did not finish within the timeout!");
				// They are always clean after running
				if (updated[0])
					ConduitUtilityNetworkUpdater.TriggerEvent(inst.gasConduitSystem);
				if (updated[1])
					ConduitUtilityNetworkUpdater.TriggerEvent(inst.liquidConduitSystem);
				if (updated[2])
					SolidUtilityNetworkUpdater.TriggerEvent(inst.solidConduitSystem);
				running = false;
			}
		}
	}

	/// <summary>
	/// Applied to Game to restructure the conduit updates for better parallelism.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.LateUpdate))]
	public static class BackgroundConduitLateUpdatePatch {
		internal static bool Prepare() => FastTrackOptions.Instance.ConduitOpts;

		/// <summary>
		/// Transpiles LateUpdate to replace methods with versions that use parallelism.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var finishUpdates = typeof(BackgroundConduitUpdater).GetMethodSafe(nameof(
				BackgroundConduitUpdater.FinishUpdateAll), true);
			var updateLiquidGas = typeof(ConduitUtilityNetworkManager).GetMethodSafe(nameof(
				ConduitUtilityNetworkManager.Update), false);
			var updateSolid = typeof(SolidUtilityNetworkManager).GetMethodSafe(nameof(
				SolidUtilityNetworkManager.Update), false);
			if (finishUpdates == null)
				throw new ArgumentException("Unable to find BackgroundConduitUpdater methods");
			bool patched = false;
			if (updateLiquidGas != null && updateSolid != null)
				foreach (var instr in instructions) {
					var opcode = instr.opcode;
					if (opcode == OpCodes.Callvirt) {
						// Is it calling some version of UtilityNetworkManager.Update
						var operand = instr.operand as MethodBase;
						if (operand == updateSolid) {
							yield return new CodeInstruction(OpCodes.Call, finishUpdates);
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
#if DEBUG
							PUtil.LogDebug("Patched Game.LateUpdate for background conduits");
#endif
							patched = true;
						} else if (operand == updateLiquidGas) {
							// Replace with nothing
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
						}
					}
					yield return instr;
				}
			else
				foreach (var instr in instructions)
					yield return instr;
			if (!patched)
				PUtil.LogWarning("Unable to patch Game.LateUpdate for background conduits");
		}
	}

	/// <summary>
	/// Applied to Game to restructure the conduit updates for better parallelism.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.Update))]
	public static class BackgroundConduitUpdatePatch {
		internal static bool Prepare() => FastTrackOptions.Instance.ConduitOpts;

		/// <summary>
		/// Transpiles Update to replace methods with versions that use parallelism.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var finishUpdates = typeof(BackgroundConduitUpdater).GetMethodSafe(nameof(
				BackgroundConduitUpdater.FinishUpdateAll), true);
			var updateLiquidGas = typeof(ConduitUtilityNetworkManager).GetMethodSafe(nameof(
				ConduitUtilityNetworkManager.Update), false);
			var updateSolid = typeof(SolidUtilityNetworkManager).GetMethodSafe(nameof(
				SolidUtilityNetworkManager.Update), false);
			if (finishUpdates == null)
				throw new ArgumentException("Unable to find BackgroundConduitUpdater methods");
			bool patched = false;
			if (updateLiquidGas != null && updateSolid != null)
				foreach (var instr in instructions) {
					var opcode = instr.opcode;
					if (opcode == OpCodes.Callvirt) {
						// Is it calling some version of UtilityNetworkManager.Update
						var operand = instr.operand as MethodBase;
						if (operand == updateSolid) {
							yield return new CodeInstruction(OpCodes.Call, finishUpdates);
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
#if DEBUG
							PUtil.LogDebug("Patched Game.Update for background conduits");
#endif
							patched = true;
						} else if (operand == updateLiquidGas) {
							// Replace with nothing
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
						}
					}
					yield return instr;
				}
			else
				foreach (var instr in instructions)
					yield return instr;
			if (!patched)
				PUtil.LogWarning("Unable to patch Game.Update for background conduits");
		}
	}
}
