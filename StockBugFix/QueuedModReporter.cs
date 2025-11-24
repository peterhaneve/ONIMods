/*
 * Copyright 2025 Peter Han
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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// A component to report queued mod updates, replacing MainMenu.Update.
	/// </summary>
	public sealed class QueuedModReporter : KMonoBehaviour {
		private const float MAX_LOCKOUT = 10.0f;

		// Detour the "Resume Game" button
		private static readonly IDetouredField<MainMenu, KButton> RESUME_GAME = PDetours.
			DetourFieldLazy<MainMenu, KButton>(nameof(MainMenu.Button_ResumeGame));

		public static void Init() {
			firstLoad = true;
		}

		/// <summary>
		/// Only lock the button out on the first main menu load; loading and quitting a game
		/// is so slow that it will certainly be loaded by then.
		/// </summary>
		private static volatile bool firstLoad;

		/// <summary>
		/// Is the mods button already enabled?
		/// </summary>
		private bool buttonEnabled;

		/// <summary>
		/// The button used to open the Mods screen.
		/// </summary>
		private KButton modsButton;

		/// <summary>
		/// The time when the menu was first opened.
		/// </summary>
		private float startupTime;

		public QueuedModReporter() {
			buttonEnabled = true;
			modsButton = null;
			startupTime = 0.0f;
		}
		
		/// <summary>
		/// Finds the "Mods" button and stores it in the modsButton field.
		/// </summary>
		/// <param name="buttonParent">The parent of all the main menu buttons.</param>
		private void FindModsButton(Transform buttonParent) {
			int n = buttonParent.childCount;
			for (int i = 0; i < n; i++) {
				var button = buttonParent.GetChild(i).gameObject;
				// Match by text... sucks but only Mod Updater and AVC change it this early
				if (button != null && button.TryGetComponent(out KButton kb)) {
					string text = button.GetComponentInChildren<LocText>()?.text;
					if (text != null && text.StartsWith(STRINGS.UI.FRONTEND.MODS.TITLE)) {
						modsButton = kb;
						break;
					}
				}
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			startupTime = 0.0f;
			if (firstLoad && StockBugFixOptions.Instance.DelayModsMenu && TryGetComponent(
					out MainMenu mm)) {
				firstLoad = false;
				try {
					Transform buttonParent;
					KButton resumeButton;
					// "Resume game" is in the same panel as "Mods"
					if (mm != null && (resumeButton = RESUME_GAME.Get(mm)) != null &&
							(buttonParent = resumeButton.transform.parent) != null)
						FindModsButton(buttonParent);
				} catch (DetourException) { }
				if (modsButton != null) {
					modsButton.isInteractable = false;
					buttonEnabled = false;
				} else
					PUtil.LogWarning("Unable to find Mods menu button, mods lockout will not be functional");
			}
		}

		public void Update() {
			float now = Time.unscaledTime;
			var inst = QueuedReportManager.Instance;
			if (isActiveAndEnabled && inst != null) {
				if (startupTime <= 0.0f)
					startupTime = now;
				else {
					if (!buttonEnabled && (inst.ReadyToReport || now - MAX_LOCKOUT >
							startupTime)) {
						modsButton.isInteractable = true;
						buttonEnabled = true;
					}
					inst.CheckQueuedReport(gameObject);
				}
			}
		}
	}
}
