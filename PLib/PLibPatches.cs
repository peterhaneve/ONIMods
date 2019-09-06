/*
 * Copyright 2019 Peter Han
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
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.PLib {
	/// <summary>
	/// All patches for PLib are stored here and only applied once for all PLib mods loaded.
	/// </summary>
	sealed class PLibPatches {
		#region Patches

#pragma warning disable IDE0051 // Remove unused private members

		/// <summary>
		/// Applied after BuildDisplay runs.
		/// </summary>
		private static void BuildDisplay_Postfix(ref InputBindingsScreen __instance,
				ref List<string> ___screens, int ___activeScreen) {
			KeyBindingManager.BuildDisplay(__instance, ___screens[___activeScreen]);
		}

		/// <summary>
		/// Applies all patches.
		/// </summary>
		/// <param name="instance">The Harmony instance to use when patching.</param>
		private static void PatchAll(HarmonyInstance instance) {
			if (instance == null)
				throw new ArgumentNullException("instance");
#if false
			instance.Patch(typeof(InputBindingsScreen), "BuildDisplay", null,
				PatchMethod("BuildDisplay_Postfix"));
#endif
		}

#pragma warning restore IDE0051 // Remove unused private members

#endregion

#region Infrastructure

		/// <summary>
		/// Returns a patch method from this class. It must be static.
		/// </summary>
		/// <param name="name">The patch method name.</param>
		/// <returns>The matching method.</returns>
		private static HarmonyMethod PatchMethod(string name) {
			return new HarmonyMethod(typeof(PLibPatches).GetMethod(name, BindingFlags.
				NonPublic | BindingFlags.Static));
		}

		/// <summary>
		/// The version of PLib that these patches will represent.
		/// </summary>
		public string MyVersion { get; }

		public PLibPatches() {
			MyVersion = PVersion.VERSION;
		}

		/// <summary>
		/// Applies the patches for this version of PLib.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		public void Apply(HarmonyInstance instance) {
			PRegistry.LogPatchDebug("Using version " + MyVersion);
			PatchAll(instance);
		}

		public override bool Equals(object obj) {
			return obj is PLibPatches other && other.MyVersion == MyVersion;
		}

		public override int GetHashCode() {
			return MyVersion.GetHashCode();
		}

		public override string ToString() {
			return "PLibPatches version " + MyVersion;
		}

#endregion
	}
}
