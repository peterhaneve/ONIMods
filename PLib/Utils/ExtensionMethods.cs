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

using Harmony;
using System;
using System.Reflection;
using System.Text;
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
		/// Retrieves a component, but returns null if the GameObject is disposed.
		/// </summary>
		/// <typeparam name="T">The component type to retrieve.</typeparam>
		/// <param name="obj">The GameObject that hosts the component.</param>
		/// <returns>The requested component, or null if it does not exist</returns>
		public static T GetComponentSafe<T>(this GameObject obj) where T : Component {
#pragma warning disable IDE0031 // Use null propagation
			// == operator is overloaded on GameObject to be equal to null if destroyed
			return (obj == null) ? null : obj.GetComponent<T>();
#pragma warning restore IDE0031 // Use null propagation
		}

		/// <summary>
		/// Gets the assembly name of an assembly.
		/// </summary>
		/// <param name="assembly">The assembly to query.</param>
		/// <returns>The assembly name, or null if assembly is null.</returns>
		internal static string GetNameSafe(this Assembly assembly) {
			return assembly?.GetName()?.Name;
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
		/// Coerces a floating point number into the specified range.
		/// </summary>
		/// <param name="value">The original number.</param>
		/// <param name="min">The minimum value (inclusive).</param>
		/// <param name="max">The maximum value (inclusive).</param>
		/// <returns>The nearest value between minimum and maximum inclusive to value.</returns>
		public static double InRange(this double value, double min, double max) {
			double result = value;
			if (result < min) result = min;
			if (result > max) result = max;
			return result;
		}

		/// <summary>
		/// Coerces a floating point number into the specified range.
		/// </summary>
		/// <param name="value">The original number.</param>
		/// <param name="min">The minimum value (inclusive).</param>
		/// <param name="max">The maximum value (inclusive).</param>
		/// <returns>The nearest value between minimum and maximum inclusive to value.</returns>
		public static float InRange(this float value, float min, float max) {
			float result = value;
			if (result < min) result = min;
			if (result > max) result = max;
			return result;
		}

		/// <summary>
		/// Coerces an integer into the specified range.
		/// </summary>
		/// <param name="value">The original number.</param>
		/// <param name="min">The minimum value (inclusive).</param>
		/// <param name="max">The maximum value (inclusive).</param>
		/// <returns>The nearest value between minimum and maximum inclusive to value.</returns>
		public static int InRange(this int value, int min, int max) {
			int result = value;
			if (result < min) result = min;
			if (result > max) result = max;
			return result;
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
		/// Checks to see if a floating point value is NaN or infinite.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <returns>true if it is NaN, PositiveInfinity, or NegativeInfinity, or false otherwise.</returns>
		public static bool IsNaNOrInfinity(this double value) {
			return double.IsNaN(value) || double.IsInfinity(value);
		}

		/// <summary>
		/// Checks to see if a floating point value is NaN or infinite.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <returns>true if it is NaN, PositiveInfinity, or NegativeInfinity, or false otherwise.</returns>
		public static bool IsNaNOrInfinity(this float value) {
			return float.IsNaN(value) || float.IsInfinity(value);
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
		/// Creates a string joining the members of an enumerable.
		/// </summary>
		/// <param name="values">The values to join.</param>
		/// <param name="delimiter">The delimiter to use between values.</param>
		/// <returns>A string consisting of each value in order, with the delimiter in between.</returns>
		public static string Join(this System.Collections.IEnumerable values,
				string delimiter = ",") {
			var ret = new StringBuilder(128);
			bool first = true;
			// Append all, but skip comma if the first time
			foreach (var value in values) {
				if (!first)
					ret.Append(delimiter);
				ret.Append(value);
				first = false;
			}
			return ret.ToString();
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
		/// Patches a constructor manually.
		/// </summary>
		/// <param name="instance">The Harmony instance.</param>
		/// <param name="type">The class to modify.</param>
		/// <param name="arguments">The constructor's argument types.</param>
		/// <param name="prefix">The prefix to apply, or null if none.</param>
		/// <param name="postfix">The postfix to apply, or null if none.</param>
		public static void PatchConstructor(this HarmonyInstance instance, Type type,
				Type[] arguments, HarmonyMethod prefix = null, HarmonyMethod postfix = null) {
			if (type == null)
				throw new ArgumentNullException("type");
			// Fetch the constructor
			try {
				var cons = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public |
					BindingFlags.Static | BindingFlags.Instance, null, arguments, null);
				if (cons != null)
					instance.Patch(cons, prefix, postfix);
				else
					PUtil.LogWarning("Unable to find constructor on type {0}".F(type.
						FullName));
			} catch (ArgumentException e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Patches a method manually with a transpiler.
		/// </summary>
		/// <param name="instance">The Harmony instance.</param>
		/// <param name="type">The class to modify.</param>
		/// <param name="methodName">The method to patch.</param>
		/// <param name="transpiler">The transpiler to apply.</param>
		public static void PatchTranspile(this HarmonyInstance instance, Type type,
				string methodName, HarmonyMethod transpiler) {
			if (type == null)
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(methodName))
				throw new ArgumentNullException("method");
			// Fetch the method
			try {
				var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.
					Public | BindingFlags.Static | BindingFlags.Instance);
				if (method != null)
					instance.Patch(method, null, null, transpiler);
				else
					PUtil.LogWarning("Unable to find method {0} on type {1}".F(methodName,
						type.FullName));
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			} catch (FormatException e) {
				PUtil.LogWarning("Unable to transpile method {0}: {1}".F(methodName,
					e.Message));
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
		/// Sets a game object's parent.
		/// </summary>
		/// <param name="child">The game object to modify.</param>
		/// <param name="parent">The new parent object.</param>
		/// <returns>The game object, for call chaining.</returns>
		public static GameObject SetParent(this GameObject child, GameObject parent) {
			if (child == null)
				throw new ArgumentNullException("child");
#pragma warning disable IDE0031 // Use null propagation
			child.transform.SetParent((parent == null) ? null : parent.transform, false);
#pragma warning restore IDE0031 // Use null propagation
			return child;
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
