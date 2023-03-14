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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Static utility functions used across mods.
	/// </summary>
	public static class PUtil {
		/// <summary>
		/// Retrieves the current changelist version of the game. LU-371502 has a version of
		/// 371502u.
		/// 
		/// If the version cannot be determined, returns 0.
		/// </summary>
		public static uint GameVersion { get; }

		/// <summary>
		/// Whether PLib has been initialized.
		/// </summary>
		private static volatile bool initialized;

		/// <summary>
		/// Serializes attempts to initialize PLib.
		/// </summary>
		private static readonly object initializeLock;

		/// <summary>
		/// The characters which are not allowed in file names.
		/// </summary>
		private static readonly HashSet<char> INVALID_FILE_CHARS;

		static PUtil() {
			initialized = false;
			initializeLock = new object();
			INVALID_FILE_CHARS = new HashSet<char>(Path.GetInvalidFileNameChars());
			GameVersion = GetGameVersion();
		}

		/// <summary>
		/// Generates a mapping of assembly names to Mod instances. Only works after all mods
		/// have been loaded.
		/// </summary>
		/// <returns>A mapping from assemblies to the Mod instance that owns them.</returns>
		public static IDictionary<Assembly, KMod.Mod> CreateAssemblyToModTable() {
			var allMods = Global.Instance?.modManager?.mods;
			var result = new Dictionary<Assembly, KMod.Mod>(32);
			if (allMods != null)
				foreach (var mod in allMods) {
					var dlls = mod?.loaded_mod_data?.dlls;
					if (dlls != null)
						foreach (var assembly in dlls)
							result[assembly] = mod;
				}
			return result;
		}

		/// <summary>
		/// Finds the distance between two points.
		/// </summary>
		/// <param name="x1">The first X coordinate.</param>
		/// <param name="y1">The first Y coordinate.</param>
		/// <param name="x2">The second X coordinate.</param>
		/// <param name="y2">The second Y coordinate.</param>
		/// <returns>The non-taxicab (straight line) distance between the points.</returns>
		public static float Distance(float x1, float y1, float x2, float y2) {
			float dx = x2 - x1, dy = y2 - y1;
			return Mathf.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// Finds the distance between two points.
		/// </summary>
		/// <param name="x1">The first X coordinate.</param>
		/// <param name="y1">The first Y coordinate.</param>
		/// <param name="x2">The second X coordinate.</param>
		/// <param name="y2">The second Y coordinate.</param>
		/// <returns>The non-taxicab (straight line) distance between the points.</returns>
		public static double Distance(double x1, double y1, double x2, double y2) {
			double dx = x2 - x1, dy = y2 - y1;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// Retrieves the current game version from the Klei code.
		/// </summary>
		/// <returns>The change list version of the game, or 0 if it cannot be determined.</returns>
		private static uint GetGameVersion() {
			/*
			 * KleiVersion.ChangeList is a const which is substituted at compile time; if
			 * accessed directly, PLib would have a version "baked in" and would never
			 * update depending on the game version in use.
			 */
			var field = PPatchTools.GetFieldSafe(typeof(KleiVersion), nameof(KleiVersion.
				ChangeList), true);
			uint ver = 0U;
			if (field != null && field.GetValue(null) is uint newVer)
				ver = newVer;
			return ver;
		}

		/// <summary>
		/// Retrieves the mod directory for the specified assembly. If an archived version is
		/// running, the path to that version is reported.
		/// </summary>
		/// <param name="modDLL">The assembly used for a mod.</param>
		/// <returns>The directory where the mod is currently executing.</returns>
		public static string GetModPath(Assembly modDLL) {
			if (modDLL == null)
				throw new ArgumentNullException(nameof(modDLL));
			string dir = null;
			try {
				dir = Directory.GetParent(modDLL.Location)?.FullName;
			} catch (NotSupportedException e) {
				// Guess from the Klei strings
				LogExcWarn(e);
			} catch (System.Security.SecurityException e) {
				// Guess from the Klei strings
				LogExcWarn(e);
			} catch (IOException e) {
				// Guess from the Klei strings
				LogExcWarn(e);
			}
			if (dir == null)
				dir = Path.Combine(KMod.Manager.GetDirectory(), modDLL.GetName()?.Name ?? "");
			return dir;
		}

		/// <summary>
		/// Initializes PLib. While most components are initialized dynamically if used, some
		/// key infrastructure must be initialized first.
		/// </summary>
		/// <param name="logVersion">If true, the mod name and version is emitted to the log.</param>
		public static void InitLibrary(bool logVersion = true) {
			var assembly = Assembly.GetCallingAssembly();
			lock (initializeLock) {
				if (!initialized) {
					initialized = true;
					if (assembly != null && logVersion)
						Debug.LogFormat("[PLib] Mod {0} initialized, version {1}",
							assembly.GetNameSafe(), assembly.GetFileVersion() ?? "Unknown");
				}
			}
		}

		/// <summary>
		/// Returns true if the file is a valid file name. If the argument contains path
		/// separator characters, this method returns false, since that is not a valid file
		/// name.
		/// 
		/// Null and empty file names are not valid file names.
		/// </summary>
		/// <param name="file">The file name to check.</param>
		/// <returns>true if the name could be used to name a file, or false otherwise.</returns>
		public static bool IsValidFileName(string file) {
			bool valid = (file != null);
			if (valid) {
				// Cannot contain characters in INVALID_FILE_CHARS
				int len = file.Length;
				for (int i = 0; i < len && valid; i++)
					if (INVALID_FILE_CHARS.Contains(file[i]))
						valid = false;
			}
			return valid;
		}

		/// <summary>
		/// Logs a message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogDebug(object message) {
			Debug.LogFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly().GetNameSafe(),
				message);
		}

		/// <summary>
		/// Logs an error message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogError(object message) {
			// Cannot make a utility property or method for Assembly.GetCalling... because
			// its caller would then be the assembly PLib is in, not the assembly which
			// invoked LogXXX
			Debug.LogErrorFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly().
				GetNameSafe() ?? "?", message);
		}

		/// <summary>
		/// Logs an exception message to the debug log.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		public static void LogException(Exception thrown) {
			Debug.LogErrorFormat("[PLib/{0}] {1} {2} {3}", Assembly.GetCallingAssembly().
				GetNameSafe() ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs an exception message to the debug log at WARNING level.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		public static void LogExcWarn(Exception thrown) {
			Debug.LogWarningFormat("[PLib/{0}] {1} {2} {3}", Assembly.GetCallingAssembly().
				GetNameSafe() ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogWarning(object message) {
			Debug.LogWarningFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly().
				GetNameSafe() ?? "?", message);
		}

		/// <summary>
		/// Measures how long the specified code takes to run. The result is logged to the
		/// debug log in microseconds.
		/// </summary>
		/// <param name="code">The code to execute.</param>
		/// <param name="header">The name used in the log to describe this code.</param>
		public static void Time(System.Action code, string header = "Code") {
			if (code == null)
				throw new ArgumentNullException(nameof(code));
			var watch = new System.Diagnostics.Stopwatch();
			watch.Start();
			code.Invoke();
			watch.Stop();
			LogDebug("{1} took {0:D} us".F(watch.ElapsedTicks * 1000000L / System.Diagnostics.
				Stopwatch.Frequency, header));
		}
	}
}
