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
using System.Collections.Generic;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An attribute placed on an option property for a class used as mod options in order to
	/// show or hide it for particular active mods. If the option is hidden, the value
	/// currently in the options file is preserved unchanged when reading or writing.
	/// 
	/// This attribute can also be added to individual members of an Enum to filter the options
	/// shown by SelectOneOptionsEntry.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true,
		Inherited = true)]
	public sealed class RequireModAttribute : Attribute, IRequireFilter {
		/// <summary>
		/// Caches the list of active mods. Technically created once for each mod using
		/// RequireModAttribute, but trying to share the list through a forwarded component
		/// creates another API surface that might break.
		/// </summary>
		private static readonly ISet<string> ACTIVE_MODS = new HashSet<string>();

		/// <summary>
		/// Checks to see if a mod is active.
		/// </summary>
		/// <param name="staticID">The mod static ID to query.</param>
		/// <returns>true if the mod is installed and active, or false otherwise.</returns>
		private static bool IsModActive(string staticID) {
			var mods = ACTIVE_MODS;
			lock (mods) {
				if (mods.Count <= 0) {
					// Since this code is running, at least one mod MUST be active!
					var allMods = Global.Instance.modManager.mods;
					int n = allMods.Count;
					for (int i = 0; i < n; i++) {
						var mod = allMods[i];
						if (mod.IsActive())
							mods.Add(mod.staticID);
					}
				}
				return mods.Contains(staticID);
			}
		}

		/// <summary>
		/// The mod static ID to check.
		/// </summary>
		public string StaticID { get; }

		/// <summary>
		/// If true, the mod is required, and the option is hidden if the mod is inactive.
		/// If false, the mod is forbidden, and the option is hidden if the mod is active.
		/// </summary>
		public bool Required { get; }

		/// <summary>
		/// Annotates an option field as requiring the specified mod to be active and
		/// installed. The [Option] attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="staticID">The mod ID to require.</param>
		public RequireModAttribute(string staticID) {
			StaticID = staticID ?? "";
			Required = true;
		}

		/// <summary>
		/// Annotates an option field as requiring or forbidding the specified mod to be active
		/// and installed. The [Option] attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="staticID">The mod ID to require or forbid.</param>
		/// <param name="required">true to require the mod, or false to forbid it.</param>
		public RequireModAttribute(string staticID, bool required) {
			StaticID = staticID ?? "";
			Required = required;
		}
		
		public bool Filter() {
			return IsModActive(StaticID) == Required;
		}

		public override string ToString() {
			return "RequireMod[StaticID={0},require={1}]".F(StaticID, Required);
		}
	}
}
