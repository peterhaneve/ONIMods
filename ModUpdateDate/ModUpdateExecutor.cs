/*
 * Copyright 2020 Peter Han
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

using KMod;
using PeterHan.PLib;
using PeterHan.PLib.UI;
using Steamworks;
using System;
using System.Reflection;
using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Stores the instance data for one mod update.
	/// </summary>
	internal sealed class ModUpdateExecutor {
		/// <summary>
		/// Caches a reference to the mActiveModifiers field of KInputController.
		/// </summary>
		private static FieldInfo activeModifiers = typeof(KInputController).GetFieldSafe(
			"mActiveModifiers", false);

		/// <summary>
		/// The last update on the Steam Workshop.
		/// </summary>
		internal System.DateTime LastSteamUpdate { get; }

		/// <summary>
		/// The Steam mod ID of this mod.
		/// </summary>
		internal PublishedFileId_t SteamID { get; }

		/// <summary>
		/// The mod to update.
		/// </summary>
		private readonly Mod mod;

		internal ModUpdateExecutor(Mod mod) {
			this.mod = mod ?? throw new ArgumentNullException("mod");
			if (mod.label.distribution_platform != Label.DistributionPlatform.Steam)
				throw new ArgumentException("Only Steam mods can be updated by this class");
			SteamID = mod.GetSteamModID();
			if (!SteamID.GetGlobalLastModified(out System.DateTime steamLastUpdate))
				steamLastUpdate = System.DateTime.MinValue;
			LastSteamUpdate = steamLastUpdate;
		}

		/// <summary>
		/// Shows a confirmation dialog to force update the specified mod.
		/// </summary>
		internal void TryUpdateMod(GameObject _) {
			var controller = Global.Instance.GetInputManager()?.GetDefaultController();
			// Check for SHIFT - bypass dialog
			if (activeModifiers != null && (activeModifiers.GetValue(controller) is Modifier
					modifier) && modifier == Modifier.Shift)
				UpdateMod();
			else
				PUIElements.ShowConfirmDialog(null, string.Format(ModUpdateDateStrings.
					CONFIG_WARNING, mod.label.title), UpdateMod, null,
					ModUpdateDateStrings.UPDATE_CONTINUE, ModUpdateDateStrings.
					UPDATE_CANCEL);
		}

		/// <summary>
		/// Force updates the specified mod.
		/// </summary>
		internal void UpdateMod() {
			var label = mod.label;
			if (ModUpdateDetails.Details.TryGetValue(SteamID.m_PublishedFileId,
					out SteamUGCDetails_t details))
				ModUpdateHandler.Instance.StartModUpdate(mod, details, LastSteamUpdate);
			else
				// Uninstalled?
				PUtil.LogWarning("Unable to find details for mod: " + label.title);
		}

		public override string ToString() {
			return mod.label.title;
		}
	}
}
