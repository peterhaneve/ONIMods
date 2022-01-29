/*
 * Copyright 2022 Peter Han
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
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReimaginationTeam.Reimagination {
	/// <summary>
	/// The Reimagination Loader uses PLib to compile a list of loaded ReImagination mods on
	/// the running instance.
	/// </summary>
	public sealed class ImaginationLoader {
		/// <summary>
		/// The world name key for the Final Destination.
		/// </summary>
		public const string FINALDEST_KEY = "STRINGS.WORLDS.FINAL_DESTINATION.NAME";

		/// <summary>
		/// The registry key for the Imagination mod table.
		/// </summary>
		private const string IMAGINATION_TABLE = "Imagination.Table";

		/// <summary>
		/// Initializes the Imagination Loader. PLib will be initialized if not already done.
		/// </summary>
		/// <param name="rootType">The Type that will be used as the base for this Imagination
		/// mod's assembly. The namespace for that Type will be registered as the name for
		/// other Imagination mods to see.</param>
		public static void Init(Type rootType) {
			if (rootType == null)
				throw new ArgumentNullException("rootType");
			var asm = Assembly.GetExecutingAssembly();
			PUtil.InitLibrary();
			// Create imagination mod table
			var imag = PSharedData.GetData<IDictionary<string, Assembly>>(IMAGINATION_TABLE);
			if (imag == null)
				PSharedData.PutData(IMAGINATION_TABLE, imag = new Dictionary<string,
					Assembly>(8));
			var rootNS = rootType.Namespace;
			if (imag.ContainsKey(rootNS))
				PUtil.LogWarning("Reimagination mod {0} is loaded more than once. This may " +
					"cause severe problems!".F(rootType.FullName));
			else {
				imag.Add(rootNS, asm);
				PUtil.LogDebug("Imagination Loader registered mod ID: " + rootNS);
			}
		}

		/// <summary>
		/// Checks to see if the (unreleased) Final Destination mod is the current asteroid.
		/// Only valid after Game is loaded.
		/// </summary>
		/// <returns>true if the current asteroid is Final Destination, or false otherwise.</returns>
		public static bool IsFinalDestination() {
			var loader = SaveLoader.Instance;
#if DEBUG
			if (loader == null)
				PUtil.LogWarning("IsFinalDestination() called before save loaded!");
#endif
			return loader?.worldGen?.Settings?.world?.name == FINALDEST_KEY;
		}

		/// <summary>
		/// Reports whether another Reimagination mod with the specified mod ID is loaded.
		/// Only usable in postload patches or later.
		/// </summary>
		/// <param name="modID">The mod ID to check, typically the namespace of its root
		/// type.</param>
		/// <returns>true if that mod has been loaded and called Init(), or false otherwise.</returns>
		public static bool IsModLoaded(string modID) {
			var imag = PSharedData.GetData<IDictionary<string, Assembly>>(IMAGINATION_TABLE);
			return imag != null && imag.ContainsKey(modID);
		}
	}
}
