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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Extension methods make life easier!
	/// </summary>
	public static class ExtensionMethods {
		/// <summary>
		/// Appends the time slice unit (like "/s") to the string buffer. Allocates less than
		/// a string concatenation.
		/// </summary>
		/// <param name="buffer">The string builder to append.</param>
		/// <param name="timeSlice">The time slice unit to use.</param>
		/// <returns>The string builder.</returns>
		public static StringBuilder AppendTimeSlice(this StringBuilder buffer,
				GameUtil.TimeSlice timeSlice) {
			switch (timeSlice) {
			case GameUtil.TimeSlice.PerSecond:
				buffer.Append(STRINGS.UI.UNITSUFFIXES.PERSECOND);
				break;
			case GameUtil.TimeSlice.PerCycle:
				buffer.Append(STRINGS.UI.UNITSUFFIXES.PERCYCLE);
				break;
			}
			return buffer;
		}

		/// <summary>
		/// Copies layout information to a fixed layout element. Useful for freezing a UI
		/// object.
		/// </summary>
		/// <param name="dest">The fixed layout component that will replace it.</param>
		/// <param name="src">The current layout component.</param>
		public static void CopyFrom(this LayoutElement dest, ILayoutElement src) {
			dest.flexibleHeight = src.flexibleHeight;
			dest.flexibleWidth = src.flexibleWidth;
			dest.preferredHeight = src.preferredHeight;
			dest.preferredWidth = src.preferredWidth;
			dest.minHeight = src.minHeight;
			dest.minWidth = src.minWidth;
		}

		/// <summary>
		/// Creates a GameObject to render meshes using a MeshRenderer.
		/// </summary>
		/// <param name="targetMesh">The mesh to be rendered.</param>
		/// <param name="name">The object's name.</param>
		/// <param name="layer">The layer on which the mesh will be rendered.</param>
		/// <returns>The game object to use for rendering.</returns>
		public static GameObject CreateMeshRenderer(this Mesh targetMesh, string name,
				int layer) {
			if (targetMesh == null)
				throw new ArgumentNullException(nameof(targetMesh));
			var go = new GameObject(name ?? "Mesh Renderer", typeof(MeshRenderer), typeof(
					MeshFilter)) {
				layer = layer
			};
			// Set up the mesh with the right material
			var renderer = go.GetComponent<MeshRenderer>();
			renderer.allowOcclusionWhenDynamic = false;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			renderer.receiveShadows = false;
			renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			// Set the mesh to render
			var filter = go.GetComponent<MeshFilter>();
			filter.sharedMesh = targetMesh;
			return go;
		}

		/// <summary>
		/// A non-mutating version of Navigator.GetNavigationCost that can be run on
		/// background threads.
		/// </summary>
		/// <param name="navigator">The navigator to calculate.</param>
		/// <param name="destination">The destination to find the cost.</param>
		/// <param name="cell">The workable's current cell.</param>
		/// <param name="cost">The location where the cost will be stored.</param>
		/// <returns>true if the table needs to be updated, or false otherwise.</returns>
		public static bool GetNavigationCostNU(this Navigator navigator, Workable destination,
				int cell, out int cost) {
			CellOffset[] offsets = null;
			bool update = false;
			var offsetTracker = destination.offsetTracker;
			if (offsetTracker != null && (offsets = offsetTracker.offsets) == null) {
				offsetTracker.UpdateOffsets(cell);
				offsets = offsetTracker.offsets;
				update = offsetTracker.previousCell != cell;
			}
			cost = (offsets == null) ? navigator.GetNavigationCost(cell) : navigator.
				GetNavigationCost(cell, offsets);
			return update;
		}

		/// <summary>
		/// Checks to see if the cell allows pipe visibility; shared by conduit culling code.
		/// </summary>
		/// <param name="cell">The cell to test.</param>
		/// <returns>true if a pipe could be seen in the cell (visible, not fully solid or
		/// transparent), or false otherwise</returns>
		public static bool IsVisibleCell(this int cell) {
			var element = Grid.Element[cell];
			return Grid.IsValidCell(cell) && Grid.IsVisible(cell) && (element == null ||
				!element.IsSolid || Grid.Transparent[cell]);
		}

		/// <summary>
		/// Profiles invocations of a particular method.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		/// <param name="type">The type containing the method to profile.</param>
		/// <param name="name">The method name to profile.</param>
		internal static void Profile(this Harmony instance, Type type, string name)
		{
#if DEBUG
			if (FastTrackOptions.Instance.Metrics) {
				var targetMethod = type.GetMethod(name, BindingFlags.Instance | BindingFlags.
					Static | PPatchTools.BASE_FLAGS);
				PUtil.LogDebug("Profiling method {0}.{1}".F(type.Name, name));
				var existingPatches = Harmony.GetPatchInfo(targetMethod);
				if (existingPatches != null) {
					TellMeWhy("Prefix", existingPatches.Prefixes);
					TellMeWhy("Postfix", existingPatches.Postfixes);
					TellMeWhy("Transpiler", existingPatches.Transpilers);
				}
				instance.Patch(targetMethod, new HarmonyMethod(typeof(ExtensionMethods),
					nameof(ProfilePrefix)), new HarmonyMethod(typeof(ExtensionMethods),
					nameof(ProfileSuffix)));
			}
#endif
		}

		/// <summary>
		/// Prefixes methods to profile to start a stopwatch.
		/// </summary>
		[HarmonyPriority(Priority.High)]
		private static void ProfilePrefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Postfixes methods to profile to log them.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		private static void ProfileSuffix(MethodBase __originalMethod, Stopwatch __state) {
			if (__state != null)
				Metrics.DebugMetrics.LogTracked(__originalMethod.DeclaringType.Name + "." +
					__originalMethod.Name, __state.ElapsedTicks);
		}

#if DEBUG
		/// <summary>
		/// Describes information about existing patches.
		/// </summary>
		/// <param name="heading">The heading to use for the patch.</param>
		/// <param name="patches">The patches for this category.</param>
		private static void TellMeWhy(string heading, IReadOnlyCollection<Patch> patches) {
			if (patches != null)
				foreach (var patch in patches) {
					var patchMethod = patch.PatchMethod;
					PUtil.LogDebug(" {3} from {0} ({1}.{2})".F(patch.owner, patchMethod.
						DeclaringType.Name, patchMethod.Name, heading));
				}
		}
#endif

		/// <summary>
		/// Gets the elapsed time in microseconds.
		/// </summary>
		/// <param name="ticks">The time elapsed in stopwatch ticks.</param>
		/// <returns>The elapsed time in microseconds.</returns>
		public static long TicksToUS(this long ticks) {
			return ticks * 1000000L / Stopwatch.Frequency;
		}

		/// <summary>
		/// Converts a float to a standard string like ONI would, but with less memory used.
		/// </summary>
		/// <param name="f">The value to format.</param>
		/// <returns>The value formatted like ONI wants it for display.</returns>
		public static string ToStandardString(this float f) {
			string result;
			float absF = Mathf.Abs(f);
			if (f == 0f)
				result = "0";
			else if (absF < 1f)
				result = f.ToString("#,##0.#");
			else if (absF < 10f)
				result = f.ToString("#,###.#");
			else
				result = f.ToString("N0");
			return result;
		}
	}
}
