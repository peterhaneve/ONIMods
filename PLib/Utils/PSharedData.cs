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

using System;
using System.Collections.Generic;

namespace PeterHan.PLib {
	/// <summary>
	/// Used to handle data sharing between all PLib mod dependents. This data is shared
	/// across assemblies, so caution is advised when using custom types.
	/// </summary>
	public static class PSharedData {
		/// <summary>
		/// The PLibRegistry cached object. Set to non-null when it is successfully resolved.
		/// </summary>
		private static IDictionary<string, object> registry = null;

		/// <summary>
		/// Initializes the registry from the Global state if necessary.
		/// </summary>
		private static void InitRegistry() {
			if (registry == null) {
				var obj = Global.Instance.gameObject;
				object pr;
				if (obj != null && (pr = obj.GetComponent(typeof(PRegistry).Name)) != null)
					registry = pr as IDictionary<string, object>;
			}
		}

		/// <summary>
		/// Retrieves a locking object from the shared data, creating it if necessary.
		/// </summary>
		/// <param name="key">The locking object to retrieve.</param>
		/// <returns>A lock object to synchronize data accesses.</returns>
		internal static object GetLock(string key) {
			object locker;
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			InitRegistry();
			if (registry != null) {
				// Attempt to read and relock
				registry.TryGetValue(key, out locker);
				if (locker == null)
					registry.Add(key, locker = new object());
			} else
				// Avoid crashing by locking null
				locker = new object();
			return locker;
		}

		/// <summary>
		/// Retrieves a value from the single-instance share.
		/// </summary>
		/// <typeparam name="T">The type of the desired data.</typeparam>
		/// <param name="key">The string key to retrieve. <i>Suggested key format: YourMod.
		/// Category.KeyName</i></param>
		/// <returns>The data associated with that key.</returns>
		public static T GetData<T>(string key) {
			T value = default;
			object sval = null;
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			InitRegistry();
			registry?.TryGetValue(key, out sval);
			if (sval is T)
				value = (T)sval;
			return value;
		}

		/// <summary>
		/// Saves a value into the single-instance share.
		/// </summary>
		/// <param name="key">The string key to set. <i>Suggested key format: YourMod.
		/// Category.KeyName</i></param>
		/// <param name="value">The data to be associated with that key.</param>
		public static void PutData(string key, object value) {
			InitRegistry();
			if (registry != null) {
				if (registry.ContainsKey(key))
					registry[key] = value;
				else
					registry.Add(key, value);
			}
		}
	}
}
