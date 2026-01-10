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

using PeterHan.PLib.UI;
using UnityEngine;

namespace PeterHan.DebugNotIncluded {
#if ALL_MODS_CHECKBOX
	/// <summary>
	/// Added to the toggle all checkbox to manage its state.
	/// </summary>
	internal sealed class AllModsHandler : KMonoBehaviour {
		/// <summary>
		/// The "all mods" checkbox.
		/// </summary>
		internal GameObject checkbox;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		/// <summary>
		/// The current mods screen.
		/// </summary>
		[MyCmpReq]
		private ModsScreen instance;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// Triggered when the checkbox is clicked.
		/// </summary>
		/// <param name="state">The new state of the checkbox.</param>
		internal void OnClick(GameObject _, int state) {
			var manager = Global.Instance.modManager;
			var mods = manager?.mods;
			if (mods != null) {
				if (state == PCheckBox.STATE_CHECKED) {
					var thisMod = DebugNotIncludedPatches.ThisMod;
					// Disable all except DNI
					foreach (var mod in mods)
						if (thisMod == null || !mod.label.Match(thisMod.label))
							manager.EnableMod(mod.label, false, instance);
				} else
					// Enable all
					foreach (var mod in mods)
						manager.EnableMod(mod.label, true, instance);
				// Fire "external update" to rebuild the mods screen
				manager.on_update(manager);
			}
		}

		/// <summary>
		/// Updates the checkbox state based on the current mod selections.
		/// </summary>
		internal void UpdateCheckedState() {
			bool all = true, some = false;
			var mods = Global.Instance.modManager.mods;
			// Check to see if all, some, or none are enabled
			foreach (var mod in mods)
				if (mod.enabled)
					some = true;
				else
					all = false;
			// Apply to checkbox
			if (checkbox != null)
				PCheckBox.SetCheckState(checkbox, all ? PCheckBox.STATE_CHECKED : (some ?
					PCheckBox.STATE_PARTIAL : PCheckBox.STATE_UNCHECKED));
		}
	}
#endif
}
