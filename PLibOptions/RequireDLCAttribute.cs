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

using PeterHan.PLib.Core;
using System;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An attribute placed on an option property for a class used as mod options in order to
	/// show or hide it for particular DLCs. If the option is hidden, the value currently
	/// in the options file is preserved unchanged when reading or writing.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
	public sealed class RequireDLCAttribute : Attribute {
		/// <summary>
		/// The DLC ID to check.
		/// </summary>
		public string DlcID { get; }

		/// <summary>
		/// If true, the DLC is required, and the option is hidden if the DLC is inactive.
		/// If false, the DLC is forbidden, and the option is hidden if the DLC is active.
		/// </summary>
		public bool Required { get; }

		/// <summary>
		/// Annotates an option field as requiring the specified DLC. The [Option] attribute
		/// must also be present to be displayed at all.
		/// </summary>
		/// <param name="dlcID">The DLC ID to require. Must be one of:
		/// DlcManager.EXPANSION1_ID, DlcManager.VANILLA_ID</param>
		public RequireDLCAttribute(string dlcID) {
			DlcID = dlcID;
			Required = true;
		}

		/// <summary>
		/// Annotates an option field as requiring or forbidding the specified DLC. The
		/// [Option] attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="dlcID">The DLC ID to require or forbid. Must be one of:
		/// DlcManager.EXPANSION1_ID, DlcManager.VANILLA_ID</param>
		/// <param name="required">true to require the DLC, or false to forbid it.</param>
		public RequireDLCAttribute(string dlcID, bool required) {
			DlcID = dlcID ?? "";
			Required = required;
		}

		public override string ToString() {
			return "RequireDLC[DLC={0},require={1}]".F(DlcID, Required);
		}
	}
}
