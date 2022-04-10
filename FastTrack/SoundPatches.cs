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
using System.Runtime.CompilerServices;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Updates LoopingSoundManager and AmbienceManager on alternating frames at about 5 FPS.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class SoundUpdater : KMonoBehaviour, IRender200ms {
		/// <summary>
		/// The player preference value for the ambience volume.
		/// </summary>
		public static readonly string VOLUME_AMBIENCE = "Volume_" + STRINGS.UI.FRONTEND.
			AUDIO_OPTIONS_SCREEN.AUDIO_BUS_AMBIENCE;

		/// <summary>
		/// The player preference value for the master volume.
		/// </summary>
		public static readonly string VOLUME_MASTER = "Volume_" + STRINGS.UI.FRONTEND.
			AUDIO_OPTIONS_SCREEN.AUDIO_BUS_MASTER;

		/// <summary>
		/// The player preference value for the music volume.
		/// </summary>
		public static readonly string VOLUME_MUSIC = "Volume_" + STRINGS.UI.FRONTEND.
			AUDIO_OPTIONS_SCREEN.AUDIO_BUS_MUSIC;

		/// <summary>
		/// The player preference value for the SFX volume.
		/// </summary>
		public static readonly string VOLUME_SFX = "Volume_" + STRINGS.UI.FRONTEND.
			AUDIO_OPTIONS_SCREEN.AUDIO_BUS_MASTER;

		/// <summary>
		/// The player preference value for the UI volume.
		/// </summary>
		public static readonly string VOLUME_UI = "Volume_" + STRINGS.UI.FRONTEND.
			AUDIO_OPTIONS_SCREEN.AUDIO_BUS_UI;

		/// <summary>
		/// Whether the ambience manager's update should run the next time.
		/// </summary>
		private static bool runAmbience = !FastTrackOptions.Instance.DisableSound;

		/// <summary>
		/// Whether the mix manager's update should run the next time.
		/// </summary>
		private static bool runMix = !FastTrackOptions.Instance.DisableSound;

		public override void OnSpawn() {
			base.OnSpawn();
			runAmbience = false;
			runMix = false;
		}

		public void Render200ms(float dt) {
			if (KPlayerPrefs.GetFloat(VOLUME_MASTER, 1.0f) > 0.0f && !FastTrackOptions.
					Instance.DisableSound) {
#if false
				var lsm = LoopingSoundManager.Get();
				if (lsm != null)
					LoopingSoundManagerUpdater.RenderEveryTick(lsm, dt);
#endif
				if (KPlayerPrefs.GetFloat(VOLUME_AMBIENCE, 1.0f) > 0.0f) {
					StartCoroutine(RunAmbienceNextFrame());
					runMix = true;
				} else
					runMix = false;
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
		[HarmonyPatch(typeof(AmbienceManager), nameof(AmbienceManager.LateUpdate))]
		public static class AmbienceManagerUpdater {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.DisableSound || options.ReduceSoundUpdates;
			}

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
		/// Applied to LoopingSoundManager to reduce sound updates to every other frame.
		/// </summary>
		[HarmonyPatch(typeof(LoopingSoundManager), nameof(LoopingSoundManager.
			RenderEveryTick))]
		public static class LoopingSoundManagerUpdater {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.DisableSound || options.ReduceSoundUpdates;
			}

			/// <summary>
			/// Whether looping sounds should be updated this frame.
			/// </summary>
			private static bool runLooping;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix() {
				bool shouldRunLooping = runLooping;
				runLooping = !runLooping;
				return shouldRunLooping;
			}

#if false
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
#endif
		}

		/// <summary>
		/// Applied to MixManager to reduce sound updates to 5 FPS.
		/// </summary>
		[HarmonyPatch(typeof(MixManager), nameof(MixManager.Update))]
		public static class MixManagerUpdater {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.DisableSound || options.ReduceSoundUpdates;
			}

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static bool Prefix() {
				bool shouldRunMix = runMix;
				runMix = false;
				return shouldRunMix;
			}
		}
	}

	/// <summary>
	/// Applied to NotificationScreen to suppress sounds queued very early in the load.
	/// sequence.
	/// </summary>
	[HarmonyPatch(typeof(NotificationScreen), nameof(NotificationScreen.PlayDingSound))]
	public static class NotificationScreen_PlayDingSound_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceSoundUpdates;

		/// <summary>
		/// Applied before PlayDingSound runs.
		/// </summary>
		internal static bool Prefix(Notification notification,
				IDictionary<NotificationType, string> ___notificationSounds) {
			// No const for that sound name
			return notification == null || FastTrackMod.GameRunning ||
				!___notificationSounds.TryGetValue(notification.Type, out string sound) ||
				sound != "Notification";
		}
	}

	/// <summary>
	/// Applied to MusicManager to turn off music updates when sound is disabled.
	/// </summary>
	[HarmonyPatch]
	public static class MusicManager_Play_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.DisableSound || options.ReduceSoundUpdates;
		}

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(MusicManager).GetMethodSafe(nameof(MusicManager.
				PlayDynamicMusic), false);
			yield return typeof(MusicManager).GetMethodSafe(nameof(MusicManager.PlaySong),
				false, typeof(string), typeof(bool));
		}

		/// <summary>
		/// Applied before these methods run.
		/// </summary>
		internal static bool Prefix() {
			var options = FastTrackOptions.Instance;
			return !options.DisableSound && KPlayerPrefs.GetFloat(SoundUpdater.VOLUME_MUSIC,
				1.0f) > 0.0f && KPlayerPrefs.GetFloat(SoundUpdater.VOLUME_MASTER, 1.0f) > 0.0f;
		}
	}

	/// <summary>
	/// Applied to multiple classes to turn off sound completely.
	/// </summary>
	[HarmonyPatch]
	public static class TurnOffSoundsPatch {
		internal static bool Prepare() => FastTrackOptions.Instance.DisableSound;

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(AnimEventManager).GetMethodSafe(nameof(AnimEventManager.
				PlayEvents), false, PPatchTools.AnyArguments);
			yield return typeof(AnimEventManager).GetMethodSafe(nameof(AnimEventManager.
				StopEvents), false, PPatchTools.AnyArguments);
			yield return typeof(AudioMixer).GetMethodSafe(nameof(AudioMixer.
				SetSnapshotParameter), false, PPatchTools.AnyArguments);
			yield return typeof(AudioMixer).GetMethodSafe(nameof(AudioMixer.Start), false,
				PPatchTools.AnyArguments);
			yield return typeof(AudioMixer).GetMethodSafe(nameof(AudioMixer.Stop), false,
				PPatchTools.AnyArguments);
			yield return typeof(ConduitFlowVisualizer).GetMethodSafe(nameof(
				ConduitFlowVisualizer.AddAudioSource), false, PPatchTools.AnyArguments);
			yield return typeof(ConduitFlowVisualizer).GetMethodSafe(nameof(
				ConduitFlowVisualizer.TriggerAudio), false, PPatchTools.AnyArguments);
			yield return typeof(MusicManager).GetMethodSafe(nameof(MusicManager.StopSong),
				false, PPatchTools.AnyArguments);
			yield return typeof(KFMOD).GetMethodSafe(nameof(KFMOD.CreateInstance), true,
				PPatchTools.AnyArguments);
			yield return typeof(KFMOD).GetMethodSafe(nameof(KFMOD.Initialize), true,
				PPatchTools.AnyArguments);
			yield return typeof(KFMOD).GetMethodSafe(nameof(KFMOD.RenderEveryTick), true,
				PPatchTools.AnyArguments);
			yield return typeof(SolidConduitFlowVisualizer).GetMethodSafe(nameof(
				SolidConduitFlowVisualizer.AddAudioSource), false, PPatchTools.AnyArguments);
			yield return typeof(SolidConduitFlowVisualizer).GetMethodSafe(nameof(
				SolidConduitFlowVisualizer.TriggerAudio), false, PPatchTools.AnyArguments);
			yield return typeof(SoundEvent).GetMethodSafe(nameof(SoundEvent.PlaySound), false,
				typeof(AnimEventManager.EventPlayerData), typeof(string));
		}

		/// <summary>
		/// Applied before these methods run.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
