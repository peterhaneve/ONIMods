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

using PeterHan.PLib.Core;
using System;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An attribute placed on an option property for a class used as mod options in order to
	/// show or hide it for particular game versions. If the option is hidden, the value
	/// currently in the options file is preserved unchanged when reading or writing.
	/// 
	/// This attribute can also be added to individual members of an Enum to filter the options
	/// shown by SelectOneOptionsEntry.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true,
		Inherited = true)]
	public sealed class RequireVersionAttribute : Attribute, IRequireFilter {
		/// <summary>
		/// The game version number (KleiVersion.ChangeList) to check.
		/// </summary>
		public uint Version { get; }

		/// <summary>
		/// If true, the option is shown for game versions greater than or equal to the
		/// required version. If false, the option is shown for game versions less than the
		/// required version.
		/// </summary>
		public bool Minimum { get; }

		/// <summary>
		/// Annotates an option field as requiring at least the specified game version.
		/// The [Option] attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="version">The minimum game version to require.</param>
		public RequireVersionAttribute(uint version) {
			Version = version;
			Minimum = true;
		}

		/// <summary>
		/// Annotates an option field as requiring the specified game version.
		/// The [Option] attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="version">The minimum or maximum game version to require.</param>
		/// <param name="minimum">true if the version is the minimum game version, or false if
		/// the version is the maximum game version (exclusive).</param>
		public RequireVersionAttribute(uint version, bool minimum) {
			Version = version;
			Minimum = minimum;
		}
		
		public bool Filter() {
			return (PUtil.GameVersion >= Version) == Minimum;
		}

		public override string ToString() {
			return "RequireVersion[version={0},minimum={1}]".F(Version, Minimum);
		}
	}
}
