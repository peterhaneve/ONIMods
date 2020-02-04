/*
 * Copyright 2020 Peter Han
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

using Harmony;
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
		/// Creates a ConfigFileAttribute using an object from another mod.
		/// </summary>
		/// <param name="attr">The attribute from the other mod.</param>
		/// <returns>A ConfigFileAttribute object with the values from that object, where
		/// possible to retrieve; or null if none could be obtained.</returns>
		internal static ConfigFileAttribute CreateFrom(object attr) {
			ConfigFileAttribute cfa = null;
			if (attr.GetType().Name == typeof(ConfigFileAttribute).Name) {
				var trAttr = Traverse.Create(attr);
				string file = null;
				bool indent = false;
				// Log any errors from obtaining these values
				try {
					file = trAttr.GetProperty<string>(nameof(ConfigFileName));
					indent = trAttr.GetProperty<bool>(nameof(IndentOutput));
				} catch (Exception e) {
					PUtil.LogExcWarn(e);
				}
				// Remove invalid file names
				if (!PUtil.IsValidFileName(file))
					file = null;
				cfa = new ConfigFileAttribute(file, indent);
			}
			return cfa;
		}

		/// <summary>
		/// The configuration file name. If null, the default file name will be used.
		/// </summary>
		public string ConfigFileName { get; }

		/// <summary>
		/// Whether the output should be indented nicely. Defaults to false for smaller
		/// config files.
		/// </summary>
		public bool IndentOutput { get; }

		public ConfigFileAttribute(string FileName = POptions.CONFIG_FILE_NAME,
				bool IndentOutput = false) {
			ConfigFileName = FileName;
			this.IndentOutput = IndentOutput;
		}

		public override string ToString() {
			return ConfigFileName;
		}
	}
}
