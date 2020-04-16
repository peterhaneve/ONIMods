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
using KMod;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Stores debugging information about a mod.
	/// </summary>
	public sealed class ModDebugInfo {
		/// <summary>
		/// Gets the identifier used to uniquely identify mods. Also used as the harmony
		/// identifier for each mod.
		/// </summary>
		/// <param name="mod">The mod to identify.</param>
		/// <returns>The unique identifier for that mod.</returns>
		public static string GetIdentifier(Mod mod) {
			return (mod == null) ? "" : mod.label.id + "." + mod.label.distribution_platform;
		}

		/// <summary>
		/// The Harmony identifier which would be used for this mod's annotation patches.
		/// </summary>
		public string HarmonyIdentifier { get; internal set; }

		/// <summary>
		/// The Klei mod information.
		/// </summary>
		public Mod Mod { get; }

		/// <summary>
		/// The assemblies loaded by this mod. Since the assemblies all have a ref in the
		/// current appdomain until restarted anyways, this will not leak any more memory
		/// than is already done.
		/// </summary>
		public ICollection<Assembly> ModAssemblies { get; }

		/// <summary>
		/// The display name for this mod.
		/// </summary>
		public string ModName { get; }

		/// <summary>
		/// Creates a new mod debug information object.
		/// </summary>
		/// <param name="mod">The mod which this object represents.</param>
		internal ModDebugInfo(Mod mod) {
			Mod = mod ?? throw new ArgumentNullException("mod");
			ModAssemblies = new HashSet<Assembly>();
			ModName = mod.title ?? "unknown";
			HarmonyIdentifier = GetIdentifier(mod);
		}

		public override bool Equals(object obj) {
			return obj is ModDebugInfo other && Mod.label.Match(other.Mod.label);
		}

		public override int GetHashCode() {
			return Mod.label.id.GetHashCode();
		}

		public override string ToString() {
			return ModName;
		}
	}
}
