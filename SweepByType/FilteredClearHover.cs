/*
 * Copyright 2024 Peter Han
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

using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.SweepByType {
	/// <summary>
	/// The hover text shown when filtered sweep is invoked.
	/// </summary>
	internal sealed class FilteredClearHover : HoverTextConfiguration {
		protected override void OnSpawn() {
			var ct = ClearTool.Instance;
			base.OnSpawn();
			// Take the text configuration from the existing sweep tool's hover text
			if (ct != null && ct.TryGetComponent(out HoverTextConfiguration template)) {
				Styles_BodyText = template.Styles_BodyText;
				Styles_Instruction = template.Styles_Instruction;
				Styles_Title = template.Styles_Title;
				Styles_Values = template.Styles_Values;
			}
		}

		/// <summary>
		/// Updates the hover card text.
		/// </summary>
		/// <param name="selected">The objects under the cursor.</param>
		public override void UpdateHoverElements(List<KSelectable> selected) {
			var ts = FilteredClearTool.Instance.TypeSelect;
			var hoverInstance = HoverTextScreen.Instance;
			// Determine if in default Sweep All mode
			bool all = ts.IsAllSelected;
			int cell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(KInputManager.
				GetMousePos()));
			// Draw the tool title
			string titleStr = all ? STRINGS.UI.TOOLS.MARKFORSTORAGE.TOOLNAME.ToString() :
				SweepByTypeStrings.TOOL_NAME_FILTERED.ToString().ToUpper();
			var drawer = hoverInstance.BeginDrawing();
			drawer.BeginShadowBar();
			drawer.DrawText(titleStr.ToUpper(), ToolTitleTextStyle);
			// Draw the instructions
			ActionName = all ? STRINGS.UI.TOOLS.MARKFORSTORAGE.TOOLACTION.text :
				SweepByTypeStrings.TOOLTIP_FILTERED.text;
			DrawInstructions(hoverInstance, drawer);
			drawer.EndShadowBar();
			if (selected != null && Grid.IsValidCell(cell) && Grid.IsVisible(cell))
				DrawPickupText(selected, drawer, hoverInstance.GetSprite("dash"));
			drawer.EndDrawing();
		}

		/// <summary>
		/// Draws tool tips for selectable items in the sweep area.
		/// </summary>
		/// <param name="selected">The items which were found.</param>
		/// <param name="drawer">The renderer for hover card text.</param>
		/// <param name="dash">The dash sprite.</param>
		private void DrawPickupText(IEnumerable<KSelectable> selected, HoverTextDrawer drawer,
				Sprite dash) {
			// For each pickupable object, show the type
			foreach (var selectable in selected)
				// Ignore Duplicants
				if (selectable.TryGetComponent(out Clearable cc) && cc.isClearable &&
						selectable.TryGetComponent(out PrimaryElement ec) &&
						!selectable.TryGetComponent(out MinionIdentity _)) {
					drawer.BeginShadowBar();
					// Element name (uppercase)
					drawer.DrawText(GameUtil.GetUnitFormattedName(selectable.gameObject, true),
						Styles_Title.Standard);
					drawer.NewLine();
					drawer.DrawIcon(dash);
					// Mass (kg, g, mg...)
					drawer.DrawText(GameUtil.GetFormattedMass(ec.Mass), Styles_Values.Property.
						Standard);
					drawer.NewLine();
					drawer.DrawIcon(dash);
					// Temperature
					drawer.DrawText(GameUtil.GetFormattedTemperature(ec.Temperature),
						Styles_BodyText.Standard);
					drawer.EndShadowBar();
				}
		}
	}
}
