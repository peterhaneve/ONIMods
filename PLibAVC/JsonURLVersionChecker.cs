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

using KMod;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Checks the mod version using a URL to a JSON file. The file at this URL must resolve
	/// to a JSON file which can deserialize to the JsonURLVersionChecker.ModVersions class.
	/// </summary>
	public sealed class JsonURLVersionChecker : IModVersionChecker {
		/// <summary>
		/// The timeout in seconds for the web request before declaring the check as failed.
		/// </summary>
		public const int REQUEST_TIMEOUT = 8;

		/// <summary>
		/// The URL to query for checking the mod version.
		/// </summary>
		public string JsonVersionURL { get; }

		public event PVersionCheck.OnVersionCheckComplete OnVersionCheckCompleted;

		public JsonURLVersionChecker(string url) {
			if (string.IsNullOrEmpty(url))
				throw new ArgumentNullException(nameof(url));
			JsonVersionURL = url;
		}

		public bool CheckVersion(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			var request = UnityWebRequest.Get(JsonVersionURL);
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("User-Agent", "PLib AVC");
			request.timeout = REQUEST_TIMEOUT;
			var operation = request.SendWebRequest();
			operation.completed += (_) => OnRequestFinished(request, mod);
			return true;
		}

		/// <summary>
		/// When a web request completes, triggers the handler for the next updater.
		/// </summary>
		/// <param name="request">The JSON web request data.</param>
		/// <param name="mod">The mod that needs to be checked.</param>
		private void OnRequestFinished(UnityWebRequest request, Mod mod) {
			ModVersionCheckResults result = null;
			if (request.result == UnityWebRequest.Result.Success) {
				// Parse the text
				ModVersions versions;
				using (var reader = new StreamReader(new MemoryStream(request.
						downloadHandler.data))) {
					versions = new JsonSerializer() {
						MaxDepth = 4, DateTimeZoneHandling = DateTimeZoneHandling.Utc,
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}.Deserialize<ModVersions>(new JsonTextReader(reader));
				}
				if (versions != null)
					result = ParseModVersion(mod, versions);
			}
			request.Dispose();
			OnVersionCheckCompleted?.Invoke(result);
		}

		/// <summary>
		/// Parses the JSON file and looks up the version for the specified mod.
		/// </summary>
		/// <param name="mod">The mod's static ID.</param>
		/// <param name="versions">The data from the web JSON file.</param>
		/// <returns>The results of the update, or null if the mod could not be found in the
		/// JSON.</returns>
		private ModVersionCheckResults ParseModVersion(Mod mod, ModVersions versions) {
			ModVersionCheckResults result = null;
			string id = mod.staticID;
			if (versions.mods != null)
				foreach (var modVersion in versions.mods)
					if (modVersion != null && modVersion.staticID == id) {
						string newVersion = modVersion.version?.Trim();
						if (string.IsNullOrEmpty(newVersion))
							result = new ModVersionCheckResults(id, true);
						else
							result = new ModVersionCheckResults(id, newVersion !=
								PVersionCheck.GetCurrentVersion(mod), newVersion);
						break;
					}
			return result;
		}

		/// <summary>
		/// The serialization type for JSONURLVersionChecker. Allows multiple mods to query
		/// the same URL.
		/// </summary>
		[JsonObject(MemberSerialization.OptIn)]
		public sealed class ModVersions {
			[JsonProperty]
			public List<ModVersion> mods;

			public ModVersions() {
				mods = new List<ModVersion>(16);
			}
		}

		/// <summary>
		/// Represents the current version of each mod.
		/// </summary>
		public sealed class ModVersion {
			/// <summary>
			/// The mod's static ID, as reported by its mod.yaml. If a mod does not specify its
			/// static ID, it gets the default ID mod.label.id + "_" + mod.label.
			/// distribution_platform.
			/// </summary>
			public string staticID { get; set; }

			/// <summary>
			/// The mod's current version.
			/// </summary>
			public string version { get; set; }

			public override string ToString() {
				return "{0}: version={1}".F(staticID, version);
			}
		}
	}
}
