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

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to Chatty to remove the every frame conversation behavior.
	/// </summary>
	[HarmonyPatch(typeof(Chatty), nameof(Chatty.SimEveryTick))]
	public static class Chatty_SimEveryTick_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.NoConversations || options.MiscOpts;
		}

		/// <summary>
		/// Applied before SimEveryTick runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to Chatty to trigger the joy reactions when talking starts. If "no
	/// conversations" is enabled, then this joy reaction can never trigger anyways.
	/// </summary>
	[HarmonyPatch(typeof(Chatty), nameof(Chatty.OnStartedTalking))]
	public static class Chatty_OnStartedTalking_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return !options.NoConversations && options.MiscOpts;
		}

		/// <summary>
		/// Applied before OnStartedTalking runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(object data, Chatty __instance) {
			if ((data is MinionIdentity other || (data is ConversationManager.
					StartedTalkingEvent evt && evt.talker != null && evt.talker.
					TryGetComponent(out other))) && UnityEngine.Random.Range(0, 100) <= 1 &&
					other != null && other != __instance.identity) {
				// Cannot talk to yourself (self)
				if (other.TryGetComponent(out StateMachineController smc))
					smc.GetSMI<JoyBehaviourMonitor.Instance>()?.GoToOverjoyed();
				if (__instance.TryGetComponent(out smc))
					smc.GetSMI<JoyBehaviourMonitor.Instance>()?.GoToOverjoyed();
			}
			__instance.conversationPartners.Clear();
			return false;
		}
	}

	/// <summary>
	/// Applied to ConversationManager to turn off all updates.
	/// </summary>
	[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.Sim200ms))]
	public static class ConversationManager_Sim200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before Sim200ms runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to DupeGreetingManager to disable greetings.
	/// </summary>
	[HarmonyPatch(typeof(DupeGreetingManager), nameof(DupeGreetingManager.Sim200ms))]
	public static class DupeGreetingManager_Sim200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before Sim200ms runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to SpeechMonitor.Instance to prevent speech from ever starting.
	/// </summary>
	[HarmonyPatch(typeof(SpeechMonitor.Instance), nameof(SpeechMonitor.Instance.PlaySpeech))]
	public static class SpeechMonitor_Instance_PlaySpeech_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before PlaySpeech runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to ThoughtGraph.Instance to prevent any thoughts from being added.
	/// </summary>
	[HarmonyPatch(typeof(ThoughtGraph.Instance), nameof(ThoughtGraph.Instance.AddThought))]
	public static class ThoughtGraph_Instance_AddThought_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before AddThought runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}
}
