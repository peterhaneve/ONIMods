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
using System.Collections.Generic;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to DecorProvider to reduce the effect of the Tropical Pacu bug by instead of
	/// triggering a full room rebuild, just refreshing the room constraints.
	/// 
	/// If Decor Reimagined is installed, it will override the auto patch, the conditional one
	/// will be used instead.
	/// </summary>
	public static class DecorProviderRefreshFix {
		/// <summary>
		/// Stores the rooms that are pending an update.
		/// </summary>
		private static readonly ISet<int> ROOMS_PENDING = new HashSet<int>();

		/// <summary>
		/// Attempts to also patch the Decor Reimagined implementation of DecorProvider.
		/// Refresh.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void ApplyPatch(Harmony harmony) {
			var patchMethod = new HarmonyMethod(typeof(DecorProviderRefreshFix), nameof(
				TranspileRefresh));
			var targetMethod = PPatchTools.GetTypeSafe(
				"ReimaginationTeam.DecorRework.DecorSplatNew", "DecorReimagined")?.
				GetMethodSafe("RefreshDecor", false, PPatchTools.AnyArguments);
			if (targetMethod != null) {
				PUtil.LogDebug("Patching Decor Reimagined for DecorProvider.RefreshDecor");
				harmony.Patch(targetMethod, transpiler: patchMethod);
			}
			PUtil.LogDebug("Patching DecorProvider.Refresh");
			harmony.Patch(typeof(DecorProvider).GetMethodSafe(nameof(DecorProvider.Refresh),
				false, PPatchTools.AnyArguments), transpiler: patchMethod);
			if (!FastTrackOptions.Instance.BackgroundRoomRebuild)
				harmony.Patch(typeof(RoomProber), nameof(RoomProber.Sim1000ms), prefix:
					new HarmonyMethod(typeof(DecorProviderRefreshFix),
					nameof(PrefixRoomProbe)));
			ROOMS_PENDING.Clear();
		}

		/// <summary>
		/// Retriggers the conditions only when rooms would be rebuilt normally.
		/// </summary>
		[HarmonyPriority(Priority.HigherThanNormal)]
		private static void PrefixRoomProbe(RoomProber __instance) {
			foreach (int cell in ROOMS_PENDING) {
				var cavity = __instance.GetCavityForCell(cell);
				if (cavity != null)
					__instance.UpdateRoom(cavity);
				else
					__instance.SolidChangedEvent(cell, true);
			}
			ROOMS_PENDING.Clear();
		}

		/// <summary>
		/// Instead of triggering a full solid change of the room, merely retrigger the
		/// conditions.
		/// </summary>
		/// <param name="prober">The current room prober.</param>
		/// <param name="cell">The cell of the room that will be updated.</param>
		private static void SolidNotChangedEvent(RoomProber prober, int cell, bool _) {
			if (prober != null)
				ROOMS_PENDING.Add(cell);
		}

		/// <summary>
		/// Transpiles Refresh to change a solid change event into a condition retrigger.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		internal static TranspiledMethod TranspileRefresh(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(RoomProber).
				GetMethodSafe(nameof(RoomProber.SolidChangedEvent), false, typeof(int),
				typeof(bool)), typeof(DecorProviderRefreshFix).GetMethodSafe(nameof(
				SolidNotChangedEvent), true, typeof(RoomProber), typeof(int), typeof(bool)));
		}

		/// <summary>
		/// Triggers all queued room updates caused by Decor providers.
		/// </summary>
		internal static void TriggerUpdates() {
			var inst = BackgroundRoomProber.Instance;
			foreach (int cell in ROOMS_PENDING) {
				var cavity = inst.GetCavityForCell(cell);
				if (cavity != null)
					inst.UpdateRoom(cavity);
				else
					inst.QueueSolidChange(cell);
			}
			ROOMS_PENDING.Clear();
		}
	}
}
