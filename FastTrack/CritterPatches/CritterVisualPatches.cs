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

using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.FastTrack.CritterPatches {
	/// <summary>
	/// Applied to SegmentedCreature to be smarter about updating the body position if the
	/// head position did not change.
	/// </summary>
	[HarmonyPatch]
	public static class SegmentedCreature_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		internal static IEnumerable<MethodBase> TargetMethods() {
			var targetType = typeof(SegmentedCreature);
			return new List<MethodBase>(3) {
				targetType.GetMethodSafe(nameof(SegmentedCreature.UpdateFreeMovement), false,
					typeof(SegmentedCreature.Instance), typeof(float)),
				targetType.GetMethodSafe(nameof(SegmentedCreature.UpdateRetractedLoop), false,
					typeof(SegmentedCreature.Instance), typeof(float)),
				targetType.GetMethodSafe(nameof(SegmentedCreature.UpdateRetractedPre), false,
					typeof(SegmentedCreature.Instance), typeof(float))
			};
		}

		/// <summary>
		/// Applied before UpdateFreeMovement or UpdateRetractedLoop runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(SegmentedCreature.Instance smi) {
			var head = smi.GetHeadSegmentNode().Value;
			return head.Position != smi.previousHeadPosition;
		}
	}
}
