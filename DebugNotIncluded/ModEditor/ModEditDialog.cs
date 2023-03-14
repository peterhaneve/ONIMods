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

using PeterHan.PLib.UI;
using System;
using UnityEngine;

using UI = PeterHan.DebugNotIncluded.DebugNotIncludedStrings.UI;

#if false
namespace PeterHan.DebugNotIncluded {
	internal sealed class ModEditDialog : IDisposable {
		private static void ToggleCheckbox(GameObject checkbox, int state) {
			PCheckBox.SetCheckState(checkbox, state == PCheckBox.STATE_CHECKED ? PCheckBox.
				STATE_UNCHECKED : PCheckBox.STATE_CHECKED);
		}

		private GameObject dataPathField;

		private GameObject descriptionField;

		private GameObject doUpdateData;

		private GameObject doUpdateImg;

		private readonly ModEditor editor;

		private GameObject imagePathField;

		private readonly KMod.Mod mod;

		private readonly GameObject parent;

		private GameObject patchNotesField;

		private GameObject titleField;

		internal ModEditDialog(GameObject parent, KMod.Mod target) {
			if (target == null)
				throw new ArgumentNullException("target");
			if (target.label.distribution_platform != KMod.Label.DistributionPlatform.Steam)
				throw new ArgumentException("Only works on Steam mods");
			mod = target;
			editor = new ModEditor(target);
			editor.OnModifyComplete += OnModifyComplete;
			editor.OnModifyFailed += OnModifyFailed;
			this.parent = parent;
		}

		internal bool CanBegin() {
			editor.PresetFields();
			return editor.CanBegin();
		}

		public void Dispose() {
			editor.Dispose();
		}

		private static IUIComponent CheckGroup(IUIComponent check, IUIComponent rest) {
			return new PRelativePanel("Align") {
				FlexSize = Vector2.right
			}.AddChild(check).AddChild(rest).AnchorYAxis(check, 0.5f).AnchorYAxis(rest, 0.5f).
				SetLeftEdge(check, fraction: 0.0f).SetLeftEdge(rest, toRight: check).
				SetRightEdge(rest, fraction: 1.0f).SetMargin(check,
				new RectOffset(0, 5, 0, 0));
		}

		internal void CreateDialog() {
			var dialog = new PDialog("ModifyItem") {
				Title = string.Format(UI.MODIFYDIALOG.TITLE, mod.label.title.ToUpper()),
				DialogClosed = OnDialogClosed, SortKey = 200.0f, Parent = parent
			}.AddButton("ok", UI.MODIFYDIALOG.OK, null, PUITuning.Colors.ButtonPinkStyle).
				AddButton("close", UI.MODIFYDIALOG.CANCEL, null, PUITuning.Colors.
				ButtonBlueStyle);
			var body = new PGridPanel("ModifyBody") {
				Margin = new RectOffset(10, 10, 10, 10)
			}.AddColumn(new GridColumnSpec()).AddColumn(new GridColumnSpec(0.0f, 1.0f));
			body.AddRow(UI.MODIFYDIALOG.CAPTION, new PTextField("Title") {
				Text = editor.Title, MaxLength = 127, MinWidth = 512, BackColor =
				PUITuning.Colors.DialogDarkBackground, TextStyle = PUITuning.Fonts.
				TextLightStyle, TextAlignment = TMPro.TextAlignmentOptions.Left
			}.AddOnRealize((obj) => titleField = obj));
			body.AddRow(UI.MODIFYDIALOG.DESC, new PTextArea("Description") {
				LineCount = 8, Text = editor.Description, MaxLength = 7999,
				MinWidth = 512, BackColor = PUITuning.Colors.DialogDarkBackground,
				TextStyle = PUITuning.Fonts.TextLightStyle
			}.AddOnRealize((obj) => descriptionField = obj));
			body.AddRow(UI.MODIFYDIALOG.IMAGE_PATH, CheckGroup(new PCheckBox("UpdateImage") {
				CheckSize = new Vector2(16.0f, 16.0f), OnChecked = ToggleCheckbox, BackColor =
				PUITuning.Colors.DialogDarkBackground, CheckColor = PUITuning.Colors.
				ComponentDarkStyle
			}.AddOnRealize((obj) => doUpdateImg = obj), new PTextField("PreviewPath") {
				Text = editor.PreviewPath, MaxLength = 512, MinWidth = 512, BackColor =
				PUITuning.Colors.DialogDarkBackground, TextStyle = PUITuning.Fonts.
				TextLightStyle, TextAlignment = TMPro.TextAlignmentOptions.Left
			}.AddOnRealize((obj) => imagePathField = obj)));
			body.AddRow(UI.MODIFYDIALOG.DATA_PATH, CheckGroup(new PCheckBox("UpdateData") {
				CheckSize = new Vector2(16.0f, 16.0f), OnChecked = ToggleCheckbox, BackColor =
				PUITuning.Colors.DialogDarkBackground, CheckColor = PUITuning.Colors.
				ComponentDarkStyle
			}.AddOnRealize((obj) => doUpdateData = obj), new PTextField("DataPath") {
				Text = editor.DataPath, MaxLength = 512, MinWidth = 512, BackColor =
				PUITuning.Colors.DialogDarkBackground, TextStyle = PUITuning.Fonts.
				TextLightStyle, TextAlignment = TMPro.TextAlignmentOptions.Left
			}.AddOnRealize((obj) => dataPathField = obj)));
			body.AddRow(UI.MODIFYDIALOG.PATCHNOTES, new PTextField("PatchNotes") {
				Text = editor.PatchInfo, MaxLength = 512, MinWidth = 512,
				BackColor = PUITuning.Colors.DialogDarkBackground, TextStyle = PUITuning.Fonts.
				TextLightStyle, TextAlignment = TMPro.TextAlignmentOptions.Left
			}.AddOnRealize((obj) => patchNotesField = obj));
			dialog.Body.AddChild(body);
			dialog.Show();
		}

		private void OnDialogClosed(string option) {
			if (option == "ok") {
				editor.Description = PTextField.GetText(descriptionField);
				editor.PatchInfo = PTextField.GetText(patchNotesField);
				editor.Title = PTextField.GetText(titleField);
				editor.DataPath = PTextField.GetText(dataPathField);
				editor.UpdateData = PCheckBox.GetCheckState(doUpdateData) == PCheckBox.
					STATE_CHECKED;
				editor.PreviewPath = PTextField.GetText(imagePathField);
				editor.UpdatePreview = PCheckBox.GetCheckState(doUpdateImg) == PCheckBox.
					STATE_CHECKED;
				editor.StartModify();
			} else
				Dispose();
		}

		private void OnModifyComplete() {
			PUIElements.ShowMessageDialog(parent, string.Format(UI.MODIFYDIALOG.SUCCESS,
				mod.label.title));
			Dispose();
		}

		private void OnModifyFailed() {
			PUIElements.ShowMessageDialog(parent, string.Format(UI.MODIFYFAILEDDIALOG.TEXT,
				mod.label.title));
			Dispose();
		}
	}
}
#endif
