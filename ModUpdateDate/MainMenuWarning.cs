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
using UnityEngine;

using UISTRINGS = PeterHan.ModUpdateDate.ModUpdateDateStrings.UI.MODUPDATER;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Added to the main menu to warn users if mods are out of date.
	/// </summary>
	internal sealed class MainMenuWarning : KMonoBehaviour {
		/// <summary>
		/// The singleton (should be!) instance of this class.
		/// </summary>
		internal static MainMenuWarning Instance { get; private set; }

		/// <summary>
		/// The button used to open the Mods screen.
		/// </summary>
		private GameObject modsButton;

		internal MainMenuWarning() {
			modsButton = null;
		}

		protected override void OnCleanUp() {
			Instance = null;
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			Transform buttonParent;
			base.OnPrefabInit();
			if (Instance != null)
				PUtil.LogWarning("Multiple instances of MainMenuWarning have been created!");
			Instance = this;
			var resumeButton = GetComponent<MainMenu>()?.Button_ResumeGame;
			// "resume game" is in the same panel
			if (resumeButton != null && (buttonParent = resumeButton.gameObject.transform?.
					parent) != null) {
				int n = buttonParent.childCount;
				for (int i = 0; i < n; i++) {
					var button = buttonParent.GetChild(i).gameObject;
					// Match by text... sucks but unlikely to have changed this early
					if (button != null && button.GetComponentInChildren<LocText>()?.text ==
							STRINGS.UI.FRONTEND.MODS.TITLE) {
						modsButton = button;
						break;
					}
				}
				if (modsButton == null)
					PUtil.LogWarning("Unable to find Mods menu button, main menu update " +
						"warning will not be functional");
			}
			UpdateText();
		}

		/// <summary>
		/// Updates the Mods button text.
		/// </summary>
		public void UpdateText() {
			if (modsButton != null) {
				var modsText = modsButton.GetComponentInChildren<LocText>();
				// How many are out of date?
				int outdated = ModUpdateHandler.CountOutdatedMods();
				string text = STRINGS.UI.FRONTEND.MODS.TITLE;
				if (outdated == 1)
					text += UISTRINGS.MAINMENU_UPDATE_1;
				else if (outdated > 1)
					text += string.Format(UISTRINGS.MAINMENU_UPDATE, outdated);
				if (modsText != null)
					modsText.text = text;
			}
		}
	}
}
