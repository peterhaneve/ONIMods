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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Patches in the changes required by SimpleInfoScreenWrapper.
	/// </summary>
	internal sealed partial class SimpleInfoScreenWrapper {
		/// <summary>
		/// Applied to SimpleInfoScreen to add our component to its game object.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnPrefabInit))]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(SimpleInfoScreen __instance) {
				if (__instance != null)
					__instance.gameObject.AddOrGet<SimpleInfoScreenWrapper>();
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to update the selected target.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnSelectTarget))]
		internal static class OnSelectTarget_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before OnSelectTarget runs.
			/// </summary>
			internal static void Prefix(SimpleInfoScreen __instance, GameObject target) {
				if (instance != null && __instance.lastTarget != target)
					instance.OnSelectTarget(target);
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to speed up refreshing it.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(bool force) {
				if (instance != null)
					instance.Refresh(force);
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the checklist of conditions for operation.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.
			RefreshProcessConditionsPanel))]
		internal static class RefreshProcessConditions_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshProcessConditionsPanel runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
				if (instance != null && instance.lastSelection.conditions != null)
					instance.RefreshProcess();
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the storage when storage changes. Even
		/// though the Refresh method was replaced, other sections of the game call this method
		/// directly.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.RefreshStoragePanel))]
		internal static class RefreshStoragePanel_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshStoragePanel runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
				var inst = instance;
				if (inst != null)
					inst.RefreshStorage();
				return false;
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the cluster map info when the refresh is
		/// triggered. Even though the Refresh method was replaced, other sections of the game
		/// call this method directly.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.RefreshWorldPanel))]
		internal static class RefreshWorldPanel_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before RefreshWorldPanel runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
				if (instance != null)
					instance.RefreshWorld();
				return false;
			}
		}
    }

	/// <summary>
	/// Sorts side screens by their sort key.
	/// </summary>
	internal sealed class StressNoteComparer : IComparer<ReportManager.ReportEntry.Note> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly StressNoteComparer INSTANCE = new StressNoteComparer();

		private StressNoteComparer() { }

		public int Compare(ReportManager.ReportEntry.Note x, ReportManager.ReportEntry.Note y)
		{
			return x.value.CompareTo(y.value);
		}
	}
}
