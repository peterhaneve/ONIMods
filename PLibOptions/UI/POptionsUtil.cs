/*
 * Copyright 2021 Peter Han
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

namespace PeterHan.PLib {
	/// <summary>
	/// Static utility functions used across mods.
	/// 
	/// This is a cutdown version for PLib Options only.
	/// </summary>
	internal static class PUtil {
		/// <summary>
		/// The characters which are not allowed in file names.
		/// </summary>
		private static readonly HashSet<char> INVALID_FILE_CHARS;

		static PUtil() {
			INVALID_FILE_CHARS = new HashSet<char>(Path.GetInvalidFileNameChars());
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
		internal static bool IsValidFileName(string file) {
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
		internal static void LogDebug(object message) {
			Debug.LogFormat("[PLibOptions/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.Name,
				message);
		}

		/// <summary>
		/// Logs an error message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogError(object message) {
			Debug.LogErrorFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
		}

		/// <summary>
		/// Logs an exception message to the debug log.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		internal static void LogException(Exception thrown) {
			Debug.LogErrorFormat("[PLibOptions/{0}] {1} {2} {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs an exception message to the debug log at WARNING level.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		internal static void LogExcWarn(Exception thrown) {
			Debug.LogWarningFormat("[PLibOptions/{0}] {1} {2} {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		internal static void LogWarning(object message) {
			Debug.LogWarningFormat("[PLibOptions/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
		}
	}
}
