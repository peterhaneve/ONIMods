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
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Handles debugging existing mod loading.
	/// </summary>
	internal static class ModLoadHandler {
		/// <summary>
		/// The mod which caused the first unhandled crash. Clears the crash when accessed,
		/// allowing the next unhandled crash to again be logged.
		/// </summary>
		internal static ModDebugInfo CrashingMod {
			get {
				var mod = lastCrashedMod;
				lastCrashedMod = null;
				return mod;
			}
			set {
				if (value != null && lastCrashedMod == null)
					lastCrashedMod = value;
			}
		}

		/// <summary>
		/// The last mod which began loading content.
		/// </summary>
		internal static ModDebugInfo CurrentMod { get; set; }

		/// <summary>
		/// The title of the current mod.
		/// </summary>
		internal static string CurrentModTitle => CurrentMod?.ModName;

		/// <summary>
		/// The last mod which crashed, or null if none have / the crash has been cleared.
		/// </summary>
		private static ModDebugInfo lastCrashedMod;

		/// <summary>
		/// Handles an exception thrown when loading a mod.
		/// </summary>
		/// <param name="ex">The exception thrown.</param>
		internal static void HandleModException(object ex) {
			if (ex is Exception e) {
				string title = CurrentModTitle;
				if (!string.IsNullOrEmpty(title))
					DebugLogger.LogDebug("When loading mod {0}:", title);
				DebugLogger.LogException(e);
			}
		}

		/// <summary>
		/// Loads the assembly from the path just like Assembly.LoadFrom, but saves the mod
		/// associated with that assembly.
		/// </summary>
		/// <param name="path">The path to load.</param>
		/// <returns>The assembly loaded.</returns>
		internal static Assembly LoadAssembly(string path) {
			var assembly = string.IsNullOrEmpty(path) ? null : Assembly.LoadFrom(path);
			if (assembly != null && CurrentMod != null) {
#if DEBUG
				DebugLogger.LogDebug("Loaded assembly {0} for mod {1}", assembly.FullName,
					CurrentModTitle);
#endif
				ModDebugRegistry.Instance.RegisterModAssembly(assembly, CurrentMod);
			}
			return assembly;
		}

		/// <summary>
		/// Adds the assemblies in the loaded mod data to the active mod.
		/// </summary>
		/// <param name="data">The assemblies loaded.</param>
		internal static void LoadAssemblies(object data) {
			var dllList = data.GetType().GetFieldSafe("dlls", false);
			if (dllList != null && dllList.GetValue(data) is ICollection<Assembly> dlls)
				foreach (var assembly in dlls) {
#if DEBUG
					DebugLogger.LogDebug("Attributed assembly {0} to mod {1}", assembly.
						FullName, CurrentModTitle);
#endif
					ModDebugRegistry.Instance.RegisterModAssembly(assembly, CurrentMod);
				}
		}
	}
}
