/*
 * Copyright 2019 Peter Han
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

using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Adds an "Options" screen to a mod in the Mods menu.
	/// </summary>
	public sealed class POptions {
		/// <summary>
		/// The text shown on the Done button.
		/// </summary>
		public static readonly LocString BUTTON_OK = STRINGS.UI.FRONTEND.OPTIONS_SCREEN.BACK;

		/// <summary>
		/// The text shown on the Options button.
		/// </summary>
		public static readonly LocString BUTTON_OPTIONS = STRINGS.UI.FRONTEND.MAINMENU.OPTIONS;

		/// <summary>
		/// The dialog title, where {0} is substituted with the mod friendly name.
		/// </summary>
		public static readonly string DIALOG_TITLE = "Options for {0}";

		/// <summary>
		/// The location where mod option types are stored.
		/// </summary>
		private static readonly IDictionary<string, Type> options = new Dictionary<string, Type>(4);

		/// <summary>
		/// Adds the Options button to the Mods screen.
		/// </summary>
		/// <param name="instance">The current mods screen.</param>
		/// <param name="modEntry">The mod entry where the button should be added.</param>
		private static void AddModOptions(ModsScreen instance, Traverse modEntry) {
			var modSpec = Global.Instance.modManager.mods[modEntry.GetField<int>(
				"mod_index")];
			string modID = modSpec.label.id;
			if (modSpec.enabled && !string.IsNullOrEmpty(modID) && options.TryGetValue(modID,
					out Type optionsType)) {
				var transform = modEntry.GetField<RectTransform>("rect_transform");
				// Copy the Manage button
				var templateButton = transform.Find("ManageButton");
				if (templateButton == null)
					PUtil.LogWarning("Could not find template button!");
				else {
					// Create delegate to spawn actions dialog
					var action = new OptionsAction(instance, optionsType);
					var settingsButton = PUIElements.CreateButton(transform.gameObject,
						templateButton, "ModSettingsButton", action.OnModOptions);
					PUIElements.AddSizeFitter(settingsButton);
					PUIElements.SetText(settingsButton, BUTTON_OPTIONS.text);
					PUIElements.SetToolTip(settingsButton, DIALOG_TITLE.F(modSpec.title));
					// Move before the subscription and enable button
					settingsButton.transform.SetSiblingIndex(3);
				}
			}
		}

		public static void RegisterOptions(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			var assembly = optionsType.Assembly;
			bool hasPath = false;
			try {
				var modDir = Directory.GetParent(assembly.Location);
				if (modDir != null) {
					var id = Path.GetFileName(modDir.FullName);
					if (options.ContainsKey(id))
						PUtil.LogWarning("Duplicate mod ID: " + id);
					else
						options.Add(id, optionsType);
					hasPath = true;
				}
			} catch (IOException) { }
			if (!hasPath)
				PUtil.LogWarning("Unable to determine mod path for assembly: " + assembly.
					FullName);
		}

		/// <summary>
		/// Applied to ModsScreen to add the buttons to BuildDisplay.
		/// </summary>
#if false
		[HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
		public static class ModsScreen_BuildDisplay_Patch {
			private static void Postfix(ref ModsScreen __instance, ref object ___displayedMods) {
				// Must cast the type because ModsScreen.DisplayedMod is private
				var mods = (System.Collections.IEnumerable)___displayedMods;
				foreach (var displayedMod in mods)
					AddModOptions(__instance, Traverse.Create(displayedMod));
			}
		}
#endif

		/// <summary>
		/// A triggerable action for handling mod options events that opens the options dialog.
		/// </summary>
		private sealed class OptionsAction {
			/// <summary>
			/// The Mods screen which will own this dialog.
			/// </summary>
			private readonly ModsScreen modsScreen;

			/// <summary>
			/// The type used to determine which options are visible.
			/// </summary>
			private readonly Type optionsType;

			public OptionsAction(ModsScreen modsScreen, Type optionsType) {
				this.modsScreen = modsScreen ?? throw new ArgumentNullException("modsScreen");
				this.optionsType = optionsType ?? throw new ArgumentNullException(
					"optionsType");
			}
			
			/// <summary>
			/// Triggered when the Mod Options button is clicked.
			/// </summary>
			public void OnModOptions() {
				PUtil.LogDebug("Options for " + optionsType.FullName);
				var optionsScreen = KScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.
					ConfirmDialogScreen.gameObject, modsScreen.gameObject);
			}
		}
	}

	/// <summary>
	/// The screen displayed for mod options.
	/// </summary>
	sealed class ModOptionsScreen : KModalScreen {
		protected override void OnActivate() {
			base.OnActivate();
		}

		/// <summary>
		/// Triggered when the options screen is closed.
		/// </summary>
		private void OnClose() {
			Deactivate();
		}
	}
}
