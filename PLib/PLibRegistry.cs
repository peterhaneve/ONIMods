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
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// A custom component added to manage different PLib patch versions.
	/// </summary>
	sealed class PLibRegistry : MonoBehaviour {
		/// <summary>
		/// The instantiated instance of PLibRegistry, if it has been added as a component.
		/// </summary>
		private static PLibRegistry instance = null;

#pragma warning disable IDE0051 // Remove unused private members
		/// <summary>
		/// Finds the latest patch and applies only it.
		/// </summary>
		private static void ApplyLatest() {
			if (instance != null) {
				object latest = null;
				Version latestVer = null;
				try {
					foreach (var pair in instance.Patches) {
						var patch = pair.Value;
						var patchVer = new Version(pair.Key);
						if (latestVer == null || latestVer.CompareTo(patchVer) < 0) {
							// First element or newer version
							latest = patch;
							latestVer = patchVer;
						}
					}
				} catch (ArgumentOutOfRangeException e) {
					// .NET 3.5 please
					PLibUtil.LogException(e);
				} catch (FormatException e) {
					PLibUtil.LogException(e);
				} catch (OverflowException e) {
					PLibUtil.LogException(e);
				}
				if (latest != null)
					try {
						Traverse.Create(latest).CallMethod("Apply", instance.PLibInstance);
					} catch (ArgumentException e) {
						PLibUtil.LogException(e);
					}
			} else {
#if DEBUG
				LogPatchWarning("ApplyLatest invoked with no Instance!");
#endif
			}
		}
#pragma warning restore IDE0051 // Remove unused private members

		/// <summary>
		/// Initializes the patch bootstrapper, creating a PLibPatchRegistry if not yet
		/// present and offering our library as a candidate for shared patches.
		/// </summary>
		public static void Init() {
			var obj = Global.Instance.gameObject;
			if (obj != null) {
				// The hack is sick but we have few choices
				object reg = obj.GetComponent(typeof(PLibRegistry).Name);
				if (reg == null) {
					var plr = obj.AddComponent<PLibRegistry>();
#if DEBUG
					LogPatchDebug("Creating PLibRegistry from " + Assembly.
						GetExecutingAssembly().FullName);
#endif
					// Patch in the bootstrap method
					plr.ApplyBootstrapper();
					reg = plr;
				}
				// Use reflection to execute the actual AddPatch method
				try {
					Traverse.Create(reg).CallMethod("AddPatch", (object)new PLibPatches());
				} catch (ArgumentException e) {
					PLibUtil.LogException(e);
				}
			} else {
#if DEBUG
				LogPatchWarning("Attempted to Init before Global created!");
#endif
			}
		}

		/// <summary>
		/// Logs a debug message while patching in PLib patches.
		/// </summary>
		/// <param name="message">The debug message.</param>
		public static void LogPatchDebug(string message) {
			Debug.LogFormat("[PLibPatches] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered while patching in PLib patches.
		/// </summary>
		/// <param name="message">The warning message.</param>
		public static void LogPatchWarning(string message) {
			Debug.LogWarningFormat("[PLibPatches] {0}", message);
		}

		/// <summary>
		/// Stores shared mod data which needs single instance existence. Available to all
		/// PLib consumers through PLib API.
		/// </summary>
		private readonly IDictionary<string, object> modData;

		/// <summary>
		/// The candidate patches with file version.
		/// </summary>
		public IDictionary<string, object> Patches { get; }

		/// <summary>
		/// The Harmony instance used by PLib patching.
		/// </summary>
		public HarmonyInstance PLibInstance { get; }

		public PLibRegistry() {
			if (instance == null)
				instance = this;
			else {
#if DEBUG
				LogPatchWarning("Multiple PLibRegistry created!");
#endif
			}
			modData = new Dictionary<string, object>(64);
			Patches = new Dictionary<string, object>(32);
			PLibInstance = HarmonyInstance.Create("PeterHan.PLib");
		}

		/// <summary>
		/// Adds a candidate patch.
		/// </summary>
		/// <param name="patch">The patch from PLib to add.</param>
		public void AddPatch(object patch) {
			if (patch == null)
				throw new ArgumentNullException("patch");
			string ver = Traverse.Create(patch).GetProperty<string>("MyVersion");
			if (ver == null) {
#if DEBUG
				LogPatchWarning("Invalid patch provided to AddPatch!");
#endif
			} else if (!Patches.ContainsKey(ver)) {
				LogPatchDebug("Candidate version {0} from {1}".F(ver, patch.GetType().
					Assembly.GetName()?.Name));
				Patches.Add(ver, patch);
			}
		}

		/// <summary>
		/// Applies a bootstrapper patch which will patch in the appropriate version of
		/// PLibPatches when Global.Awake() completes.
		/// </summary>
		private void ApplyBootstrapper() {
			try {
				// Gets called in Global.Awake() after mods load
				PLibInstance.Patch(typeof(Global), "RestoreLegacyMetricsSetting", null,
					new HarmonyMethod(typeof(PLibRegistry).GetMethod("ApplyLatest",
					BindingFlags.NonPublic | BindingFlags.Static)));
			} catch (AmbiguousMatchException e) {
				PLibUtil.LogException(e);
			} catch (ArgumentException e) {
				PLibUtil.LogException(e);
			}
		}

		/// <summary>
		/// Retrieves a value from the single-instance share.
		/// </summary>
		/// <param name="key">The string key to retrieve.</param>
		/// <returns>The data associated with that key.</returns>
		public object GetData(string key) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			modData.TryGetValue(key, out object ret);
			return ret;
		}

		/// <summary>
		/// Saves a value into the single-instance share.
		/// </summary>
		/// <param name="key">The string key to set.</param>
		/// <param name="value">The data to be associated with that key.</param>
		public void PutData(string key, object value) {
			if (modData.ContainsKey(key))
				modData[key] = value;
			else
				modData.Add(key, value);
		}

		public override string ToString() {
			return Patches.ToString();
		}
	}
}
