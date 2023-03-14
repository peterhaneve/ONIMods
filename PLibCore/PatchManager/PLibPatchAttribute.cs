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
	/// Represents a method that will be patched by PLib at a specific time to allow
	/// conditional integration with other mods.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class PLibPatchAttribute : Attribute, IPLibAnnotation {
		/// <summary>
		/// The required argument types. If null, any matching method name is patched, or an
		/// exception thrown if more than one matches.
		/// </summary>
		public Type[] ArgumentTypes { get; set; }

		/// <summary>
		/// If this flag is set, the patch will emit only at DEBUG level if the target method
		/// is not found or matches ambiguously.
		/// </summary>
		public bool IgnoreOnFail { get; set; }

		/// <summary>
		/// The name of the method to patch.
		/// </summary>
		public string MethodName { get; }

		/// <summary>
		/// The type of patch to apply through Harmony.
		/// </summary>
		public HarmonyPatchType PatchType { get; set; }

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
		
		/// <summary>
		/// The type to patch. If null, the patcher will try to use the required type from the
		/// RequireType parameter.
		/// </summary>
		public Type TargetType { get; }

		/// <summary>
		/// Patches a concrete type and method.
		/// 
		/// Passing null as the method name will attempt to patch a constructor. Only one
		/// declared constructor may be present, or the call will fail at patch time.
		/// </summary>
		/// <param name="runtime">When to apply the patch.</param>
		/// <param name="target">The type to patch.</param>
		/// <param name="method">The method name to patch.</param>
		public PLibPatchAttribute(uint runtime, Type target, string method) {
			ArgumentTypes = null;
			IgnoreOnFail = false;
			MethodName = method;
			PatchType = HarmonyPatchType.All;
			Runtime = runtime;
			TargetType = target ?? throw new ArgumentNullException(nameof(target));
		}

		/// <summary>
		/// Patches a concrete type and overloaded method.
		/// 
		/// Passing null as the method name will attempt to patch a constructor.
		/// </summary>
		/// <param name="runtime">When to apply the patch.</param>
		/// <param name="target">The type to patch.</param>
		/// <param name="method">The method name to patch.</param>
		/// <param name="argTypes">The types of the overload to patch.</param>
		public PLibPatchAttribute(uint runtime, Type target, string method,
				params Type[] argTypes) {
			ArgumentTypes = argTypes;
			IgnoreOnFail = false;
			MethodName = method;
			PatchType = HarmonyPatchType.All;
			Runtime = runtime;
			TargetType = target ?? throw new ArgumentNullException(nameof(target));
		}

		/// <summary>
		/// Patches a method only if a specified type is available. Use optional parameters to
		/// specify the type to patch using RequireType / RequireAssembly.
		/// 
		/// Passing null as the method name will attempt to patch a constructor. Only one
		/// declared constructor may be present, or the call will fail at patch time.
		/// </summary>
		/// <param name="runtime">When to apply the patch.</param>
		/// <param name="method">The method name to patch.</param>
		public PLibPatchAttribute(uint runtime, string method) {
			ArgumentTypes = null;
			IgnoreOnFail = false;
			MethodName = method;
			PatchType = HarmonyPatchType.All;
			Runtime = runtime;
			TargetType = null;
		}

		/// <summary>
		/// Patches an overloaded method only if a specified type is available. Use optional
		/// parameters to specify the type to patch using RequireType / RequireAssembly.
		/// 
		/// Passing null as the method name will attempt to patch a constructor.
		/// </summary>
		/// <param name="runtime">When to apply the patch.</param>
		/// <param name="method">The method name to patch.</param>
		/// <param name="argTypes">The types of the overload to patch.</param>
		public PLibPatchAttribute(uint runtime, string method, params Type[] argTypes) {
			ArgumentTypes = argTypes;
			IgnoreOnFail = false;
			MethodName = method;
			PatchType = HarmonyPatchType.All;
			Runtime = runtime;
			TargetType = null;
		}

		/// <summary>
		/// Creates a new patch method instance.
		/// </summary>
		/// <param name="method">The method that was attributed.</param>
		/// <returns>An instance that can execute this patch.</returns>
		public IPatchMethodInstance CreateInstance(MethodInfo method) {
			return new PLibPatchInstance(this, method);
		}

		public override string ToString() {
			return "PLibPatch[RunAt={0},PatchType={1},MethodName={2}]".F(RunAt.ToString(
				Runtime), PatchType, MethodName);
		}
	}

	/// <summary>
	/// Refers to a single instance of the annotation, with its annotated method.
	/// </summary>
	internal sealed class PLibPatchInstance : IPatchMethodInstance {
		/// <summary>
		/// The attribute describing the method.
		/// </summary>
		public PLibPatchAttribute Descriptor { get; }

		/// <summary>
		/// The method to run.
		/// </summary>
		public MethodInfo Method { get; }

		public PLibPatchInstance(PLibPatchAttribute attribute, MethodInfo method) {
			Descriptor = attribute ?? throw new ArgumentNullException(nameof(attribute));
			Method = method ?? throw new ArgumentNullException(nameof(method));
		}

		/// <summary>
		/// Calculates the patch type to perform.
		/// </summary>
		/// <returns>The type of Harmony patch to use for this method.</returns>
		private HarmonyPatchType GetPatchType() {
			var patchType = Descriptor.PatchType;
			if (patchType == HarmonyPatchType.All) {
				// Auto-determine the patch type based on name, if possible
				string patchName = Method.Name;
				foreach (var value in Enum.GetValues(typeof(HarmonyPatchType)))
					if (value is HarmonyPatchType eval && eval != patchType && patchName.
							EndsWith(eval.ToString(), StringComparison.Ordinal)) {
						patchType = eval;
						break;
					}
			}
			return patchType;
		}

		/// <summary>
		/// Gets the specified instance constructor.
		/// </summary>
		/// <param name="targetType">The type to be constructed.</param>
		/// <returns>The target constructor.</returns>
		/// <exception cref="AmbiguousMatchException">If no parameter types were specified,
		/// and multiple declared constructors exist.</exception>
		private MethodBase GetTargetConstructor(Type targetType, Type[] argumentTypes) {
			MethodBase constructor;
			if (argumentTypes == null) {
				var cons = targetType.GetConstructors(PPatchManager.FLAGS | BindingFlags.
					Instance);
				if (cons == null || cons.Length != 1)
					throw new InvalidOperationException("No constructor for {0} found".F(
						targetType.FullName));
				constructor = cons[0];
			} else
				constructor = targetType.GetConstructor(PPatchManager.FLAGS | BindingFlags.
					Instance, null, argumentTypes, null);
			return constructor;
		}

		/// <summary>
		/// Calculates the target method to patch.
		/// </summary>
		/// <param name="requiredType">The type to use if no type was specified.</param>
		/// <returns>The method to patch.</returns>
		/// <exception cref="AmbiguousMatchException">If no parameter types were specified,
		/// and multiple options match the method name.</exception>
		/// <exception cref="InvalidOperationException">If the target method was not found.</exception>
		private MethodBase GetTargetMethod(Type requiredType) {
			var targetType = Descriptor.TargetType;
			var argumentTypes = Descriptor.ArgumentTypes;
			string name = Descriptor.MethodName;
			MethodBase method;
			if (targetType == null)
				targetType = requiredType;
			// Only allow non-inherited members, patching inherited members gets spooky on
			// Mac OS and Linux
			if (targetType == null)
				throw new InvalidOperationException("No type specified to patch");
			if (string.IsNullOrEmpty(name) || name == ".ctor")
				// Constructor
				method = GetTargetConstructor(targetType, argumentTypes);
			else
				// Method
				method = (argumentTypes == null) ? targetType.GetMethod(name, PPatchManager.
					FLAGS_EITHER) : targetType.GetMethod(name, PPatchManager.FLAGS_EITHER,
					null, argumentTypes, null);
			if (method == null)
				throw new InvalidOperationException("Method {0}.{1} not found".F(targetType.
					FullName, name));
			return method;
		}

		/// <summary>
		/// Logs a message at debug level if Ignore On Patch Fail is enabled.
		/// </summary>
		/// <param name="e">The exception thrown during patching.</param>
		/// <returns>true to suppress the exception, or false to rethrow it.</returns>
		private bool LogIgnoreOnFail(Exception e) {
			bool ignore = Descriptor.IgnoreOnFail;
			if (ignore)
				PUtil.LogDebug("Patch for {0} not applied: {1}".F(Descriptor.
					MethodName, e.Message));
			return ignore;
		}

		/// <summary>
		/// Applies the patch.
		/// </summary>
		/// <param name="instance">The Harmony instance to use.</param>
		/// <exception cref="InvalidOperationException">If the </exception>
		/// <exception cref="AmbiguousMatchException">If no parameter types were specified,
		/// and multiple options match the method name.</exception>
		public void Run(Harmony instance) {
			if (PPatchManager.CheckConditions(Descriptor.RequireAssembly, Descriptor.
					RequireType, out Type requiredType)) {
				var dest = new HarmonyMethod(Method);
				if (instance == null)
					throw new ArgumentNullException(nameof(instance));
				try {
					var method = GetTargetMethod(requiredType);
					switch (GetPatchType()) {
					case HarmonyPatchType.Postfix:
						instance.Patch(method, postfix: dest);
						break;
					case HarmonyPatchType.Prefix:
						instance.Patch(method, prefix: dest);
						break;
					case HarmonyPatchType.Transpiler:
						instance.Patch(method, transpiler: dest);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(HarmonyPatchType));
					}
				} catch (AmbiguousMatchException e) {
					// Multi catch or filtering is not available in this version of C#
					if (!LogIgnoreOnFail(e))
						throw;
				} catch (InvalidOperationException e) {
					if (!LogIgnoreOnFail(e))
						throw;
				}
			}
		}
	}
}
