using System;
using System.Collections.Generic;
using System.Text;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// The options available for Settings Change Tool.
	/// </summary>
	public sealed class BulkChangeOptions {
		public int DefaultIndex { get; }

		public bool EnableMod { get; }

		public enum ToolModes {
			EnableBuilding, DisableBuilding, None
		}
	}
}
