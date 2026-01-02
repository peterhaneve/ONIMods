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
using System;
using System.Runtime.CompilerServices;

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
