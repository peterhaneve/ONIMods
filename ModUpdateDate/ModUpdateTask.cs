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

using PeterHan.PLib;
using PeterHan.PLib.UI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Stores the instance data for a mod update or batch mod update.
	/// </summary>
	internal sealed class ModUpdateTask {
		/// <summary>
		/// Caches a reference to the mActiveModifiers field of KInputController.
		/// </summary>
		private static readonly FieldInfo activeModifiers = typeof(KInputController).
			GetFieldSafe("mActiveModifiers", false);

		/// <summary>
		/// Saves the mod enabled settings and restarts the game.
		/// </summary>
		private static void SaveAndRestart() {
			Global.Instance?.modManager?.Save();
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
				resultText.AppendFormat(ModUpdateDateStrings.UPDATE_OK_NOCONFIG, title);
				break;
			case ModDownloadStatus.OK:
				// Success!
				int configs = result.ConfigsRestored;
				if (configs > 0)
					resultText.AppendFormat(ModUpdateDateStrings.UPDATE_OK_CONFIG, title,
						configs, configs > 1 ? ModUpdateDateStrings.PLURAL.text : "");
				else
					resultText.AppendFormat(ModUpdateDateStrings.UPDATE_OK, title);
				break;
			case ModDownloadStatus.NoSteamFile:
				// Steam data not found
				resultText.AppendFormat(ModUpdateDateStrings.UPDATE_ERROR, title,
					ModUpdateDateStrings.UPDATE_NOFILE);
				break;
			case ModDownloadStatus.ModUninstalled:
				// Mod data not found
				resultText.AppendFormat(ModUpdateDateStrings.UPDATE_ERROR, title,
					ModUpdateDateStrings.UPDATE_NODETAILS);
				break;
			case ModDownloadStatus.SteamError:
			default:
				string message;
				switch (result.Result) {
				case EResult.k_EResultServiceUnavailable:
					message = ModUpdateDateStrings.UPDATE_CANTSTART;
					break;
				case EResult.k_EResultNotLoggedOn:
					message = ModUpdateDateStrings.UPDATE_OFFLINE;
					break;
				default:
					message = result.Result.ToString();
					break;
				}
				resultText.AppendFormat(ModUpdateDateStrings.UPDATE_ERROR, title, message);
				break;
			}
		}

		/// <summary>
		/// Shows a confirmation dialog to force update the specified mod(s).
		/// </summary>
		internal void TryUpdateMods(GameObject _) {
			var controller = Global.Instance.GetInputManager()?.GetDefaultController();
			// Check for SHIFT - bypass dialog
			if (activeModifiers != null && (activeModifiers.GetValue(controller) is Modifier
					modifier) && modifier == Modifier.Shift)
				UpdateMods();
			else {
				var modList = new StringBuilder(256);
				// Add up to the limit to avoid making a dialog larger than the screen
				int n = 0;
				foreach (var mod in Mods) {
					modList.AppendFormat(ModUpdateDateStrings.CONFIRM_LINE, mod.Title);
					n++;
					// (and N more...)
					if (n >= ModUpdateDateStrings.MAX_LINES) {
						modList.AppendFormat(ModUpdateDateStrings.CONFIRM_MORE, Mods.Count - n);
						break;
					}
				}
				PUIElements.ShowConfirmDialog(null, string.Format(ModUpdateDateStrings.
					CONFIRM_UPDATE, modList.ToString()), UpdateMods, null,
					ModUpdateDateStrings.CONFIRM_OK, ModUpdateDateStrings.CONFIRM_CANCEL);
			}
		}

		/// <summary>
		/// Force updates the specified mod(s).
		/// </summary>
		private void UpdateMods() {
			var instance = ModUpdateHandler.Instance;
			if (instance.IsUpdating)
				PUIElements.ShowMessageDialog(null, ModUpdateDateStrings.UPDATE_INPROGRESS);
			else
				instance.StartModUpdate(this);
		}

		/// <summary>
		/// Shows the summary text once all mods get updated.
		/// </summary>
		internal void OnComplete() {
			bool errors = false, configFail = false;
			int updated = 0, n = 0;
			var resultText = new StringBuilder(512);
			resultText.Append(ModUpdateDateStrings.UPDATE_HEADER);
			Results.Sort();
			foreach (var result in Results) {
				// Update cumulative status
				if (result.Status != ModDownloadStatus.ConfigError && result.Status !=
						ModDownloadStatus.OK)
					errors = true;
				else
					updated++;
				if (result.Status == ModDownloadStatus.ConfigError)
					configFail = true;
				// Only add the maximum number of lines
				if (n < ModUpdateDateStrings.MAX_LINES)
					AddText(resultText, result);
				else if (n == ModUpdateDateStrings.MAX_LINES)
					// (and N more...)
					resultText.AppendFormat(ModUpdateDateStrings.CONFIRM_MORE, Results.Count -
						n);
				n++;
			}
			if (updated > 0)
				// Success text
				resultText.AppendFormat(ModUpdateDateStrings.UPDATE_FOOTER_OK, updated > 1 ?
					ModUpdateDateStrings.UPDATE_MULTIPLE : ModUpdateDateStrings.UPDATE_ONE);
			if (errors)
				// Error text
				resultText.Append(ModUpdateDateStrings.UPDATE_FOOTER_ERROR);
			if (configFail)
				// Config warning text
				resultText.Append(ModUpdateDateStrings.UPDATE_FOOTER_CONFIG);
			PUIElements.ShowConfirmDialog(null, resultText.ToString(), SaveAndRestart,
				null, STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.OK, STRINGS.UI.FRONTEND.
				MOD_DIALOGS.RESTART.CANCEL);
		}

		public override string ToString() {
			return Mods.ToString();
		}
	}
}
