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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// A custom component added to manage different PLib patch versions.
	/// </summary>
	internal sealed class PRegistry : MonoBehaviour, IDictionary<string, object> {
		#region Shared Keys

		/// <summary>
		/// Key used for the PLib action internal ID.
		/// </summary>
		public const string KEY_ACTION_ID = "PLib.Action.ID";

		/// <summary>
		/// Used to synchronize access to PLib action data.
		/// </summary>
		public const string KEY_ACTION_LOCK = "PLib.Action.Lock";

		/// <summary>
		/// Stores the functions to be invoked when a PLib action is fired.
		/// </summary>
		public const string KEY_ACTION_TABLE = "PLib.Action.Table";

		/// <summary>
		/// Used to synchronize access to PLib.Lighting data.
		/// </summary>
		public const string KEY_LIGHTING_LOCK = "PLib.Lighting.Lock";

		/// <summary>
		/// Stores the lighting types currently registered.
		/// </summary>
		public const string KEY_LIGHTING_TABLE = "PLib.Lighting.Table";

		/// <summary>
		/// Used to synchronize access to PLib options data.
		/// </summary>
		public const string KEY_OPTIONS_LOCK = "PLib.Options.Lock";

		/// <summary>
		/// Stores the mod options currently registered.
		/// </summary>
		public const string KEY_OPTIONS_TABLE = "PLib.Options.Table";

		/// <summary>
		/// Used to synchronize access to post load handlers.
		/// </summary>
		public const string KEY_POSTLOAD_LOCK = "PLib.PostLoad.Lock";

		/// <summary>
		/// Stores the post load handlers currently registered.
		/// </summary>
		public const string KEY_POSTLOAD_TABLE = "PLib.PostLoad.Table";

		/// <summary>
		/// Used to denote the latest version of PLib installed across any mod, which is the
		/// version that is being used for any shared item forwarding.
		/// </summary>
		public const string KEY_VERSION = "PLib.Version";

		#endregion

		/// <summary>
		/// The Harmony instance name used when patching via PLib.
		/// </summary>
		internal const string PLIB_HARMONY = "PeterHan.PLib";

		/// <summary>
		/// The instantiated instance of PLibRegistry, if it has been added as a component.
		/// </summary>
		private static PRegistry instance = null;

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
					PUtil.LogException(e);
				} catch (FormatException e) {
					PUtil.LogException(e);
				} catch (OverflowException e) {
					PUtil.LogException(e);
				}
				if (latest != null) {
					// Store the winning version
					PSharedData.PutData(KEY_VERSION, latestVer.ToString());
					try {
						Traverse.Create(latest).CallMethod("Apply", instance.PLibInstance);
					} catch (ArgumentException e) {
						PUtil.LogException(e);
					}
				}
				// Reduce memory usage by cleaning up the patch list
				instance.Patches.Clear();
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
				object reg = obj.GetComponent(typeof(PRegistry).Name);
				if (reg == null) {
					var plr = obj.AddComponent<PRegistry>();
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
					PUtil.LogException(e);
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
		internal static void LogPatchDebug(string message) {
			Debug.LogFormat("[PLibPatches] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered while patching in PLib patches.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogPatchWarning(string message) {
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

		public PRegistry() {
			if (instance == null)
				instance = this;
			else {
#if DEBUG
				LogPatchWarning("Multiple PLibRegistry created!");
#endif
			}
			modData = new Dictionary<string, object>(64);
			Patches = new Dictionary<string, object>(32);
			PLibInstance = HarmonyInstance.Create(PLIB_HARMONY);

			// Action 0 is reserved
			modData.Add(KEY_ACTION_ID, 1);
			modData.Add(KEY_ACTION_LOCK, new object());
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
					new HarmonyMethod(typeof(PRegistry).GetMethod("ApplyLatest",
					BindingFlags.NonPublic | BindingFlags.Static)));
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			} catch (ArgumentException e) {
				PUtil.LogException(e);
			} catch (TypeLoadException e) {
				PUtil.LogException(e);
			}
		}

		public override string ToString() {
			return Patches.ToString();
		}

		#region IDictionary

		public int Count => modData.Count;

		public ICollection<string> Keys => modData.Keys;

		public ICollection<object> Values => modData.Values;

		/// <summary>
		/// Mod data is mutable.
		/// </summary>
		public bool IsReadOnly => false;

		public object this[string key] {
			get => modData[key];
			set => modData[key] = value;
		}

		public void Add(string key, object value) {
			modData.Add(key, value);
		}

		public void Add(KeyValuePair<string, object> item) {
			modData.Add(item);
		}

		public void Clear() {
			modData.Clear();
		}

		public bool Contains(KeyValuePair<string, object> item) {
			return modData.Contains(item);
		}

		public bool ContainsKey(string key) {
			return modData.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) {
			modData.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
			return modData.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return modData.GetEnumerator();
		}

		public bool Remove(KeyValuePair<string, object> item) {
			return modData.Remove(item);
		}

		public bool Remove(string key) {
			return modData.Remove(key);
		}

		public bool TryGetValue(string key, out object value) {
			return modData.TryGetValue(key, out value);
		}

		#endregion
	}
}
