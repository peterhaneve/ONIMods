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

using System;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An attribute placed on an options class only (will not function on a member property)
	/// which denotes the config file name to use for that mod, and allows save/load options
	/// to be set.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class ConfigFileAttribute : Attribute {
		/// <summary>
		/// The configuration file name. If null, the default file name will be used.
		/// </summary>
		public string ConfigFileName { get; }

		/// <summary>
		/// Whether the output should be indented nicely. Defaults to false for smaller
		/// config files.
		/// </summary>
		public bool IndentOutput { get; }

		/// <summary>
		/// If true, the config file will be moved from the mod folder to a folder in the
		/// config directory shared across mods. This change preserves the mod configuration
		/// across updates, but may not be cleared when the mod is uninstalled. Use with
		/// caution.
		/// </summary>
		public bool UseSharedConfigLocation { get; }

		public ConfigFileAttribute(string FileName = POptions.CONFIG_FILE_NAME,
				bool IndentOutput = false, bool SharedConfigLocation = false) {
			ConfigFileName = FileName;
			this.IndentOutput = IndentOutput;
			UseSharedConfigLocation = SharedConfigLocation;
		}

		public override string ToString() {
			return ConfigFileName;
		}
	}
}
