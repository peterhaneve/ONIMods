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
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Extension methods to make life easier!
	/// </summary>
	public static class ExtensionMethods {
		/// <summary>
		/// Shorthand for string.Format() which can be invoked directly on the message.
		/// </summary>
		/// <param name="message">The format template message.</param>
		/// <param name="args">The substitutions to be included.</param>
		/// <returns>The formatted string.</returns>
		public static string F(this string message, params object[] args) {
			return string.Format(message, args);
		}

		/// <summary>
		/// Uses Traverse to call a private method on an object.
		/// </summary>
		/// <param name="root">The object on which to call the method.</param>
		/// <param name="name">The method name to call.</param>
		/// <param name="args">The arguments to supply to the method.</param>
		public static void CallMethod(this Traverse root, string name, params object[] args) {
			root.Method(name, args).GetValue(args);
		}

		/// <summary>
		/// Uses Traverse to call a private method on an object.
		/// </summary>
		/// <param name="root">The object on which to call the method.</param>
		/// <param name="name">The method name to call.</param>
		/// <param name="args">The arguments to supply to the method.</param>
		/// <returns>The method's return value.</returns>
		public static T CallMethod<T>(this Traverse root, string name, params object[] args) {
			return root.Method(name, args).GetValue<T>(args);
		}

		/// <summary>
		/// Uses Traverse to retrieve a private field of an object.
		/// </summary>
		/// <param name="root">The object on which to get the field.</param>
		/// <param name="name">The field name to access.</param>
		/// <returns>The value of the field.</returns>
		public static T GetField<T>(this Traverse root, string name) {
			return root.Field(name).GetValue<T>();
		}

		/// <summary>
		/// Uses Traverse to retrieve a private property of an object.
		/// </summary>
		/// <param name="root">The object on which to get the property.</param>
		/// <param name="name">The property name to access.</param>
		/// <returns>The value of the property.</returns>
		public static T GetProperty<T>(this Traverse root, string name) {
			return root.Property(name).GetValue<T>();
		}

		/// <summary>
		/// Gets the file version of the specified assembly.
		/// </summary>
		/// <param name="assembly">The assembly to query</param>
		/// <returns>The AssemblyFileVersion of that assembly, or null if it could not be determined.</returns>
		public static string GetFileVersion(this Assembly assembly) {
			// Mod version
			var fileVersions = assembly.GetCustomAttributes(typeof(
				AssemblyFileVersionAttribute), true);
			string modVersion = null;
			if (fileVersions != null && fileVersions.Length > 0) {
				// Retrieves the "File Version" attribute
				var assemblyFileVersion = (AssemblyFileVersionAttribute)fileVersions[0];
				if (assemblyFileVersion != null)
					modVersion = assemblyFileVersion.Version;
			}
			return modVersion;
		}

		/// <summary>
		/// Checks to see if an object is falling.
		/// </summary>
		/// <param name="obj">The object to check.</param>
		/// <returns>true if it is falling, or false otherwise.</returns>
		public static bool IsFalling(this GameObject obj) {
			return obj.GetSMI<FallMonitor.Instance>()?.IsFalling() ?? false;
		}

		/// <summary>
		/// Checks to see if a building is usable.
		/// </summary>
		/// <param name="building">The building component to check.</param>
		/// <returns>true if it is usable (enabled, not broken, not overheated), or false otherwise.</returns>
		public static bool IsUsable(this GameObject building) {
			return building.GetComponent<Operational>()?.IsFunctional ?? false;
		}

		/// <summary>
		/// Patches a method manually.
		/// </summary>
		/// <param name="instance">The Harmony instance.</param>
		/// <param name="type">The class to modify.</param>
		/// <param name="methodName">The method to patch.</param>
		/// <param name="prefix">The prefix to apply, or null if none.</param>
		/// <param name="postfix">The postfix to apply, or null if none.</param>
		public static void Patch(this HarmonyInstance instance, Type type, string methodName,
				HarmonyMethod prefix = null, HarmonyMethod postfix = null) {
			if (type == null)
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(methodName))
				throw new ArgumentNullException("method");
			// Fetch the method
			try {
				var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.
					Public | BindingFlags.Static | BindingFlags.Instance);
				if (method != null)
					instance.Patch(method, prefix, postfix);
				else
					PUtil.LogWarning("Unable to find method {0} on type {1}".F(methodName,
						type.FullName));
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Uses Traverse to set a private field of an object.
		/// </summary>
		/// <param name="root">The object on which to set the field.</param>
		/// <param name="name">The field name to edit.</param>
		/// <param name="value">The new value to assign to the field.</param>
		public static void SetField(this Traverse root, string name, object value) {
			root.Field(name).SetValue(value);
		}

		/// <summary>
		/// Uses Traverse to set a private property of an object.
		/// </summary>
		/// <param name="root">The object on which to set the property.</param>
		/// <param name="name">The property name to edit.</param>
		/// <param name="value">The new value to assign to the property.</param>
		public static void SetProperty(this Traverse root, string name, object value) {
			root.Property(name).SetValue(value);
		}
	}
}
