/*
 * Copyright 2025 Peter Han
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
using System.Runtime.CompilerServices;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to Light2D to add SlowLightSymbolTracker when necessary.
	/// </summary>
	[HarmonyPatch(typeof(Light2D), nameof(Light2D.OnSpawn))]
	public static class Light2D_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(Light2D __instance) {
			var go = __instance.gameObject;
			if (go.TryGetComponent(out LightSymbolTracker _))
				go.AddOrGet<SlowLightSymbolTracker>();
		}
	}

	/// <summary>
	/// Tracks the number of lighting rays/halos in any particular tile, and stops rendering
	/// more once it exceeds a threshold.
	/// </summary>
	internal static class LightBufferManager {
		/// <summary>
		/// The maximum number of light rays rendered per tile.
		/// </summary>
		private const int MAX_RENDERED_PER_TILE = 4;

		/// <summary>
		/// The number of rays rendered in each cell.
		/// </summary>
		private static IDictionary<int, int> raysInCell;

		/// <summary>
		/// Cleans up the array to avoid leaking memory.
		/// </summary>
		internal static void Cleanup() {
			raysInCell.Clear();
		}

		/// <summary>
		/// Initializes the light buffer manager to the current grid size if necessary, and
		/// otherwise clears all light sources to zero.
		/// </summary>
		internal static void Init() {
			if (raysInCell == null)
				raysInCell = new Dictionary<int, int>(128);
			else
				raysInCell.Clear();
		}

		/// <summary>
		/// Checks to see if a light source ray should be rendered.
		/// </summary>
		/// <param name="light">The light to check.</param>
		/// <returns>true to render the light source, or false to hide it.</returns>
		internal static bool ShouldRender(Behaviour light) {
			bool render = false;
			// Was already null checked
			int cell = Grid.PosToCell(light.transform.position);
			if (Grid.IsValidCell(cell) && light.enabled) {
				if (!raysInCell.TryGetValue(cell, out int count))
					count = 0;
				raysInCell[cell] = count + 1;
				render = count < MAX_RENDERED_PER_TILE;
			}
			return render;
		}
	}

	/// <summary>
	/// Applied to LightBuffer to patch in checks to turn down the lights on big Shine Bug
	/// farms.
	/// </summary>
	[HarmonyPatch(typeof(LightBuffer), nameof(LightBuffer.LateUpdate))]
	public static class LightBuffer_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.UnstackLights;

		/// <summary>
		/// Transpiles LateUpdate to insert the ShouldRender check.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var init = typeof(LightBufferManager).GetMethodSafe(nameof(LightBufferManager.
				Init), true);
			var target = typeof(Behaviour).GetPropertySafe<bool>(nameof(Behaviour.enabled),
				false)?.GetGetMethod(true);
			var replacement = typeof(LightBufferManager).GetMethodSafe(nameof(
				LightBufferManager.ShouldRender), true, typeof(Behaviour));
			if (init != null)
				yield return new CodeInstruction(OpCodes.Call, init);
			else
				PUtil.LogWarning("Unable to find LightBufferManager.Init!");
			if (target == null || replacement == null) {
				PUtil.LogWarning("Unable to find Behaviour.enabled!");
				foreach (var instr in instructions)
					yield return instr;
			} else {
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Callvirt, target)) {
						instr.operand = replacement;
#if DEBUG
						PUtil.LogDebug("Patched LightBuffer.LateUpdate");
#endif
					}
					yield return instr;
				}
			}
		}
	}

	/// <summary>
	/// Applied to LightSymbolTracker to reduce its update frequency to 200ms.
	/// </summary>
	[HarmonyPatch(typeof(LightSymbolTracker), nameof(LightSymbolTracker.RenderEveryTick))]
	public static class LightSymbolTrackerRenderer {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}

		[HarmonyReversePatch]
		[HarmonyPatch(nameof(LightSymbolTracker.RenderEveryTick))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void RenderEveryTick(LightSymbolTracker instance, float dt) {
			_ = instance;
			_ = dt;
			// Dummy code to ensure no inlining
			while (System.DateTime.Now.Ticks > 0L)
				throw new NotImplementedException("Reverse patch stub");
		}
	}

	/// <summary>
	/// Only updates LightSymbolTracker every 200 ms realtime, not every frame.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class SlowLightSymbolTracker : KMonoBehaviour, IRender200ms {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpReq]
		private LightSymbolTracker tracker;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void Render200ms(float dt) {
			if (tracker != null)
				LightSymbolTrackerRenderer.RenderEveryTick(tracker, dt);
		}
	}
}
