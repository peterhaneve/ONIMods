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
using PeterHan.PLib.Options;

namespace PeterHan.OldPipeColor {
	/// <summary>
	/// Patches which will be applied via annotations for Custom Pipe Colors.
	/// </summary>
	public static class OldPipeColorPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
			PUtil.RegisterPatchClass(typeof(OldPipeColorPatches));
			POptions.RegisterOptions(typeof(OldPipeColorOptions));
		}

		/// <summary>
		/// Run at game start to alter the pipe colors to the configured colors. The active
		/// ColorSet is a reference to the options array so changes copy through.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			var colorOptions = POptions.ReadSettingsForAssembly<OldPipeColorOptions>() ??
				new OldPipeColorOptions();
			// 0 is the default
			var options = GlobalAssets.Instance.colorSetOptions[0];
			options.conduitInsulated = colorOptions.InsulatedColor;
			options.conduitNormal = colorOptions.NormalColor;
			options.conduitRadiant = colorOptions.RadiantColor;
			options.RefreshLookup();
		}
	}
}
