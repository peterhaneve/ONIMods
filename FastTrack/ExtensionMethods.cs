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
using Ryu;
using System;
#if DEBUG
using System.Collections.Generic;
using System.Reflection;
#endif
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Extension methods make life easier!
	/// </summary>
	public static class ExtensionMethods {
		/// <summary>
		/// The shared stopwatch used to avoid allocations when timing handles.
		/// </summary>
		private static readonly Stopwatch WAIT_HANDLE_CLOCK = new Stopwatch();

		/// <summary>
		/// Appends two string builders with no intermediate ToString allocation.
		/// </summary>
		/// <param name="dest">The destination string.</param>
		/// <param name="src">The source string.</param>
		/// <returns>The modified destination string with src appended.</returns>
		public static StringBuilder Append(this StringBuilder dest, StringBuilder src) {
			int n = src.Length;
			dest.EnsureCapacity(dest.Length + n);
			for (int i = 0; i < n; i++)
				dest.Append(src[i]);
			return dest;
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
		/// <param name="shader">The material to use, or null to leave unassigned.</param>
		/// <returns>The game object to use for rendering.</returns>
		public static GameObject CreateMeshRenderer(this Mesh targetMesh, string name,
				int layer, Material shader = null) {
			if (targetMesh == null)
				throw new ArgumentNullException(nameof(targetMesh));
			var go = new GameObject(name ?? "Mesh Renderer", typeof(MeshRenderer), typeof(
					MeshFilter)) {
				layer = layer
			};
			// Set up the mesh with the right material
			if (go.TryGetComponent(out MeshRenderer renderer)) {
				renderer.allowOcclusionWhenDynamic = false;
				renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
				renderer.receiveShadows = false;
				renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				if (shader != null)
					renderer.material = shader;
			}
			// Set the mesh to render
			if (go.TryGetComponent(out MeshFilter filter))
				filter.sharedMesh = targetMesh;
			return go;
		}

		/// <summary>
		/// A faster version of string.Format with one string argument.
		/// </summary>
		/// <param name="str">The LocString to format.</param>
		/// <param name="value">The value to substitute for "{0}".</param>
		/// <returns>The formatted string.</returns>
		public static string Format(this LocString str, string value) {
			return str.text.Replace("{0}", value);
		}

		/// <summary>
		/// A faster version of string.Format with one string argument.
		/// </summary>
		/// <param name="str">The StringEntry to format.</param>
		/// <param name="value">The value to substitute for "{0}".</param>
		/// <returns>The formatted string.</returns>
		public static string Format(this StringEntry str, string value) {
			return str.String.Replace("{0}", value);
		}

		/// <summary>
		/// Checks to see if the cell allows pipe visibility; shared by conduit culling code.
		/// </summary>
		/// <param name="cell">The cell to test.</param>
		/// <returns>true if a pipe could be seen in the cell (visible, not fully solid or
		/// transparent), or false otherwise</returns>
		public static bool IsVisibleCell(this int cell) {
			Element element;
			return Grid.IsValidCell(cell) && Grid.IsVisible(cell) && ((element = Grid.
				Element[cell]) == null || !element.IsSolid || Grid.Transparent[cell]);
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

#if DEBUG
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
				Metrics.DebugMetrics.LogTracked(__originalMethod.DeclaringType?.Name + "." +
					__originalMethod.Name, __state.ElapsedTicks);
		}
#endif

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
						DeclaringType?.Name, patchMethod.Name, heading));
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
		/// Converts a float to a fixed decimal point format with exactly the specified number
		/// of decimals.
		/// </summary>
		/// <param name="f">The value to format.</param>
		/// <param name="result">The location where the formatted value will be stored.</param>
		/// <param name="precision">The exact number of decimal places.</param>
		public static void ToRyuHardString(this float f, StringBuilder result, int precision) {
			RyuFormat.ToString(result, (double)f, precision, RyuFormatOptions.FixedMode);
		}

		/// <summary>
		/// Converts a float to a fixed decimal point format with up to the specified number
		/// of optional decimals.
		/// </summary>
		/// <param name="f">The value to format.</param>
		/// <param name="result">The location where the formatted value will be stored.</param>
		/// <param name="precision">The maximum number of decimal places.</param>
		public static void ToRyuSoftString(this float f, StringBuilder result, int precision) {
			RyuFormat.ToString(result, (double)f, precision, RyuFormatOptions.FixedMode |
				RyuFormatOptions.SoftPrecision);
		}

		/// <summary>
		/// Converts a float to a standard string like ONI would, but with less memory used.
		/// </summary>
		/// <param name="f">The value to format.</param>
		/// <param name="result">The location where the formatted value will be stored.</param>
		public static void ToStandardString(this float f, StringBuilder result) {
			float absF = Mathf.Abs(f);
			int precision = (absF < 10f) ? 1 : 0;
			RyuFormat.ToString(result, (double)f, precision, RyuFormatOptions.FixedMode |
				RyuFormatOptions.SoftPrecision | RyuFormatOptions.ThousandsSeparators);
		}

		/// <summary>
		/// Waits for a handle to be signaled. Logs a warning if it waits for more than the
		/// specified duration.
		/// 
		/// This method is not re-entrant as it uses a shared stopwatch. Only run it on the
		/// main thread.
		/// </summary>
		/// <param name="handle">The handle to wait for.</param>
		/// <param name="timeout">The maximum time to wait, or -1 to wait forever.</param>
		/// <param name="warning">The amount of time to wait before emitting a warning.</param>
		/// <returns>true if the handle was signaled, or false if it timed out.</returns>
		public static bool WaitAndMeasure(this WaitHandle handle, int timeout = Timeout.
				Infinite, int warning = 30) {
			var now = WAIT_HANDLE_CLOCK;
			now.Restart();
			bool signaled = handle.WaitOne(timeout);
			now.Stop();
			long t = now.ElapsedMilliseconds;
			if (signaled && t > warning && FastTrackMod.GameRunning)
				PUtil.LogWarning("Waited {0:D} ms for an async join (max {1:D} ms)".F(t,
					warning));
			return signaled;
		}
	}
}
