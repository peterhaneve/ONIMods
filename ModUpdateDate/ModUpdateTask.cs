/*
 * Copyright 2021 Peter Han
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

using PeterHan.PLib;
using PeterHan.PLib.Detours;
using PeterHan.PLib.UI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using UISTRINGS = PeterHan.ModUpdateDate.ModUpdateDateStrings.UI.MODUPDATER;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Stores the instance data for a mod update or batch mod update.
	/// </summary>
	internal sealed class ModUpdateTask {
		/// <summary>
		/// Caches a reference to the mActiveModifiers field of KInputController.
		/// </summary>
		private static readonly IDetouredField<KInputController, Modifier> ACTIVE_MODIFIERS =
			PDetours.DetourFieldLazy<KInputController, Modifier>("mActiveModifiers");

		/// <summary>
		/// Saves the mod enabled settings and restarts the game.
		/// </summary>
		private static void SaveAndRestart() {
			PUtil.SaveMods();
			App.instance.Restart();
		}

		/// <summary>
		/// The mods to be updated.
		/// </summary>
		public Queue<ModToUpdate> Mods { get; }

		/// <summary>
		/// The mods to be updated.
		/// </summary>
		public List<ModUpdateResult> Results { get; }

		internal ModUpdateTask(ModToUpdate toUpdate) {
			if (toUpdate == null)
				throw new ArgumentNullException("toUpdate");
			Mods = new Queue<ModToUpdate>(1);
			Mods.Enqueue(toUpdate);
			Results = new List<ModUpdateResult>(1);
		}

		internal ModUpdateTask(IEnumerable<ModToUpdate> updateAll) {
			if (updateAll == null)
				throw new ArgumentNullException("toUpdate");
			Mods = new Queue<ModToUpdate>(updateAll);
			Results = new List<ModUpdateResult>(Mods.Count);
		}

		/// <summary>
		/// Appends the text for the updated mod entry.
		/// </summary>
		/// <param name="resultText">The location where the result text will be stored.</param>
		/// <param name="result">The result from the mod update.</param>
		/// <param name="updated">The number of mods successfully updated so far.</param>
		private void AddText(StringBuilder resultText, ModUpdateResult result) {
			string title = result.Title;
			switch (result.Status) {
			case ModDownloadStatus.ConfigError:
				// Success but configuration could not be saved
				resultText.AppendFormat(UISTRINGS.UPDATE_OK_NOCONFIG, title);
				break;
			case ModDownloadStatus.OK:
				// Success!
				int configs = result.ConfigsRestored;
				if (configs > 0)
					resultText.AppendFormat(configs == 1 ? UISTRINGS.UPDATE_OK_CONFIG :
						UISTRINGS.UPDATE_OK_CONFIG_1, title, configs);
				break;
			case ModDownloadStatus.NoSteamFile:
				// Steam data not found
				resultText.AppendFormat(UISTRINGS.UPDATE_ERROR, title, UISTRINGS.
					UPDATE_NOFILE);
				break;
			case ModDownloadStatus.ModUninstalled:
				// Mod data not found
				resultText.AppendFormat(UISTRINGS.UPDATE_ERROR, title, UISTRINGS.
					UPDATE_NODETAILS);
				break;
			case ModDownloadStatus.SteamError:
			default:
				string message;
				switch (result.Result) {
				case EResult.k_EResultServiceUnavailable:
					message = UISTRINGS.UPDATE_CANTSTART;
					break;
				case EResult.k_EResultNotLoggedOn:
					message = UISTRINGS.UPDATE_OFFLINE;
					break;
				default:
					message = result.Result.ToString();
					break;
				}
				resultText.AppendFormat(UISTRINGS.UPDATE_ERROR, title, message);
				break;
			}
		}

		/// <summary>
		/// Shows a confirmation dialog to force update the specified mod(s).
		/// </summary>
		internal void TryUpdateMods(GameObject _) {
			var controller = Global.Instance.GetInputManager()?.GetDefaultController();
			var modifier = Modifier.None;
			try {
				modifier = ACTIVE_MODIFIERS.Get(controller);
			} catch (DetourException) { }
			// Check for SHIFT - bypass dialog
			if (modifier == Modifier.Shift)
				UpdateMods();
			else {
				var modList = new StringBuilder(256);
				// Add up to the limit to avoid making a dialog larger than the screen
				int n = 0;
				foreach (var mod in Mods) {
					modList.AppendFormat(UISTRINGS.CONFIRM_LINE, mod.Title);
					n++;
					// (and N more...)
					if (n >= ModUpdateDateStrings.MAX_LINES) {
						modList.AppendFormat(UISTRINGS.CONFIRM_MORE, Mods.Count - n);
						break;
					}
				}
				PUIElements.ShowConfirmDialog(null, string.Format(UISTRINGS.CONFIRM_UPDATE,
					modList.ToString()), UpdateMods, null, UISTRINGS.CONFIRM_OK, UISTRINGS.
					CONFIRM_CANCEL);
			}
		}

		/// <summary>
		/// Force updates the specified mod(s).
		/// </summary>
		private void UpdateMods() {
			var instance = ModUpdateHandler.Instance;
			if (instance.IsUpdating)
				PUIElements.ShowMessageDialog(null, UISTRINGS.UPDATE_INPROGRESS);
			else
				instance.StartModUpdate(this);
		}

		/// <summary>
		/// Shows the summary text once all mods get updated.
		/// </summary>
		internal void OnComplete() {
			bool errors = false, configFail = false;
			int updated = 0, nominal = 0, n = 0;
			var resultText = new StringBuilder(512);
			resultText.Append(UISTRINGS.UPDATE_HEADER);
			Results.Sort();
			foreach (var result in Results) {
				// Update cumulative status
				if (result.Status == ModDownloadStatus.ConfigError) {
					configFail = true;
					updated++;
				} else if (result.Status == ModDownloadStatus.OK)
					updated++;
				else
					errors = true;
				// Reduce clutter from no-config mods successfully updated
				if (result.Status == ModDownloadStatus.OK && result.ConfigsRestored == 0)
					nominal++;
				else {
					// Only add the maximum number of lines
					if (n < ModUpdateDateStrings.MAX_LINES)
						AddText(resultText, result);
					n++;
				}
			}
			if (n > ModUpdateDateStrings.MAX_LINES)
				// (and N more...)
				resultText.AppendFormat(UISTRINGS.CONFIRM_MORE, n - ModUpdateDateStrings.
					MAX_LINES);
			if (nominal > 0) {
				if (Results.Count == 1)
					// Specify mod that was updated with no errors
					resultText.AppendFormat(UISTRINGS.UPDATE_SINGLE, Results[0].Title);
				else
					// N other mod(s) were updated with no errors
					resultText.AppendFormat(nominal == 1 ? UISTRINGS.UPDATE_REST_1 :
						UISTRINGS.UPDATE_REST, nominal);
			}
			if (updated > 0)
				// Success text
				resultText.AppendFormat(UISTRINGS.UPDATE_FOOTER_OK, updated > 1 ? UISTRINGS.
					UPDATE_MULTIPLE : UISTRINGS.UPDATE_ONE);
			if (errors)
				// Error text
				resultText.Append(UISTRINGS.UPDATE_FOOTER_ERROR);
			if (configFail)
				// Config warning text
				resultText.Append(UISTRINGS.UPDATE_FOOTER_CONFIG);
			PUIElements.ShowConfirmDialog(null, resultText.ToString(), SaveAndRestart,
				null, STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.OK, STRINGS.UI.FRONTEND.
				MOD_DIALOGS.RESTART.CANCEL);
		}

		public override string ToString() {
			return Mods.ToString();
		}
	}
}
