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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Reflection;

namespace PeterHan.PLib.PatchManager {
	/// <summary>
	/// Represents a method that will be run by PLib at a specific time to reduce the number
	/// of patches required and allow conditional integration with other mods.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class PLibMethodAttribute : Attribute, IPLibAnnotation {
		/// <summary>
		/// Requires the specified assembly to be loaded for this method to run. If RequireType
		/// is null or empty, no particular types need to be defined in the assembly. The
		/// assembly name is required, but the version is optional (strong named assemblies
		/// can never load in ONI, since neither Unity nor Klei types are strong named...)
		/// </summary>
		public string RequireAssembly { get; set; }

		/// <summary>
		/// Requires the specified type full name (not assembly qualified name) to exist for
		/// this method to run. If RequireAssembly is null or empty, a type in any assembly
		/// will satisfy the requirement.
		/// </summary>
		public string RequireType { get; set; }

		/// <summary>
		/// When this method is run.
		/// </summary>
		public uint Runtime { get; }

		public PLibMethodAttribute(uint runtime) {
			Runtime = runtime;
		}

		/// <summary>
		/// Creates a new patch method instance.
		/// </summary>
		/// <param name="method">The method that was attributed.</param>
		/// <returns>An instance that can execute this patch.</returns>
		public IPatchMethodInstance CreateInstance(MethodInfo method) {
			return new PLibMethodInstance(this, method);
		}

		public override string ToString() {
			return "PLibMethod[RunAt={0}]".F(RunAt.ToString(Runtime));
		}
	}

	/// <summary>
	/// Refers to a single instance of the annotation, with its annotated method.
	/// </summary>
	internal sealed class PLibMethodInstance : IPatchMethodInstance {
		/// <summary>
		/// The attribute describing the method.
		/// </summary>
		public PLibMethodAttribute Descriptor { get; }

		/// <summary>
		/// The method to run.
		/// </summary>
		public MethodInfo Method { get; }

		public PLibMethodInstance(PLibMethodAttribute attribute, MethodInfo method) {
			Descriptor = attribute ?? throw new ArgumentNullException(nameof(attribute));
			Method = method ?? throw new ArgumentNullException(nameof(method));
		}

		/// <summary>
		/// Runs the method, passing the required parameters if any.
		/// </summary>
		/// <param name="instance">The Harmony instance to use if the method wants to
		/// perform a patch.</param>
		public void Run(Harmony instance) {
			if (PPatchManager.CheckConditions(Descriptor.RequireAssembly, Descriptor.
					RequireType, out Type requiredType)) {
				// Only runs once, no meaningful savings with a delegate
				var paramTypes = Method.GetParameterTypes();
				int len = paramTypes.Length;
				if (len <= 0)
					// No parameters, static method only
					Method.Invoke(null, null);
				else if (paramTypes[0] == typeof(Harmony)) {
					if (len == 1)
						// Harmony instance parameter
						Method.Invoke(null, new object[] { instance });
					else if (len == 2 && paramTypes[1] == typeof(Type))
						// Type parameter
						Method.Invoke(null, new object[] { instance, requiredType });
				} else
					PUtil.LogWarning("Invalid signature for PLibMethod - must have (), " +
						"(HarmonyInstance), or (HarmonyInstance, Type)");
			}
		}
	}
}
