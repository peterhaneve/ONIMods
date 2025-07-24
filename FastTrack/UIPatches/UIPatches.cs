/*
 * Copyright 2024 Peter Han
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

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to KChildFitter to add an updater to fit it only on layout changes.
	/// </summary>
	[HarmonyPatch(typeof(KChildFitter), nameof(KChildFitter.Awake))]
	public static class KChildFitter_Awake_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after Awake runs.
		/// </summary>
		internal static void Postfix(KChildFitter __instance) {
			__instance.gameObject.AddOrGet<KChildFitterUpdater>();
		}
	}

	/// <summary>
	/// Applied to KChildFitter to turn off an expensive fitter method that runs every frame!
	/// </summary>
	[HarmonyPatch(typeof(KChildFitter), nameof(KChildFitter.LateUpdate))]
	public static class KChildFitter_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// A layout element that triggers child fitting only if layout has actually changed.
	/// </summary>
	internal sealed class KChildFitterUpdater : KMonoBehaviour, ILayoutElement {
		public float minWidth => -1.0f;

		public float preferredWidth => -1.0f;

		public float flexibleWidth => -1.0f;

		public float minHeight => -1.0f;

		public float preferredHeight => -1.0f;

		public float flexibleHeight => -1.0f;

		public int layoutPriority => int.MinValue;

#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private KChildFitter fitter;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void CalculateLayoutInputHorizontal() {
			fitter.FitSize();
		}

		public void CalculateLayoutInputVertical() { }
	}

	/// <summary>
	/// Applied to MainMenu to get rid of the 15 MB file write and speed up boot!
	/// </summary>
	[HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OnSpawn))]
	public static class MainMenu_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

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
	/// Applied to NameDisplayScreen to destroy the thought prefabs if conversations are
	/// turned off.
	/// </summary>
	[HarmonyPatch(typeof(NameDisplayScreen), nameof(NameDisplayScreen.RegisterComponent))]
	public static class NameDisplayScreen_RegisterComponent_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before RegisterComponent runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(object component) {
			var options = FastTrackOptions.Instance;
			return !(component is ThoughtGraph.Instance && options.NoConversations);
		}
	}

	/// <summary>
	/// Applied to TechItems to remove a duplicate Add call.
	/// </summary>
	[HarmonyPatch(typeof(Database.TechItems), nameof(Database.TechItems.AddTechItem),
		typeof(string), typeof(string), typeof(string), typeof(Func<string, bool, Sprite>),
		typeof(string[]), typeof(string[]), typeof(bool))]
	public static class TechItems_AddTechItem_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Transpiles AddTechItem to remove an Add call that duplicates every item, as it was
		/// already added to the constructor.
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
