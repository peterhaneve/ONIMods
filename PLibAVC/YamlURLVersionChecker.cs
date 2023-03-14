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

using Klei;
using KMod;
using PeterHan.PLib.Core;
using System;
using UnityEngine.Networking;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Checks the mod version using a URL to a YAML file. The file at this URL must resolve
	/// to a YAML file of the same format as the mod_info.yaml class.
	/// </summary>
	public sealed class YamlURLVersionChecker : IModVersionChecker {
		/// <summary>
		/// The URL to query for checking the mod version.
		/// </summary>
		public string YamlVersionURL { get; }

		public event PVersionCheck.OnVersionCheckComplete OnVersionCheckCompleted;

		public YamlURLVersionChecker(string url) {
			if (string.IsNullOrEmpty(url))
				throw new ArgumentNullException(nameof(url));
			YamlVersionURL = url;
		}

		public bool CheckVersion(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			var request = UnityWebRequest.Get(YamlVersionURL);
			request.SetRequestHeader("Content-Type", "application/x-yaml");
			request.SetRequestHeader("User-Agent", "PLib AVC");
			request.timeout = JsonURLVersionChecker.REQUEST_TIMEOUT;
			var operation = request.SendWebRequest();
			operation.completed += (_) => OnRequestFinished(request, mod);
			return true;
		}

		/// <summary>
		/// When a web request completes, triggers the handler for the next updater.
		/// </summary>
		/// <param name="request">The YAML web request data.</param>
		/// <param name="mod">The mod that needs to be checked.</param>
		private void OnRequestFinished(UnityWebRequest request, Mod mod) {
			ModVersionCheckResults result = null;
			if (request.result == UnityWebRequest.Result.Success) {
				// Parse the text
				var modInfo = YamlIO.Parse<Mod.PackagedModInfo>(request.downloadHandler.
					text, default);
				string newVersion = modInfo?.version;
				if (modInfo != null && !string.IsNullOrEmpty(newVersion)) {
					string curVersion = PVersionCheck.GetCurrentVersion(mod);
#if DEBUG
					PUtil.LogDebug("Current version: {0} New YAML version: {1}".F(
						curVersion, newVersion));
#endif
					result = new ModVersionCheckResults(mod.staticID, newVersion !=
						curVersion, newVersion);
				}
			}
			request.Dispose();
			OnVersionCheckCompleted?.Invoke(result);
		}
	}
}
