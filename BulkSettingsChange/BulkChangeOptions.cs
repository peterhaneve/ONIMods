using System;
using System.Collections.Generic;
using System.Text;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// The options available for Settings Change Tool.
	/// 
	/// This is a test class for PLib Options. Not currently used!
	/// </summary>
	public sealed class BulkChangeOptions {
		public int DefaultIndex { get; }

		public bool EnableMod { get; }

		public enum ToolModes {
			EnableBuilding, DisableBuilding, None
		}
	}
}
