/*
 * Copyright 2023 Peter Han
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

using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using SideScreenRef = DetailsScreen.SideScreenRef;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Stores detours used for Klei UI components. Klei loves adding optional parameters and
	/// changing fields to/from properties, which while source compatible is binary
	/// incompatible. These lazy detours (resolved on first use) can bridge over a variety of
	/// such differences with minimal overhead and no recompilation.
	/// </summary>
	internal static class UIDetours {
		#region ConfirmDialogScreen
		public delegate void PCD(ConfirmDialogScreen dialog, string text,
			System.Action on_confirm, System.Action on_cancel, string configurable_text,
			System.Action on_configurable_clicked, string title_text, string confirm_text,
			string cancel_text);

		public static readonly DetouredMethod<PCD> POPUP_CONFIRM = typeof(ConfirmDialogScreen).DetourLazy<PCD>(nameof(ConfirmDialogScreen.PopupConfirmDialog));
		#endregion

		#region DetailsScreen
		public static readonly IDetouredField<DetailsScreen, List<SideScreenRef>> SIDE_SCREENS = PDetours.DetourFieldLazy<DetailsScreen, List<SideScreenRef>>("sideScreens");
		public static readonly IDetouredField<DetailsScreen, GameObject> SS_CONTENT_BODY = PDetours.DetourFieldLazy<DetailsScreen, GameObject>("sideScreenContentBody");
		#endregion

		#region KButton
		public static readonly IDetouredField<KButton, KImage[]> ADDITIONAL_K_IMAGES = PDetours.DetourFieldLazy<KButton, KImage[]>(nameof(KButton.additionalKImages));
		public static readonly IDetouredField<KButton, KImage> BG_IMAGE = PDetours.DetourFieldLazy<KButton, KImage>(nameof(KButton.bgImage));
		public static readonly IDetouredField<KButton, Image> FG_IMAGE = PDetours.DetourFieldLazy<KButton, Image>(nameof(KButton.fgImage));
		public static readonly IDetouredField<KButton, bool> IS_INTERACTABLE = PDetours.DetourFieldLazy<KButton, bool>(nameof(KButton.isInteractable));
		public static readonly IDetouredField<KButton, ButtonSoundPlayer> SOUND_PLAYER_BUTTON = PDetours.DetourFieldLazy<KButton, ButtonSoundPlayer>(nameof(KButton.soundPlayer));
		#endregion

		#region KImage
		public static readonly IDetouredField<KImage, ColorStyleSetting> COLOR_STYLE_SETTING = PDetours.DetourFieldLazy<KImage, ColorStyleSetting>(nameof(KImage.colorStyleSetting));

		public static readonly DetouredMethod<Action<KImage>> APPLY_COLOR_STYLE = typeof(KImage).DetourLazy<Action<KImage>>(nameof(KImage.ApplyColorStyleSetting));
		#endregion

		#region KScreen
		public static readonly DetouredMethod<Action<KScreen>> ACTIVATE_KSCREEN = typeof(KScreen).DetourLazy<Action<KScreen>>(nameof(KScreen.Activate));
		public static readonly DetouredMethod<Action<KScreen>> DEACTIVATE_KSCREEN = typeof(KScreen).DetourLazy<Action<KScreen>>(nameof(KScreen.Deactivate));
		#endregion

		#region KToggle
		public static readonly IDetouredField<KToggle, KToggleArtExtensions> ART_EXTENSION = PDetours.DetourFieldLazy<KToggle, KToggleArtExtensions>(nameof(KToggle.artExtension));
		public static readonly IDetouredField<KToggle, bool> IS_ON = PDetours.DetourFieldLazy<KToggle, bool>(nameof(KToggle.isOn));
		public static readonly IDetouredField<KToggle, ToggleSoundPlayer> SOUND_PLAYER_TOGGLE = PDetours.DetourFieldLazy<KToggle, ToggleSoundPlayer>(nameof(KToggle.soundPlayer));
		#endregion

		#region LocText
		public static readonly IDetouredField<LocText, string> LOCTEXT_KEY = PDetours.DetourFieldLazy<LocText, string>(nameof(LocText.key));
		public static readonly IDetouredField<LocText, TextStyleSetting> LOCTEXT_STYLE = PDetours.DetourFieldLazy<LocText, TextStyleSetting>(nameof(LocText.textStyleSetting));
		#endregion

		#region MultiToggle
		public static readonly IDetouredField<MultiToggle, int> CURRENT_STATE = PDetours.DetourFieldLazy<MultiToggle, int>(nameof(MultiToggle.CurrentState));
		public static readonly IDetouredField<MultiToggle, bool> PLAY_SOUND_CLICK = PDetours.DetourFieldLazy<MultiToggle, bool>(nameof(MultiToggle.play_sound_on_click));
		public static readonly IDetouredField<MultiToggle, bool> PLAY_SOUND_RELEASE = PDetours.DetourFieldLazy<MultiToggle, bool>(nameof(MultiToggle.play_sound_on_release));

		public static readonly DetouredMethod<Action<MultiToggle, int>> CHANGE_STATE = typeof(MultiToggle).DetourLazy<Action<MultiToggle, int>>(nameof(MultiToggle.ChangeState));
		#endregion

		#region SideScreenContent
		public static readonly IDetouredField<SideScreenContent, GameObject> SS_CONTENT_CONTAINER = PDetours.DetourFieldLazy<SideScreenContent, GameObject>(nameof(SideScreenContent.ContentContainer));
		#endregion

		#region SideScreenRef
		public static readonly IDetouredField<SideScreenRef, Vector2> SS_OFFSET = PDetours.DetourFieldLazy<SideScreenRef, Vector2>(nameof(SideScreenRef.offset));
		public static readonly IDetouredField<SideScreenRef, SideScreenContent> SS_PREFAB = PDetours.DetourFieldLazy<SideScreenRef, SideScreenContent>(nameof(SideScreenRef.screenPrefab));
		public static readonly IDetouredField<SideScreenRef, SideScreenContent> SS_INSTANCE = PDetours.DetourFieldLazy<SideScreenRef, SideScreenContent>(nameof(SideScreenRef.screenInstance));
		#endregion
	}
}
