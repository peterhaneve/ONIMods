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
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
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

	/// <summary>
	/// Collects all patches applied to the ColonyDiagnosticScreen and its inner classes.
	/// </summary>
	public static class ColonyDiagnosticScreenPatches {
		/// <summary>
		/// Resolves to the private type ColonyDiagnosticScreen.DiagnosticRow.
		/// </summary>
		internal static readonly Type DIAGNOSTIC_ROW = typeof(ColonyDiagnosticScreen).
			GetNestedType("DiagnosticRow", PPatchTools.BASE_FLAGS | BindingFlags.Instance);

		/// <summary>
		/// Applied to ColonyDiagnosticScreen.DiagnosticRow to suppress SparkChart updates if
		/// not visible.
		/// </summary>
		[HarmonyPatch]
		internal static class Sim4000ms_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// DiagnosticRow is a private class, so calculate the target with reflection.
			/// </summary>
			internal static MethodBase TargetMethod() {
				if (DIAGNOSTIC_ROW == null)
					PUtil.LogWarning("Unable to resolve target: ColonyDiagnosticScreen.DiagnosticRow");
				return DIAGNOSTIC_ROW?.GetMethodSafe(nameof(ISim4000ms.Sim4000ms), false,
					typeof(float));
			}

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
		[HarmonyPatch]
		internal static class TriggerVisualNotification_Patch {
			private static readonly MethodBase RESOLVE_NOTIFICATION = DIAGNOSTIC_ROW?.
				GetMethodSafe("ResolveNotificationRoutine", false);

			internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

			/// <summary>
			/// DiagnosticRow is a private class, so calculate the target with reflection.
			/// </summary>
			internal static MethodBase TargetMethod() {
				return DIAGNOSTIC_ROW?.GetMethodSafe("TriggerVisualNotification", false);
			}

			/// <summary>
			/// A replacement coroutine that waits 3 seconds and then resolves it with no bounce.
			/// </summary>
			private static System.Collections.IEnumerator NoMoveRoutine(object row) {
				// Wait for 3 seconds unscaled
				yield return new UnityEngine.WaitForSeconds(3.0f);
				try {
					// Ignore exception if the notification cannot be resolved
					RESOLVE_NOTIFICATION?.Invoke(row, new object[] { });
				} catch (Exception) { }
				yield break;
			}

			/// <summary>
			/// Transpiles TriggerVisualNotification to remove calls to the bouncy coroutine.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				return PPatchTools.ReplaceMethodCall(instructions, DIAGNOSTIC_ROW?.
					GetMethodSafe("VisualNotificationRoutine", false), typeof(
					TriggerVisualNotification_Patch).GetMethodSafe(nameof(NoMoveRoutine),
					true, typeof(object)));
			}
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
	/// 11388
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), nameof(NameDisplayScreen.AddNewEntry))]
	public static class NameDisplayScreen_AddNewEntry_Patch {
		internal static bool Prepare() => false;

		/// <summary>
		/// Locks down the layout and removes unnecessary layout components from name displays,
		/// to reduce time spent laying out the components.
		/// </summary>
		/// <param name="displayObj">The object displaying the name information.</param>
		private static System.Collections.IEnumerator LockLayoutLater(GameObject displayObj) {
			yield return new WaitForEndOfFrame();
			if (displayObj != null) {
				// Outer object is a vertical layout group of the name and the bars
				// Bars have a vertical layout group of the bar members
				displayObj.AddOrGet<Canvas>();
			}
		}

		/// <summary>
		/// Applied after AddNewEntry runs.
		/// </summary>
		internal static void Postfix(IList<NameDisplayScreen.Entry> ___entries) {
			int n;
			if (___entries != null && (n = ___entries.Count) > 0) {
				// Wait a frame for it to lay out
				var added = ___entries[n - 1];
				var refs = added?.refs;
				if (refs != null) {
					refs.StartCoroutine(LockLayoutLater(added.display_go));
				}
			}
		}
	}

	/// <summary>
	/// Applied to NameDisplayScreen to slightly speed up visibility checking. Every bit
	/// counts!
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), "LateUpdatePos")]
	public static class NameDisplayScreen_LateUpdatePos_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Replaces the IsVisiblePos call with a check to the local variable Contains which
		/// is way faster.
		/// Searches up to six instructions back for the arguments and fixes them up too.
		/// </summary>
		/// <param name="allInstr">The method IL to patch.</param>
		/// <param name="index">The instruction index to modify.</param>
		/// <param name="bounds">The local variable containing the area.</param>
		private static bool FixupCall(IList<CodeInstruction> allInstr, int index,
				LocalBuilder bounds, MethodBase getInstance) {
			int maxDepth = Math.Max(1, index - 6);
			bool patched = false;
			var contains = typeof(GridArea).GetMethodSafe(nameof(GridArea.Contains), false,
				typeof(Vector3));
			var current = allInstr[index];
			if (contains != null) {
				// Swap the previous GetInstance to ldloca.s
				for (int j = index - 1; j >= maxDepth && !patched; j--) {
					var instr = allInstr[j];
					if (instr.Is(OpCodes.Call, getInstance)) {
						instr.opcode = OpCodes.Ldloca_S;
						instr.operand = (byte)bounds.LocalIndex;
						patched = true;
					}
				}
				if (patched) {
					// Swap the call to Contains
					current.opcode = OpCodes.Call;
					current.operand = contains;
#if DEBUG
					PUtil.LogDebug("Patched NameDisplayScreen.LateUpdatePos");
#endif
				}
			}
			return patched;
		}

		/// <summary>
		/// Transpiles LateUpdatePos to replace CameraController.Instance.IsVisiblePos with
		/// a comparison that avoids recalculating the bounds again and again.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var curArea = typeof(GridVisibleArea).GetPropertySafe<GridArea>(nameof(
				GridVisibleArea.CurrentArea), false)?.GetGetMethod();
			var target = typeof(CameraController).GetMethodSafe(nameof(CameraController.
				IsVisiblePos), false, typeof(Vector3));
			var getInstance = typeof(CameraController).GetPropertySafe<CameraController>(
				nameof(CameraController.Instance), true)?.GetGetMethod();
			var areaField = typeof(CameraController).GetFieldSafe(nameof(CameraController.
				VisibleArea), false);
			var allInstr = new List<CodeInstruction>(instructions);
			bool patched = false;
			// The visible area test only really matters when zoomed in
			if (curArea != null && target != null && getInstance != null && areaField != null)
			{
				var bounds = generator.DeclareLocal(typeof(GridArea));
				int n = allInstr.Count;
				for (int i = 0; i < n; i++) {
					var instr = allInstr[i];
					if (!patched && instr.Is(OpCodes.Callvirt, target))
						patched = FixupCall(allInstr, i, bounds, getInstance);
				}
				if (patched)
					// Get the area and save it
					allInstr.InsertRange(0, new CodeInstruction[] {
						new CodeInstruction(OpCodes.Call, getInstance),
						new CodeInstruction(OpCodes.Ldfld, areaField),
						new CodeInstruction(OpCodes.Call, curArea),
						new CodeInstruction(OpCodes.Stloc_S, (byte)bounds.LocalIndex)
					});
			}
			if (!patched)
				PUtil.LogWarning("Unable to patch NameDisplayScreen.LateUpdatePos");
			return allInstr;
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
	/// Applied to WorldInventory to fix a bug where Update does not run properly on the first
	/// run due to a misplaced "break".
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), "Update")]
	public static class WorldInventory_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Transpiles Update to bypass the "break" if firstUpdate is true. Only matters on
		/// the first frame.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var markerField = typeof(WorldInventory).GetFieldSafe("accessibleUpdateIndex",
				false);
			var updateField = typeof(WorldInventory).GetFieldSafe("firstUpdate", false);
			var getSCS = typeof(SpeedControlScreen).GetPropertySafe<float>(nameof(
				SpeedControlScreen.Instance), true)?.GetGetMethod();
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
