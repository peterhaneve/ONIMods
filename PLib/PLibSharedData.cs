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

namespace PeterHan.PLib {
	/// <summary>
	/// Used to handle data sharing between all PLib mod dependents. This data is shared
	/// across assemblies, so caution is advised when using custom types.
	/// 
	/// Access to this data may be slow, so avoid more checks than necessary.
	/// </summary>
	public static class PLibSharedData {
		/// <summary>
		/// The PLibRegistry cached object. Set to non-null when it is successfully resolved.
		/// Cannot be of type PLibRegistry because it might be from another assembly.
		/// </summary>
		private static Traverse registry = null;

		/// <summary>
		/// Initializes the registry from the Global state if necessary.
		/// </summary>
		private static void InitRegistry() {
			if (registry == null) {
				var obj = Global.Instance.gameObject;
				object pr;
				if (obj != null && (pr = obj.GetComponent(typeof(PLibRegistry).Name)) != null)
					// Will only use reflection so 
					registry = Traverse.Create(pr);
			}
		}

		/// <summary>
		/// Retrieves a value from the single-instance share.
		/// </summary>
		/// <typeparam name="T">The type of the desired data.</typeparam>
		/// <param name="key">The string key to retrieve.</param>
		/// <returns>The data associated with that key.</returns>
		public static T GetData<T>(string key) where T : class {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			InitRegistry();
			object ret = default(T);
			// Generics and Harmony do not work well so pass as System.Object and convert here
			if (registry != null)
				ret = registry.CallMethod<object>("GetData", key);
			return ret as T;
		}

		/// <summary>
		/// Saves a value into the single-instance share.
		/// </summary>
		/// <param name="key">The string key to set.</param>
		/// <param name="value">The data to be associated with that key.</param>
		public static void PutData(string key, object value) {
			InitRegistry();
			if (registry != null)
				registry.CallMethod("PutData", key, value);
		}
	}
}
