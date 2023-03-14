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

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Provides the user facing API to the PLib Registry.
	/// </summary>
	public static class PRegistry {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static IPLibRegistry Instance {
			get {
				lock (instanceLock) {
					if (instance == null)
						Init();
				}
				return instance;
			}
		}

		/// <summary>
		/// A pointer to the active PLib registry.
		/// </summary>
		private static IPLibRegistry instance = null;

		/// <summary>
		/// Ensures that PLib can only be initialized by one thread at a time.
		/// </summary>
		private static readonly object instanceLock = new object();

		/// <summary>
		/// Retrieves a value from the single-instance share.
		/// </summary>
		/// <typeparam name="T">The type of the desired data.</typeparam>
		/// <param name="key">The string key to retrieve. <i>Suggested key format: YourMod.
		/// Category.KeyName</i></param>
		/// <returns>The data associated with that key.</returns>
		public static T GetData<T>(string key) {
			T value = default;
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));
			var registry = Instance.ModData;
			if (registry != null && registry.TryGetValue(key, out object sval) && sval is
					T newVal)
				value = newVal;
			return value;
		}

		/// <summary>
		/// Initializes the patch bootstrapper, creating a PRegistry if not yet present.
		/// </summary>
		private static void Init() {
			const string INTENDED_NAME = "PRegistryComponent";
			var obj = Global.Instance?.gameObject;
			if (obj != null) {
				var plr = obj.GetComponent(INTENDED_NAME);
				if (plr == null) {
					var localReg = obj.AddComponent<PRegistryComponent>();
					// If PLib is ILMerged more than once, PRegistry gets added with a weird
					// type name including a GUID which does not match GetComponent.Name!
					string typeName = localReg.GetType().Name;
					if (typeName != INTENDED_NAME)
						LogPatchWarning(INTENDED_NAME + " has the type name " + typeName +
							"; this may be the result of ILMerging PLib more than once!");
#if DEBUG
					LogPatchDebug("Creating PLib Registry from " + System.Reflection.Assembly.
						GetExecutingAssembly()?.FullName ?? "?");
#endif
					// Patch in the bootstrap method
					localReg.ApplyBootstrapper();
					instance = localReg;
				} else {
					instance = new PRemoteRegistry(plr);
				}
			} else {
#if DEBUG
				LogPatchWarning("Attempted to initialize PLib Registry before Global created!");
#endif
				instance = null;
			}
			if (instance != null)
				new PLibCorePatches().Register(instance);
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
		/// Saves a value into the single-instance share.
		/// </summary>
		/// <param name="key">The string key to set. <i>Suggested key format: YourMod.
		/// Category.KeyName</i></param>
		/// <param name="value">The data to be associated with that key.</param>
		public static void PutData(string key, object value) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));
			var registry = Instance.ModData;
			if (registry != null) {
				if (registry.ContainsKey(key))
					registry[key] = value;
				else
					registry.Add(key, value);
			}
		}
	}
}
