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
			var getTimeScale = typeof(Time).GetPropertySafe<float>(nameof(Time.timeScale),
				true)?.GetGetMethod();
			bool storeField = false, done = false;
			var end = generator.DefineLabel();
			if (getTimeScale != null) {
				// Exit the method if timeScale is 0.0f (paused)
				yield return new CodeInstruction(OpCodes.Call, getTimeScale);
				yield return new CodeInstruction(OpCodes.Ldc_R4, 0.0f);
				yield return new CodeInstruction(OpCodes.Ceq);
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
