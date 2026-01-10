/*
 * Copyright 2026 Peter Han
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
using PeterHan.PLib.UI;
using Steamworks;
using System;
using UnityEngine;
using UnityEngine.UI;

using UI = PeterHan.DebugNotIncluded.DebugNotIncludedStrings.UI;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Handles the additional mod actions window.
	/// </summary>
	internal sealed class MoreModActions : KMonoBehaviour {
		/// <summary>
		/// The screen to show when More Mod Actions is clicked.
		/// </summary>
		private ModActionsScreen actionsScreen;

		/// <summary>
		/// Stores the realized buttons.
		/// </summary>
		private KButton buttonFirst, buttonUp, buttonLast, buttonDown, buttonUnsub,
			buttonModify;
		private GameObject buttonManage;

		/// <summary>
		/// The button which last invoked the window.
		/// </summary>
		private KButton callingButton;

		/// <summary>
		/// The CallResult for handling the Steam API call to unsubscribe from a mod.
		/// </summary>
		private CallResult<RemoteStorageUnsubscribePublishedFileResult_t> unsubCaller;

		internal MoreModActions() {
			actionsScreen = null;
			buttonFirst = buttonUp = buttonLast = buttonDown = buttonUnsub = buttonModify =
				null;
			buttonManage = null;
			callingButton = null;
			unsubCaller = null;
		}

		/// <summary>
		/// Hides the mod actions popup.
		/// </summary>
		internal void HidePopup() {
			if (actionsScreen != null) {
				callingButton = null;
				actionsScreen.SetActive(false);
				actionsScreen.Index = -1;
				actionsScreen.Mod = null;
				// Prevent from being destroyed too early
				actionsScreen.transform.SetParent(transform);
			}
		}

		/// <summary>
		/// Makes a button with the specified name, icon, and action.
		/// </summary>
		/// <param name="name">The button name.</param>
		/// <param name="tooltip">The button tool tip.</param>
		/// <param name="sprite">The button sprite.</param>
		/// <param name="action">The method to invoke on click.</param>
		/// <param name="onRealize">The method to invoke on realize.</param>
		/// <returns>A button with that icon.</returns>
		private PButton MakeButton(string name, string tooltip, Sprite sprite, PUIDelegates.
				OnButtonPressed action, PUIDelegates.OnRealize onRealize) {
			return new PButton(name) {
				SpriteSize = ModDialogs.SPRITE_SIZE, Sprite = sprite, DynamicSize = false,
				OnClick = action, ToolTip = tooltip, Margin = DebugUtils.BUTTON_MARGIN
			}.SetKleiPinkStyle().AddOnRealize(onRealize);
		}

		protected override void OnCleanUp() {
			HidePopup();
			unsubCaller?.Dispose();
			if (actionsScreen != null)
				Destroy(actionsScreen.gameObject);
			actionsScreen = null;
			base.OnCleanUp();
		}

		/// <summary>
		/// Opens the Steam subscription or local mod folder.
		/// </summary>
		private void OnManage(GameObject _) {
			if (actionsScreen != null)
				actionsScreen.Mod?.on_managed();
		}

#if false
		/// <summary>
		/// Edits the mod on Steam if the current user is the owner.
		/// </summary>
		private void OnModify(GameObject _) {
			var mod = actionsScreen?.Mod;
			if (mod != null && mod.label.distribution_platform == Label.DistributionPlatform.
					Steam) {
				var editor = new ModEditDialog(gameObject, mod);
				if (editor.CanBegin())
					editor.CreateDialog();
				else
					editor.Dispose();
				HidePopup();
			}
		}
#endif

		/// <summary>
		/// Moves the active mod down 10 spaces.
		/// </summary>
		private void OnMoveDown(GameObject _) {
			var manager = Global.Instance.modManager;
			if (actionsScreen != null && manager != null) {
				int index = actionsScreen.Index;
				manager.Reinsert(index, Math.Min(manager.mods.Count, index + 10), false, manager);
			}
		}

		/// <summary>
		/// Moves the active mod to the top.
		/// </summary>
		private void OnMoveFirst(GameObject _) {
			var manager = Global.Instance.modManager;
			if (actionsScreen != null && manager != null)
				manager.Reinsert(actionsScreen.Index, 0, false, manager);
		}

		/// <summary>
		/// Moves the active mod to the bottom.
		/// </summary>
		private void OnMoveLast(GameObject _) {
			var manager = Global.Instance.modManager;
			if (actionsScreen != null && manager != null)
				manager.Reinsert(actionsScreen.Index, manager.mods.Count, true, manager);
		}

		/// <summary>
		/// Moves the active mod up 10 spaces.
		/// </summary>
		private void OnMoveUp(GameObject _) {
			var manager = Global.Instance.modManager;
			if (actionsScreen != null && manager != null) {
				int index = actionsScreen.Index;
				// Actually up 9 to account for the index change after removal
				manager.Reinsert(index, Math.Max(0, index - 9), false, manager);
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			// One long linear row
			var panel = new PPanel("MoreModActions") {
				BackColor = PUITuning.Colors.DialogDarkBackground, Spacing = 6,
				BackImage = PUITuning.Images.BoxBorder, ImageMode = Image.Type.Sliced,
				Direction = PanelDirection.Horizontal, Margin = new RectOffset(6, 6, 6, 6)
			}.AddChild(MakeButton("MoveToFirst", UI.TOOLTIPS.DNI_TOP,
				SpriteRegistry.GetTopIcon(), OnMoveFirst, (obj) =>
				buttonFirst = obj.GetComponent<KButton>()))
			.AddChild(MakeButton("MoveUpTen", UI.TOOLTIPS.DNI_UP,
				Assets.GetSprite("icon_priority_up_2"), OnMoveUp, (obj) =>
				buttonUp = obj.GetComponent<KButton>()))
			.AddChild(MakeButton("MoveDownTen", UI.TOOLTIPS.DNI_DOWN,
				Assets.GetSprite("icon_priority_down_2"), OnMoveDown, (obj) =>
				buttonDown = obj.GetComponent<KButton>()))
			.AddChild(MakeButton("MoveToLast", UI.TOOLTIPS.DNI_BOTTOM,
				SpriteRegistry.GetBottomIcon(), OnMoveLast, (obj) =>
				buttonLast = obj.GetComponent<KButton>()))
			.AddChild(new PButton("ManageMod") {
				Text = UI.MODSSCREEN.BUTTON_SUBSCRIPTION, DynamicSize = false,
				OnClick = OnManage, ToolTip = "Manage Mod", Margin = DebugUtils.BUTTON_MARGIN
			}.SetKleiBlueStyle().AddOnRealize((obj) => buttonManage = obj))
			.AddChild(new PButton("UnsubMod") {
				Text = UI.MODSSCREEN.BUTTON_UNSUB, DynamicSize = false,
				OnClick = OnUnsub, ToolTip = UI.TOOLTIPS.DNI_UNSUB, Margin = DebugUtils.
				BUTTON_MARGIN
			}.SetKleiBlueStyle().AddOnRealize((obj) => buttonUnsub = obj.
				GetComponent<KButton>()));
#if false
			panel.AddChild(new PButton("ModifyMod") {
				Text = UI.MODSSCREEN.BUTTON_MODIFY, DynamicSize = false,
				OnClick = OnModify, ToolTip = UI.TOOLTIPS.DNI_MODIFY, Margin = DebugUtils.
				BUTTON_MARGIN
			}.SetKleiPinkStyle().AddOnRealize((obj) => buttonModify = obj.
				GetComponent<KButton>()));
#endif
			var actionsObj = panel.AddTo(gameObject);
#if false
			PButton.SetButtonEnabled(buttonModify.gameObject, false);
#endif
			actionsObj.SetActive(false);
			// Blacklist from auto layout
			actionsObj.AddOrGet<LayoutElement>().ignoreLayout = true;
			PUIElements.SetAnchors(actionsObj, PUIAnchoring.End, PUIAnchoring.Center);
			unsubCaller = new CallResult<RemoteStorageUnsubscribePublishedFileResult_t>(
				OnUnsubComplete);
			actionsScreen = actionsObj.AddComponent<ModActionsScreen>();
			callingButton = null;
		}

		/// <summary>
		/// Unsubscribes from the active mod.
		/// </summary>
		private void OnUnsub(GameObject _) {
			var mod = actionsScreen?.Mod;
			if (mod != null && mod.label.distribution_platform == Label.DistributionPlatform.
					Steam && ulong.TryParse(mod.label.id, out ulong idLong) && !unsubCaller.
					IsActive()) {
				// Execute the unsubscribe
				var call = SteamUGC.UnsubscribeItem(new PublishedFileId_t(idLong));
				if (call.Equals(SteamAPICall_t.Invalid))
					OnUnsubFailed();
				else
					unsubCaller.Set(call);
				HidePopup();
			}
		}

		/// <summary>
		/// Called when an unsubscription completes.
		/// </summary>
		/// <param name="result">The unsubscribed mod information.</param>
		/// <param name="failed">Whether an I/O error occurred during the process.</param>
		private void OnUnsubComplete(RemoteStorageUnsubscribePublishedFileResult_t result,
				bool failed) {
			if (failed || result.m_eResult != EResult.k_EResultOK)
				OnUnsubFailed();
		}

		/// <summary>
		/// Shows a dialog when unsubscribing from a mod fails.
		/// </summary>
		private void OnUnsubFailed() {
			PUIElements.ShowMessageDialog(gameObject, UI.UNSUBFAILEDDIALOG.TEXT);
		}

		/// <summary>
		/// Shows the mod actions popup.
		/// </summary>
		/// <param name="button">The options button which invoked this popup.</param>
		/// <param name="index">The mod index in the list that is showing the popup.</param>
		private void ShowPopup(KButton button, int index) {
			var mods = Global.Instance.modManager?.mods;
			int n;
			if (mods != null && index >= 0 && index < (n = mods.Count)) {
				var mod = mods[index];
				bool isSteam = mod.label.distribution_platform == Label.DistributionPlatform.
					Steam;
				RectTransform rt = actionsScreen.rectTransform(), crt = button.rectTransform();
				actionsScreen.Index = index;
				actionsScreen.Mod = mod;
				// Update usability of each button
				if (buttonFirst != null)
					buttonFirst.isInteractable = index > 0;
				if (buttonUp != null)
					buttonUp.isInteractable = index > 0;
				if (buttonDown != null)
					buttonDown.isInteractable = index < n - 1;
				if (buttonLast != null)
					buttonLast.isInteractable = index < n - 1;
				if (buttonUnsub != null)
					buttonUnsub.isInteractable = isSteam;
				if (buttonManage != null) {
					PUIElements.SetToolTip(buttonManage, isSteam ? UI.MODSSCREEN.
						BUTTON_SUBSCRIPTION : UI.MODSSCREEN.BUTTON_LOCAL);
					PUIElements.SetToolTip(buttonManage, mod.manage_tooltip);
				}
				if (buttonModify != null)
					buttonModify.isInteractable = isSteam;
				actionsScreen.SetActive(true);
				// Resize to the proper size
				LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
				float w = LayoutUtility.GetPreferredWidth(rt), h = LayoutUtility.
					GetPreferredHeight(rt) * 0.5f, end = crt.offsetMin.x - 1.0f;
				rt.SetParent(crt.parent, false);
				rt.SetAsLastSibling();
				// Move it to the correct place
				rt.anchoredPosition = Vector2.zero;
				rt.anchorMin = DebugUtils.ANCHOR_MID_LEFT;
				rt.anchorMax = DebugUtils.ANCHOR_MID_LEFT;
				rt.offsetMin = new Vector2(end - w, -h);
				rt.offsetMax = new Vector2(end, h);
				callingButton = button;
			}
		}

		/// <summary>
		/// Shows or hides the mod actions popup.
		/// </summary>
		/// <param name="button">The options button which invoked this popup.</param>
		/// <param name="index">The mod index in the list that is showing the popup.</param>
		internal void TogglePopup(KButton button, int index) {
			if (actionsScreen != null) {
				if (actionsScreen.Mod == null)
					ShowPopup(button, index);
				else
					HidePopup();
			}
		}

		/// <summary>
		/// Called each frame by Unity, checks to see if the user clicks/scrolls outside of
		/// the dropdown while open, and closes it if so.
		/// </summary>
		internal void Update() {
			if (actionsScreen != null && (Input.GetMouseButton(0) || Input.GetAxis(
					"Mouse ScrollWheel") != 0.0f) && !actionsScreen.IsOver &&
					(callingButton == null || !callingButton.GetMouseOver))
				HidePopup();
		}
	}
}
