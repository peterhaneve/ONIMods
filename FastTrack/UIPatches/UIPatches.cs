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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

using DiagnosticRow = ColonyDiagnosticScreen.DiagnosticRow;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to Bouncer to turn off notification bounces.
	/// </summary>
	[HarmonyPatch(typeof(Bouncer), nameof(Bouncer.Bounce))]
	public static class Bouncer_Bounce_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// Applied before Bounce runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// Applied to ColonyDiagnosticScreen.DiagnosticRow to suppress SparkChart updates if
	/// not visible.
	/// </summary>
	[HarmonyPatch(typeof(DiagnosticRow), nameof(DiagnosticRow.Sim4000ms))]
	public static class DiagnosticRow_Sim4000ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before Sim4000ms runs.
		/// </summary>
		internal static bool Prefix(KMonoBehaviour ___sparkLayer) {
			return ___sparkLayer == null || ___sparkLayer.isActiveAndEnabled;
		}
	}

	/// <summary>
	/// Applied to ColonyDiagnosticScreen.DiagnosticRow to turn off the bouncing effect.
	/// </summary>
	[HarmonyPatch(typeof(DiagnosticRow), nameof(DiagnosticRow.TriggerVisualNotification))]
	public static class DiagnosticRow_TriggerVisualNotification_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// A replacement coroutine that waits 3 seconds and then resolves it with no bounce.
		/// </summary>
		private static System.Collections.IEnumerator NoMoveRoutine(DiagnosticRow row) {
			// Wait for 3 seconds unscaled
			yield return new WaitForSeconds(3.0f);
			try {
				// Ignore exception if the notification cannot be resolved
				row.ResolveNotificationRoutine();
			} catch (Exception) { }
			yield break;
		}

		/// <summary>
		/// Transpiles TriggerVisualNotification to remove calls to the bouncy coroutine.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCall(instructions, typeof(DiagnosticRow).
				GetMethodSafe(nameof(DiagnosticRow.VisualNotificationRoutine), false),
				typeof(DiagnosticRow_TriggerVisualNotification_Patch).GetMethodSafe(nameof(
				NoMoveRoutine), true, typeof(DiagnosticRow)));
		}
	}

	/// <summary>
	/// Applied to InterfaceTool to get rid of an expensive raycast for UI elements.
	/// </summary>
	[HarmonyPatch(typeof(InterfaceTool), nameof(InterfaceTool.ShowHoverUI))]
	public static class InterfaceTool_ShowHoverUI_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastRaycast;

		/// <summary>
		/// Applied before ShowHoverUI runs.
		/// </summary>
		internal static bool Prefix(ref bool __result) {
			var pos = KInputManager.GetMousePos();
			var worldPos = Camera.main.ScreenToWorldPoint(pos);
			__result = false;
			// Check for trivial false cases
			if (OverlayScreen.Instance != null && worldPos.x >= 0.0f && worldPos.x <= Grid.
					WidthInMeters && worldPos.y >= 0.0f && worldPos.y <= Grid.HeightInMeters &&
					ClusterManager.Instance.IsPositionInActiveWorld(worldPos)) {
				var es = UnityEngine.EventSystems.EventSystem.current;
				if (es != null)
					// Consult the Unity system which did this raycast already
					__result = !es.IsPointerOverGameObject();
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to MainMenu to get rid of the 15 MB file write and speed up boot!
	/// </summary>
	[HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OnSpawn))]
	public static class MainMenu_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Transpiles OnSpawn to remove everything in try/catch IOException blocks.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var method = new List<CodeInstruction>(instructions);
			int tryStart = -1, n = method.Count;
			var remove = new RangeInt(-1, 1);
			bool isIOBlock = false;
			for (int i = 0; i < n; i++) {
				var blocks = method[i].blocks;
				if (blocks != null)
					foreach (var block in blocks)
						switch (block.blockType) {
						case ExceptionBlockType.BeginExceptionBlock:
							if (tryStart < 0) {
								tryStart = i;
								isIOBlock = false;
							}
							break;
						case ExceptionBlockType.BeginCatchBlock:
							if (tryStart >= 0)
								isIOBlock = true;
							break;
						case ExceptionBlockType.EndExceptionBlock:
							if (tryStart >= 0 && isIOBlock && remove.start < 0) {
								remove.start = tryStart;
								remove.length = i - tryStart + 1;
#if DEBUG
								PUtil.LogDebug("Patched MainMenu.OnSpawn: {0:D} to {1:D}".F(
									tryStart, i));
#endif
								tryStart = -1;
								isIOBlock = false;
							}
							break;
						default:
							break;
						}
			}
			if (remove.start >= 0)
				method.RemoveRange(remove.start, remove.length);
			else
				PUtil.LogWarning("Unable to patch MainMenu.OnSpawn");
			return method;
		}
	}

	/// <summary>
	/// Applied to NameDisplayScreen to speed up checking name card positions.
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), nameof(NameDisplayScreen.LateUpdatePos))]
	public static class NameDisplayScreen_LateUpdatePos_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before LateUpdatePos runs.
		/// </summary>
		internal static bool Prefix(bool visibleToZoom, NameDisplayScreen __instance) {
			var inst = CameraController.Instance;
			if (inst != null) {
				GridArea area = inst.VisibleArea.CurrentArea;
				var entries = __instance.entries;
				var followTarget = inst.followTarget;
				int n = entries.Count;
				Vector3 pos;
				for (int i = 0; i < n; i++) {
					var entry = entries[i];
					var go = entry.world_go;
					var dg = entry.display_go;
					var animController = entry.world_go_anim_controller;
					if (go != null && dg != null) {
						// Merely fetching the position appears to take almost 1 us?
						var transform = go.transform;
						bool active = dg.activeSelf;
						if (visibleToZoom && area.Contains(pos = transform.position)) {
							// Visible
							if (followTarget == transform)
								pos = inst.followTargetPos;
							else if (animController != null)
								pos = animController.GetWorldPivot();
							entry.display_go_rect.anchoredPosition = __instance.
								worldSpace ? pos : __instance.WorldToScreen(pos);
							if (!active)
								dg.SetActive(true);
						} else if (active)
							// Invisible
							dg.SetActive(false);
					}
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to NameDisplayScreen to speed up marking name cards dirty.
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), nameof(NameDisplayScreen.LateUpdatePart2))]
	public static class NameDisplayScreen_LateUpdatePart2_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before LateUpdatePart2 runs.
		/// </summary>
		internal static bool Prefix(NameDisplayScreen __instance) {
			var camera = Camera.main;
			if (camera == null || camera.orthographicSize < __instance.HideDistance) {
				var entries = __instance.entries;
				int n = entries.Count;
				for (int i = 0; i < n; i++) {
					var go = entries[i].bars_go;
					if (go != null) {
						var colliders = __instance.workingList;
						// Mark the colliders in the bars list dirty only if visible
						go.GetComponentsInChildren(false, colliders);
						int c = colliders.Count;
						for (int j = 0; j < c; j++)
							colliders[j].MarkDirty(false);
					}
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to NameDisplayScreen to destroy the thought prefabs if conversations are
	/// turned off.
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), nameof(NameDisplayScreen.RegisterComponent))]
	public static class NameDisplayScreen_RegisterComponent_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.NoConversations || options.MiscOpts;
		}

		/// <summary>
		/// Applied before RegisterComponent runs.
		/// </summary>
		internal static bool Prefix(object component) {
			var options = FastTrackOptions.Instance;
			// GameplayEventMonitor is for the unused event/dream framework :(
			return !(component is ThoughtGraph.Instance && options.NoConversations) &&
				!(component is GameplayEventMonitor.Instance && options.MiscOpts);
		}
	}

	/// <summary>
	/// Applied to NotificationAnimator to turn off the bouncing effect.
	/// </summary>
	[HarmonyPatch(typeof(NotificationAnimator), nameof(NotificationAnimator.Begin))]
	public static class NotificationAnimator_Begin_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// Applied before Begin runs.
		/// </summary>
		internal static bool Prefix(NotificationAnimator __instance, ref bool ___animating,
				ref LayoutElement ___layoutElement) {
			var le = __instance.GetComponent<LayoutElement>();
			if (le != null)
				le.minWidth = 0.0f;
			___layoutElement = le;
			___animating = false;
			return false;
		}
	}

	/// <summary>
	/// Applied to TechItems to remove a duplicate Add call.
	/// </summary>
	[HarmonyPatch(typeof(Database.TechItems), nameof(Database.TechItems.AddTechItem))]
	public static class TechItems_AddTechItem_Patch {
		/// <summary>
		/// Transpiles AddTechItem to remove an Add call that duplicates every item, as it was
		/// already added to the constructor
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = typeof(ResourceSet<TechItem>).GetMethodSafe(nameof(
				ResourceSet<TechItem>.Add), false, typeof(TechItem));
			foreach (var instr in instructions) {
				if (target != null && instr.Is(OpCodes.Callvirt, target)) {
					// Original method was 1 arg to 1 arg and result was ignored, so just rip
					// it out
					instr.opcode = OpCodes.Nop;
					instr.operand = null;
#if DEBUG
					PUtil.LogDebug("Patched TechItems.AddTechItem");
#endif
				}
				yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to TrackerTool to reduce the update rate from 50 trackers/frame to 10/frame.
	/// </summary>
	[HarmonyPatch(typeof(TrackerTool), nameof(TrackerTool.OnSpawn))]
	public static class TrackerTool_OnSpawn_Patch {
		/// <summary>
		/// The number of trackers to update every rendered frame (disabled while paused).
		/// </summary>
		private const int UPDATES_PER_FRAME = 10;

		internal static bool Prepare() => FastTrackOptions.Instance.ReduceColonyTracking;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(ref int ___numUpdatesPerFrame) {
			___numUpdatesPerFrame = UPDATES_PER_FRAME;
		}
	}

	/// <summary>
	/// Applied to WorldInventory to fix a bug where Update does not run properly on the first
	/// run due to a misplaced "break".
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.Update))]
	public static class WorldInventory_UpdateTweak_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.MiscOpts && !options.ParallelInventory;
		}

		/// <summary>
		/// Transpiles Update to bypass the "break" if firstUpdate is true. Only matters on
		/// the first frame.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var markerField = typeof(WorldInventory).GetFieldSafe(nameof(WorldInventory.
				accessibleUpdateIndex), false);
			var updateField = typeof(WorldInventory).GetFieldSafe(nameof(WorldInventory.
				firstUpdate), false);
			var getSCS = typeof(SpeedControlScreen).GetPropertySafe<float>(nameof(
				SpeedControlScreen.Instance), true)?.GetGetMethod(true);
			var isPaused = typeof(SpeedControlScreen).GetMethodSafe(nameof(SpeedControlScreen.
				IsPaused), false);
			bool storeField = false, done = false;
			var end = generator.DefineLabel();
			if (getSCS != null && isPaused != null) {
				// Exit the method if the speed screen reports paused
				yield return new CodeInstruction(OpCodes.Call, getSCS);
				yield return new CodeInstruction(OpCodes.Callvirt, isPaused);
				yield return new CodeInstruction(OpCodes.Brtrue, end);
			}
			foreach (var instr in instructions) {
				var opcode = instr.opcode;
				if (instr.opcode == OpCodes.Ret) {
					// Label the end of the method
					var labels = instr.labels;
					if (labels == null)
						instr.labels = labels = new List<Label>(4);
					labels.Add(end);
				} else if (opcode == OpCodes.Stfld && markerField != null && instr.
						OperandIs(markerField))
					// Find the store to update the index field
					storeField = true;
				else if (!done && storeField && updateField != null && (opcode == OpCodes.Br ||
						opcode == OpCodes.Br_S)) {
					// Found the branch, need to replace it with a brtrue and add a
					// condition; load this.firstUpdate
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldfld, updateField);
					instr.opcode = OpCodes.Brfalse_S;
#if DEBUG
					PUtil.LogDebug("Patched WorldInventory.Update");
#endif
					done = true;
				}
				yield return instr;
			}
			if (!done)
				PUtil.LogWarning("Unable to patch WorldInventory.Update!");
		}
	}
}
