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
using System.Collections.Generic;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Caches calls to the DlcManager to avoid going out to Steam/Epic/WeGame every time.
	/// </summary>
	public static class DLCManagerCache {
		/// <summary>
		/// We can hope...
		/// </summary>
		private static readonly IDictionary<string, bool> DLC_ENABLED =
			new Dictionary<string, bool>(8);

		/// <summary>
		/// Checks to see if a particular DLC is active and enabled.
		/// </summary>
		/// <param name="dlcID">The DLC ID to check.</param>
		/// <returns>true if it is turned on, or false otherwise.</returns>
		public static bool IsDlcEnabled(string dlcID) {
			bool enabled;
			if (string.IsNullOrEmpty(dlcID))
				enabled = true;
			else if (!DLC_ENABLED.TryGetValue(dlcID, out enabled)) {
				enabled = DlcManager.IsContentSettingEnabled(dlcID);
				DLC_ENABLED.Add(dlcID, enabled);
			}
			return enabled;
		}
	}

	/// <summary>
	/// Applied to DlcManager to speed up the highest DLC active check.
	/// </summary>
	[HarmonyPatch(typeof(DlcManager), nameof(DlcManager.GetHighestActiveDlcId))]
	public static class DlcManager_GetHighestActiveDlcId_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before GetHighestActiveDlcId runs.
		/// </summary>
		internal static bool Prefix(ref string __result) {
			var order = DlcManager.RELEASE_ORDER;
			string dlc = string.Empty;
			for (int i = order.Count - 1; i >= 0; i--) {
				string dlcInOrder = order[i];
				if (DLCManagerCache.IsDlcEnabled(dlcInOrder)) {
					dlc = dlcInOrder;
					break;
				}
			}
			__result = dlc;
			return false;
		}
	}

	/// <summary>
	/// Applied to DlcManager to speed up checks if content is active.
	/// </summary>
	[HarmonyPatch(typeof(DlcManager), nameof(DlcManager.IsContentActive))]
	public static class DlcManager_IsContentActive_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before IsContentActive runs.
		/// </summary>
		internal static bool Prefix(string dlcId, ref bool __result) {
			__result = DLCManagerCache.IsDlcEnabled(dlcId);
			return false;
		}
	}

	/// <summary>
	/// Applied to DlcManager to speed up checks for the valid DLC list. Report true if
	/// (no DLCs are installed AND list contains "") OR (a DLC id in the list is enabled)
	/// </summary>
	[HarmonyPatch(typeof(DlcManager), nameof(DlcManager.IsDlcListValidForCurrentContent))]
	public static class DlcManager_IsDlcListValidForCurrentContent_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before IsDlcListValidForCurrentContent runs.
		/// </summary>
		internal static bool Prefix(string[] dlcIds, ref bool __result) {
			int n = dlcIds.Length;
			bool found = false;
			for (int i = 0; i < n && !found; i++) {
				string dlcId = dlcIds[i];
				if (DLCManagerCache.IsDlcEnabled(dlcId))
					found = true;
			}
			__result = found;
			return false;
		}
	}
}
