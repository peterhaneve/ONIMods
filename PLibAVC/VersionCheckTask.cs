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

using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Represents a "task" to check a particular mod for updates.
	/// </summary>
	internal sealed class VersionCheckTask {
		/// <summary>
		/// The method which will be used to check.
		/// </summary>
		private readonly IModVersionChecker method;

		/// <summary>
		/// The mod whose version will be checked.
		/// </summary>
		private readonly KMod.Mod mod;

		/// <summary>
		/// The next task to run when the check completes, or null to not run any task.
		/// </summary>
		internal System.Action Next { get; set; }

		/// <summary>
		/// The location where the outcome of mod version checking will be stored.
		/// </summary>
		private readonly ConcurrentDictionary<string, ModVersionCheckResults> results;

		internal VersionCheckTask(KMod.Mod mod, IModVersionChecker method,
				ConcurrentDictionary<string, ModVersionCheckResults> results) {
			this.mod = mod ?? throw new ArgumentNullException(nameof(mod));
			this.method = method ?? throw new ArgumentNullException(nameof(method));
			this.results = results ?? throw new ArgumentNullException(nameof(results));
			Next = null;
		}

		/// <summary>
		/// Records the result of the mod version check, and runs the next checker in
		/// line, from this mod or a different one.
		/// </summary>
		/// <param name="result">The results from the version check.</param>
		private void OnComplete(ModVersionCheckResults result) {
			method.OnVersionCheckCompleted -= OnComplete;
			if (result != null) {
				results.TryAdd(result.ModChecked, result);
				if (!result.IsUpToDate)
					PUtil.LogWarning("Mod {0} is out of date! New version: {1}".F(result.
						ModChecked, result.NewVersion ?? "unknown"));
				else {
#if DEBUG
					PUtil.LogDebug("Mod {0} is up to date".F(result.ModChecked));
#endif
				}
			} else
				RunNext();
		}

		/// <summary>
		/// Runs the version check, and registers a callback to run the next one if
		/// it is not null.
		/// </summary>
		internal void Run() {
			if (results.ContainsKey(mod.staticID))
				RunNext();
			else {
				bool run = false;
				method.OnVersionCheckCompleted += OnComplete;
				// Version check errors should not crash the game
				try {
					run = method.CheckVersion(mod);
				} catch (Exception e) {
					PUtil.LogWarning("Unable to check version for mod " + mod.label.title + ":");
					PUtil.LogExcWarn(e);
					run = false;
				}
				if (!run) {
					method.OnVersionCheckCompleted -= OnComplete;
					RunNext();
				}
			}
		}

		/// <summary>
		/// Runs the next version check.
		/// </summary>
		private void RunNext() {
			Next?.Invoke();
		}
	}
}
