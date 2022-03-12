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
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Updates LoopingSoundManager and AmbienceManager on alternating frames at about 10 FPS.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class SoundUpdater : KMonoBehaviour, IRenderEveryTick {
		/// <summary>
		/// The rate at which sounds will be updated.
		/// </summary>
		private const double UPDATE_RATE = 1.0 / 10.0;

		/// <summary>
		/// Whether the ambience manager's update should run the next time.
		/// </summary>
		private static bool runAmbience = true;

		/// <summary>
		/// The next unscaled time that sounds will be updated.
		/// </summary>
		private double nextSoundUpdate;

		protected override void OnSpawn() {
			base.OnSpawn();
			nextSoundUpdate = 0.0;
			runAmbience = false;
		}

		public void RenderEveryTick(float dt) {
			double now = Time.unscaledTimeAsDouble;
			if (now >= nextSoundUpdate) {
				var lsm = LoopingSoundManager.Get();
				if (lsm != null)
					LoopingSoundManagerUpdater.RenderEveryTick(lsm, dt);
				StartCoroutine(RunAmbienceNextFrame());
				nextSoundUpdate = now + UPDATE_RATE;
			}
		}

		/// <summary>
		/// A coroutine that waits one frame, then allows AmbienceManager to run.
		/// </summary>
		private System.Collections.IEnumerator RunAmbienceNextFrame() {
			yield return null;
			runAmbience = true;
			yield break;
		}

		/// <summary>
		/// Applied to AmbienceManager to reduce its updates to the same speed as sound
		/// updates.
		/// </summary>
		[HarmonyPatch(typeof(AmbienceManager), "LateUpdate")]
		public static class AmbienceManagerUpdater {
			internal static bool Prepare() => FastTrackOptions.Instance.ReduceSoundUpdates;

			/// <summary>
			/// Applied before LateUpdate runs.
			/// </summary>
			internal static bool Prefix() {
				bool shouldRunAmbience = runAmbience;
				runAmbience = false;
				return shouldRunAmbience;
			}
		}

		/// <summary>
		/// Applied to LoopingSoundManager to reduce sound updates to 5 FPS.
		/// </summary>
		[HarmonyPatch(typeof(LoopingSoundManager), nameof(LoopingSoundManager.
			RenderEveryTick))]
		public static class LoopingSoundManagerUpdater {
			internal static bool Prepare() => FastTrackOptions.Instance.ReduceSoundUpdates;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix() {
				return false;
			}

			[HarmonyReversePatch(HarmonyReversePatchType.Original)]
			[HarmonyPatch(nameof(LoopingSoundManager.RenderEveryTick))]
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static void RenderEveryTick(LoopingSoundManager instance, float dt) {
				_ = instance;
				_ = dt;
				// Dummy code to ensure no inlining
				while (System.DateTime.Now.Ticks > 0L)
					throw new NotImplementedException("Reverse patch stub");
			}
		}
	}

	/// <summary>
	/// Applied to NotificationScreen to suppress sounds queued very early in the load.
	/// sequence.
	/// </summary>
	[HarmonyPatch(typeof(NotificationScreen), "PlayDingSound")]
	public static class NotificationScreen_PlayDingSound_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceSoundUpdates;

		/// <summary>
		/// Applied before PlayDingSound runs.
		/// </summary>
		internal static bool Prefix(NotificationScreen __instance, Notification notification) {
			// No const for that sound name
			return notification == null || __instance.GetNotificationSound(notification.
				Type) != "Notification" || FastTrackPatches.GameRunning;
		}
	}
}
