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

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to ConversationManager to turn off all updates.
	/// </summary>
	[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.Sim200ms))]
	public static class ConversationManager_Sim200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoConversations;

		/// <summary>
		/// Applied before Sim200ms runs.
		/// </summary>
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
		internal static bool Prefix() {
			return false;
		}
	}
}
