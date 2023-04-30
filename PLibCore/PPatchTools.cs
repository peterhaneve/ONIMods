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
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Contains tools to aid with patching.
	/// </summary>
	public static class PPatchTools {
		/// <summary>
		/// The base binding flags for all reflection methods.
		/// </summary>
		public const BindingFlags BASE_FLAGS = BindingFlags.Public | BindingFlags.NonPublic;

		/// <summary>
		/// Passed to GetMethodSafe to match any method arguments.
		/// </summary>
		public static Type[] AnyArguments {
			get {
				return new Type[] { null };
			}
		}

		/// <summary>
		/// A placeholder flag to ReplaceMethodCallSafe to remove the method call.
		/// </summary>
		public static readonly MethodInfo RemoveCall = typeof(PPatchTools).GetMethodSafe(
			nameof(RemoveMethodCallPrivate), true);

		/// <summary>
		/// Creates a delegate for a private instance method. This delegate is over ten times
		/// faster than reflection, so useful if called frequently on the same object.
		/// </summary>
		/// <typeparam name="T">A delegate type which matches the method signature.</typeparam>
		/// <param name="type">The declaring type of the target method.</param>
		/// <param name="method">The target method name.</param>
		/// <param name="caller">The object on which to call the method.</param>
		/// <param name="argumentTypes">The types of the target method arguments, or PPatchTools.
		/// AnyArguments (not recommended, type safety is good) to match any method with
		/// that name.</param>
		/// <returns>A delegate which calls this method, or null if the method could not be
		/// found or did not match the types.</returns>
		public static T CreateDelegate<T>(this Type type, string method, object caller,
				params Type[] argumentTypes) where T : Delegate {
			var del = default(T);
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(method))
				throw new ArgumentNullException(nameof(method));
			var reflectMethod = GetMethodSafe(type, method, false, argumentTypes);
			// MethodInfo.CreateDelegate is @since .NET 5.0 :(
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(T), caller, reflectMethod, false) as T;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private instance method. This delegate is over ten times
		/// faster than reflection, so useful if called frequently on the same object.
		/// </summary>
		/// <typeparam name="T">A delegate type which matches the method signature.</typeparam>
		/// <param name="type">The declaring type of the target method.</param>
		/// <param name="method">The target method.</param>
		/// <param name="caller">The object on which to call the method.</param>
		/// <returns>A delegate which calls this method, or null if the method was null or did
		/// not match the delegate type.</returns>
		public static T CreateDelegate<T>(this MethodInfo method, object caller)
				where T : Delegate {
			var del = default(T);
			if (method != null)
				del = Delegate.CreateDelegate(typeof(T), caller, method, false) as T;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private instance property getter. This delegate is over
		/// ten times faster than reflection, so useful if called frequently on the same object.
		/// 
		/// This method does not work on indexed properties.
		/// </summary>
		/// <typeparam name="T">The property's type.</typeparam>
		/// <param name="type">The declaring type of the target property.</param>
		/// <param name="property">The target property name.</param>
		/// <param name="caller">The object on which to call the property getter.</param>
		/// <returns>A delegate which calls this property's getter, or null if the property
		/// could not be found or did not match the type.</returns>
		public static Func<T> CreateGetDelegate<T>(this Type type, string property,
				object caller) {
			Func<T> del = null;
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(property))
				throw new ArgumentNullException(nameof(property));
			var reflectMethod = GetPropertySafe<T>(type, property, false)?.GetGetMethod(true);
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(Func<T>), caller, reflectMethod, false)
					as Func<T>;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private instance property getter. This delegate is over
		/// ten times faster than reflection, so useful if called frequently on the same object.
		/// 
		/// This method does not work on indexed properties.
		/// </summary>
		/// <typeparam name="T">The property's type.</typeparam>
		/// <param name="property">The target property.</param>
		/// <param name="caller">The object on which to call the property getter.</param>
		/// <returns>A delegate which calls this property's getter, or null if the property
		/// was null or did not match the type.</returns>
		public static Func<T> CreateGetDelegate<T>(this PropertyInfo property, object caller) {
			Func<T> del = null;
			var reflectMethod = property?.GetGetMethod(true);
			if (reflectMethod != null && typeof(T).IsAssignableFrom(property.PropertyType))
				del = Delegate.CreateDelegate(typeof(Func<T>), caller, reflectMethod, false)
					as Func<T>;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private instance property setter. This delegate is over
		/// ten times faster than reflection, so useful if called frequently on the same object.
		/// 
		/// This method does not work on indexed properties.
		/// </summary>
		/// <typeparam name="T">The property's type.</typeparam>
		/// <param name="type">The declaring type of the target property.</param>
		/// <param name="property">The target property name.</param>
		/// <param name="caller">The object on which to call the property setter.</param>
		/// <returns>A delegate which calls this property's setter, or null if the property
		/// could not be found or did not match the type.</returns>
		public static Action<T> CreateSetDelegate<T>(this Type type, string property,
				object caller) {
			Action<T> del = null;
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(property))
				throw new ArgumentNullException(nameof(property));
			var reflectMethod = GetPropertySafe<T>(type, property, false)?.GetSetMethod(true);
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(Action<T>), caller, reflectMethod, false)
					as Action<T>;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private instance property setter. This delegate is over
		/// ten times faster than reflection, so useful if called frequently on the same object.
		/// 
		/// This method does not work on indexed properties.
		/// </summary>
		/// <typeparam name="T">The property's type.</typeparam>
		/// <param name="property">The target property.</param>
		/// <param name="caller">The object on which to call the property setter.</param>
		/// <returns>A delegate which calls this property's setter, or null if the property
		/// was null or did not match the type.</returns>
		public static Action<T> CreateSetDelegate<T>(this PropertyInfo property,
				object caller) {
			Action<T> del = null;
			var reflectMethod = property?.GetSetMethod(true);
			if (reflectMethod != null && property.PropertyType.IsAssignableFrom(typeof(T)))
				del = Delegate.CreateDelegate(typeof(Action<T>), caller, reflectMethod, false)
					as Action<T>;
			return del;
		}

		/// <summary>
		/// Creates a delegate for a private static method. This delegate is over ten times
		/// faster than reflection, so useful if called frequently.
		/// </summary>
		/// <typeparam name="T">A delegate type which matches the method signature.</typeparam>
		/// <param name="type">The declaring type of the target method.</param>
		/// <param name="method">The target method name.</param>
		/// <param name="argumentTypes">The types of the target method arguments, or PPatchTools.
		/// AnyArguments (not recommended, type safety is good) to match any static method with
		/// that name.</param>
		/// <returns>A delegate which calls this method, or null if the method could not be
		/// found or did not match the types.</returns>
		public static T CreateStaticDelegate<T>(this Type type, string method,
				params Type[] argumentTypes) where T : Delegate {
			var del = default(T);
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(method))
				throw new ArgumentNullException(nameof(method));
			var reflectMethod = GetMethodSafe(type, method, true, argumentTypes);
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(T), reflectMethod, false) as T;
			return del;
		}

		/// <summary>
		/// Replaces method calls in a transpiled method.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="translation">A mapping from the old method calls to replace, to the
		/// new method calls to use instead.</param>
		/// <returns>A transpiled version of that method that replaces or removes all calls
		/// to the specified methods.</returns>
		private static TranspiledMethod DoReplaceMethodCalls(TranspiledMethod method,
				IDictionary<MethodInfo, MethodInfo> translation) {
			var remove = RemoveCall;
			int replaced = 0;
			foreach (var instruction in method) {
				var opcode = instruction.opcode;
				if ((opcode == OpCodes.Call || opcode == OpCodes.Calli || opcode == OpCodes.
						Callvirt) && instruction.operand is MethodInfo target && translation.
						TryGetValue(target, out MethodInfo newMethod)) {
					if (newMethod != null && newMethod != remove) {
						// Replace with new method
						instruction.opcode = newMethod.IsStatic ? OpCodes.Call :
							OpCodes.Callvirt;
						instruction.operand = newMethod;
						yield return instruction;
					} else {
						// Pop "this" if needed
						int n = target.GetParameters().Length;
						if (!target.IsStatic) n++;
						// Pop the arguments off the stack
						instruction.opcode = (n == 0) ? OpCodes.Nop : OpCodes.Pop;
						instruction.operand = null;
						yield return instruction;
						for (int i = 0; i < n - 1; i++)
							yield return new CodeInstruction(OpCodes.Pop);
					}
					replaced++;
				} else
					yield return instruction;
			}
#if DEBUG
			if (replaced == 0) {
				if (translation.Count == 1) {
					// Diagnose the method that could not be replaced
					var items = new KeyValuePair<MethodInfo, MethodInfo>[1];
					translation.CopyTo(items, 0);
					MethodInfo from = items[0].Key, to = items[0].Value;
					PUtil.LogWarning("No method calls replaced: {0}.{1} to {2}.{3}".F(
						from.DeclaringType.FullName, from.Name, to.DeclaringType.FullName,
						to.Name));
				} else
					PUtil.LogWarning("No method calls replaced (multiple replacements)");
			}
#endif
		}

		/// <summary>
		/// Dumps the IL body of the method to the debug log.
		/// 
		/// Only to be used for debugging purposes.
		/// </summary>
		/// <param name="opcodes">The IL instructions to log.</param>
		public static void DumpMethodBody(TranspiledMethod opcodes) {
			var result = new StringBuilder(1024);
			result.AppendLine("METHOD BODY:");
			foreach (var instr in opcodes) {
				foreach (var block in instr.blocks) {
					var type = block.blockType;
					if (type == ExceptionBlockType.EndExceptionBlock)
						result.AppendLine("}");
					else {
						if (type != ExceptionBlockType.BeginExceptionBlock)
							result.Append("} ");
						result.Append(block.blockType);
						result.AppendLine(" {");
					}
				}
				foreach (var label in instr.labels) {
					// Label hashcodes are just easy integers
					result.Append(label.GetHashCode());
					result.Append(": ");
				}
				result.Append('\t');
				result.Append(instr.opcode);
				var operand = instr.operand;
				if (operand != null) {
					result.Append('\t');
					if (operand is Label)
						result.Append(operand.GetHashCode());
					else if (operand is MethodBase method)
						FormatMethodCall(result, method);
					else
						result.Append(FormatArgument(operand));
				}
				result.AppendLine();
			}
			PUtil.LogDebug(result.ToString());
		}

		/// <summary>
		/// This method was taken directly from Harmony (https://github.com/pardeike/Harmony)
		/// which is also available under the MIT License.
		/// </summary>
		/// <param name="argument">The argument to format.</param>
		/// <returns>The IL argument in string form.</returns>
		private static string FormatArgument(object argument) {
			if (argument is null) return "NULL";

			if (argument is MethodBase method)
				return method.FullDescription();

			if (argument is FieldInfo field)
				return $"{field.FieldType.FullDescription()} {field.DeclaringType.FullDescription()}::{field.Name}";

			if (argument is Label label)
				return $"Label{label.GetHashCode()}";

			if (argument is Label[] labels) {
				int n = labels.Length;
				string[] labelCodes = new string[n];
				for (int i = 0; i < n; i++)
					labelCodes[i] = labels[i].GetHashCode().ToString();
				return $"Labels{labelCodes.Join(",")}";
			}

			if (argument is LocalBuilder lb)
				return $"{lb.LocalIndex} ({lb.LocalType})";

			if (argument is string sval)
				return sval.ToString().ToLiteral();

			return argument.ToString().Trim();
		}

		/// <summary>
		/// Formats a method call for logging.
		/// </summary>
		/// <param name="result">The location where the log is stored.</param>
		/// <param name="method">The method that is called.</param>
		private static void FormatMethodCall(StringBuilder result, MethodBase method) {
			bool first = true;
			// The default representation leaves off the class name!
			if (method is MethodInfo hasRet) {
				result.Append(hasRet.ReturnType.Name);
				result.Append(' ');
			}
			result.Append(method.DeclaringType.Name);
			result.Append('.');
			result.Append(method.Name);
			result.Append('(');
			// Put the default value in there too
			foreach (var pr in method.GetParameters()) {
				string paramName = pr.Name;
				if (!first)
					result.Append(", ");
				result.Append(pr.ParameterType.Name);
				if (!string.IsNullOrEmpty(paramName)) {
					result.Append(' ');
					result.Append(paramName);
				}
				if (pr.IsOptional) {
					result.Append(" = ");
					result.Append(pr.DefaultValue);
				}
				first = false;
			}
			result.Append(')');
		}

		/// <summary>
		/// Retrieves a field using reflection, or returns null if it does not exist.
		/// </summary>
		/// <param name="type">The base type.</param>
		/// <param name="fieldName">The field name.</param>
		/// <param name="isStatic">true to find static fields, or false to find instance
		/// fields.</param>
		/// <returns>The field, or null if no such field could be found.</returns>
		public static FieldInfo GetFieldSafe(this Type type, string fieldName,
				bool isStatic) {
			FieldInfo field = null;
			if (type != null)
				try {
					var flag = isStatic ? BindingFlags.Static : BindingFlags.Instance;
					field = type.GetField(fieldName, BASE_FLAGS | flag);
				} catch (AmbiguousMatchException e) {
					PUtil.LogException(e);
				}
			return field;
		}

		/// <summary>
		/// Creates a store instruction to the same local as the specified load instruction.
		/// </summary>
		/// <param name="load">The initial load instruction.</param>
		/// <returns>The counterbalancing store instruction.</returns>
		public static CodeInstruction GetMatchingStoreInstruction(CodeInstruction load) {
			CodeInstruction instr;
			var opcode = load.opcode;
			if (opcode == OpCodes.Ldloc)
				instr = new CodeInstruction(OpCodes.Stloc, load.operand);
			else if (opcode == OpCodes.Ldloc_S)
				instr = new CodeInstruction(OpCodes.Stloc_S, load.operand);
			else if (opcode == OpCodes.Ldloc_0)
				instr = new CodeInstruction(OpCodes.Stloc_0);
			else if (opcode == OpCodes.Ldloc_1)
				instr = new CodeInstruction(OpCodes.Stloc_1);
			else if (opcode == OpCodes.Ldloc_2)
				instr = new CodeInstruction(OpCodes.Stloc_2);
			else if (opcode == OpCodes.Ldloc_3)
				instr = new CodeInstruction(OpCodes.Stloc_3);
			else
				instr = new CodeInstruction(OpCodes.Pop);
			return instr;
		}

		/// <summary>
		/// Retrieves a method using reflection, or returns null if it does not exist.
		/// </summary>
		/// <param name="type">The base type.</param>
		/// <param name="methodName">The method name.</param>
		/// <param name="isStatic">true to find static methods, or false to find instance
		/// methods.</param>
		/// <param name="arguments">The method argument types. If null is provided, any
		/// argument types are matched, whereas no arguments match only void methods.</param>
		/// <returns>The method, or null if no such method could be found.</returns>
		public static MethodInfo GetMethodSafe(this Type type, string methodName,
				bool isStatic, params Type[] arguments) {
			MethodInfo method = null;
			if (type != null && arguments != null)
				try {
					var flag = isStatic ? BindingFlags.Static : BindingFlags.Instance;
					if (arguments.Length == 1 && arguments[0] == null)
						// AnyArguments
						method = type.GetMethod(methodName, BASE_FLAGS | flag);
					else
						method = type.GetMethod(methodName, BASE_FLAGS | flag, null, arguments,
							new ParameterModifier[arguments.Length]);
				} catch (AmbiguousMatchException e) {
					PUtil.LogException(e);
				}
			return method;
		}

		/// <summary>
		/// Retrieves a property using reflection, or returns null if it does not exist.
		/// </summary>
		/// <param name="type">The base type.</param>
		/// <param name="propName">The property name.</param>
		/// <param name="isStatic">true to find static properties, or false to find instance
		/// properties.</param>
		/// <typeparam name="T">The property field type.</typeparam>
		/// <returns>The property, or null if no such property could be found.</returns>
		public static PropertyInfo GetPropertySafe<T>(this Type type, string propName,
				bool isStatic) {
			PropertyInfo field = null;
			if (type != null)
				try {
					var flag = isStatic ? BindingFlags.Static : BindingFlags.Instance;
					field = type.GetProperty(propName, BASE_FLAGS | flag, null, typeof(T),
						Type.EmptyTypes, null);
				} catch (AmbiguousMatchException e) {
					PUtil.LogException(e);
				}
			return field;
		}

		/// <summary>
		/// Retrieves an indexed property using reflection, or returns null if it does not
		/// exist.
		/// </summary>
		/// <param name="type">The base type.</param>
		/// <param name="propName">The property name.</param>
		/// <param name="isStatic">true to find static properties, or false to find instance
		/// properties.</param>
		/// <param name="arguments">The property indexer's arguments.</param>
		/// <typeparam name="T">The property field type.</typeparam>
		/// <returns>The property, or null if no such property could be found.</returns>
		public static PropertyInfo GetPropertyIndexedSafe<T>(this Type type, string propName,
				bool isStatic, params Type[] arguments) {
			PropertyInfo field = null;
			if (type != null && arguments != null)
				try {
					var flag = isStatic ? BindingFlags.Static : BindingFlags.Instance;
					field = type.GetProperty(propName, BASE_FLAGS | flag, null, typeof(T),
						arguments, new ParameterModifier[arguments.Length]);
				} catch (AmbiguousMatchException e) {
					PUtil.LogException(e);
				}
			return field;
		}

		/// <summary>
		/// Retrieves a type using its full name (including namespace). However, the assembly
		/// name is optional, as this method searches all assemblies in the current
		/// AppDomain if it is null or empty.
		/// </summary>
		/// <param name="name">The type name to retrieve.</param>
		/// <param name="assemblyName">If specified, the name of the assembly that contains
		/// the type. No other assembly name will be searched if this parameter is not null
		/// or empty. The assembly name might not match the DLL name, use a decompiler to
		/// make sure.</param>
		/// <returns>The type, or null if the type is not found or cannot be loaded.</returns>
		public static Type GetTypeSafe(string name, string assemblyName = null) {
			Type type = null;
			if (string.IsNullOrEmpty(assemblyName))
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					try {
						type = assembly.GetType(name, false);
					} catch (System.IO.IOException) {
						// The common parent of exceptions when the type requires another type
						// that cannot be loaded
					} catch (BadImageFormatException) { }
					if (type != null) break;
				}
			else {
				try {
					type = Type.GetType(name + ", " + assemblyName, false);
				} catch (TargetInvocationException e) {
					PUtil.LogWarning("Unable to load type {0} from assembly {1}:".F(name,
						assemblyName));
					PUtil.LogExcWarn(e);
				} catch (ArgumentException e) {
					// A generic type is loaded with bad arguments
					PUtil.LogWarning("Unable to load type {0} from assembly {1}:".F(name,
						assemblyName));
					PUtil.LogExcWarn(e);
				} catch (System.IO.IOException) {
					// The common parent of exceptions when the type requires another type that
					// cannot be loaded
				} catch (BadImageFormatException) { }
			}
			return type;
		}

		/// <summary>
		/// Checks to see if a patch with the specified method name (the method used in the
		/// patch class) and type is defined.
		/// </summary>
		/// <param name="instance">The Harmony instance to query for patches. Unused.</param>
		/// <param name="target">The target method to search for patches.</param>
		/// <param name="type">The patch type to look up.</param>
		/// <param name="name">The patch method name to look up (name as declared by patch owner).</param>
		/// <returns>true if such a patch was found, or false otherwise</returns>
		public static bool HasPatchWithMethodName(Harmony instance, MethodBase target,
				HarmonyPatchType type, string name) {
			bool found = false;
			if (target == null)
				throw new ArgumentNullException(nameof(target));
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			var patches = Harmony.GetPatchInfo(target);
			if (patches != null) {
				ICollection<Patch> patchList;
				switch (type) {
				case HarmonyPatchType.Prefix:
					patchList = patches.Prefixes;
					break;
				case HarmonyPatchType.Postfix:
					patchList = patches.Postfixes;
					break;
				case HarmonyPatchType.Transpiler:
					patchList = patches.Transpilers;
					break;
				case HarmonyPatchType.All:
				default:
					// All
					if (patches.Transpilers != null)
						found = HasPatchWithMethodName(patches.Transpilers, name);
					if (patches.Prefixes != null)
						found = found || HasPatchWithMethodName(patches.Prefixes, name);
					patchList = patches.Postfixes;
					break;
				}
				if (patchList != null)
					found = found || HasPatchWithMethodName(patchList, name);
			}
			return found;
		}

		/// <summary>
		/// Checks to see if the patch list has a method with the specified name.
		/// </summary>
		/// <param name="patchList">The patch list to search.</param>
		/// <param name="name">The declaring method name to look up.</param>
		/// <returns>true if a patch matches that name, or false otherwise</returns>
		private static bool HasPatchWithMethodName(IEnumerable<Patch> patchList, string name) {
			bool found = false;
			foreach (var patch in patchList)
				if (patch.PatchMethod.Name == name) {
					found = true;
					break;
				}
			return found;
		}

		/// <summary>
		/// Checks to see if an instruction opcode is a branch instruction.
		/// </summary>
		/// <param name="opcode">The opcode to check.</param>
		/// <returns>true if it is a branch, or false otherwise.</returns>
		public static bool IsConditionalBranchInstruction(this OpCode opcode) {
			return PTranspilerTools.IsConditionalBranchInstruction(opcode);
		}

		/// <summary>
		/// Adds a logger to all unhandled exceptions.
		/// </summary>
		[Obsolete("Do not use this method in production code. Make sure to remove it in release builds, or disable it with #if DEBUG.")]
		public static void LogAllExceptions() {
			PUtil.LogWarning("PLib in mod " + Assembly.GetCallingAssembly().GetName()?.Name +
				" is logging ALL unhandled exceptions!");
			PTranspilerTools.LogAllExceptions();
		}

		/// <summary>
		/// Adds a logger to all failed assertions. The assertions will still fail, but a stack
		/// trace will be printed for each failed assertion.
		/// </summary>
		[Obsolete("Do not use this method in production code. Make sure to remove it in release builds, or disable it with #if DEBUG.")]
		public static void LogAllFailedAsserts() {
			PUtil.LogWarning("PLib in mod " + Assembly.GetCallingAssembly().GetName()?.Name +
				" is logging ALL failed assertions!");
			PTranspilerTools.LogAllFailedAsserts();
		}

		/// <summary>
		/// Transpiles a method to replace instances of one constant value with another.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="oldValue">The old constant to remove.</param>
		/// <param name="newValue">The new constant to replace.</param>
		/// <param name="all">true to replace all instances, or false to replace the first
		/// instance (default).</param>
		/// <returns>A transpiled version of that method which replaces instances of the first
		/// constant with that of the second.</returns>
		public static TranspiledMethod ReplaceConstant(TranspiledMethod method,
				double oldValue, double newValue, bool all = false) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			int replaced = 0;
			foreach (var inst in method) {
				var instruction = inst;
				var opcode = instruction.opcode;
				if ((opcode == OpCodes.Ldc_R8 && (instruction.operand is double dval) &&
						dval == oldValue)) {
					// Replace instruction if first instance, or all to be replaced
					if (all || replaced == 0)
						instruction.operand = newValue;
					replaced++;
				}
				yield return instruction;
			}
		}

		/// <summary>
		/// Transpiles a method to replace instances of one constant value with another.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="oldValue">The old constant to remove.</param>
		/// <param name="newValue">The new constant to replace.</param>
		/// <param name="all">true to replace all instances, or false to replace the first
		/// instance (default).</param>
		/// <returns>A transpiled version of that method which replaces instances of the first
		/// constant with that of the second.</returns>
		public static TranspiledMethod ReplaceConstant(TranspiledMethod method, float oldValue,
				float newValue, bool all = false) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			int replaced = 0;
			foreach (var inst in method) {
				var instruction = inst;
				var opcode = instruction.opcode;
				if ((opcode == OpCodes.Ldc_R4 && (instruction.operand is float fval) &&
						fval == oldValue)) {
					// Replace instruction if first instance, or all to be replaced
					if (all || replaced == 0)
						instruction.operand = newValue;
					replaced++;
				}
				yield return instruction;
			}
		}

		/// <summary>
		/// Transpiles a method to replace instances of one constant value with another.
		/// 
		/// Note that values of type byte, short, char, and bool are also represented with "i4"
		/// constants which can be targeted by this method.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="oldValue">The old constant to remove.</param>
		/// <param name="newValue">The new constant to replace.</param>
		/// <param name="all">true to replace all instances, or false to replace the first
		/// instance (default).</param>
		/// <returns>A transpiled version of that method which replaces instances of the first
		/// constant with that of the second.</returns>
		public static TranspiledMethod ReplaceConstant(TranspiledMethod method, int oldValue,
				int newValue, bool all = false) {
			int replaced = 0;
			bool quickCode = oldValue >= -1 && oldValue <= 8;
			var qc = OpCodes.Nop;
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			// Quick test for the opcode on the shorthand forms
			if (quickCode)
				qc = PTranspilerTools.LOAD_INT[oldValue + 1];
			foreach (var inst in method) {
				var instruction = inst;
				var opcode = instruction.opcode;
				object operand = instruction.operand;
				if ((opcode == OpCodes.Ldc_I4 && (operand is int ival) && ival == oldValue) ||
						(opcode == OpCodes.Ldc_I4_S && (operand is byte bval) && bval ==
						oldValue) || (quickCode && qc == opcode)) {
					// Replace instruction if first instance, or all to be replaced
					if (all || replaced == 0)
						PTranspilerTools.ModifyLoadI4(instruction, newValue);
					replaced++;
				}
				yield return instruction;
			}
		}

		/// <summary>
		/// Transpiles a method to replace instances of one constant value with another.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="oldValue">The old constant to remove.</param>
		/// <param name="newValue">The new constant to replace.</param>
		/// <param name="all">true to replace all instances, or false to replace the first
		/// instance (default).</param>
		/// <returns>A transpiled version of that method which replaces instances of the first
		/// constant with that of the second.</returns>
		public static TranspiledMethod ReplaceConstant(TranspiledMethod method, long oldValue,
				long newValue, bool all = false) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			int replaced = 0;
			foreach (var inst in method) {
				var instruction = inst;
				var opcode = instruction.opcode;
				if ((opcode == OpCodes.Ldc_I8 && (instruction.operand is long lval) &&
						lval == oldValue)) {
					// Replace instruction if first instance, or all to be replaced
					if (all || replaced == 0)
						instruction.operand = newValue;
					replaced++;
				}
				yield return instruction;
			}
		}

		/// <summary>
		/// Transpiles a method to remove all calls to the specified victim method.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="victim">The old method calls to remove.</param>
		/// <returns>A transpiled version of that method that removes all calls to method.</returns>
		/// <exception cref="ArgumentException">If the method being removed had a return value
		/// (with what would it be replaced?).</exception>
		public static TranspiledMethod RemoveMethodCall(TranspiledMethod method,
				MethodInfo victim) {
			return ReplaceMethodCallSafe(method, new Dictionary<MethodInfo, MethodInfo>() {
				{ victim, RemoveCall }
			});
		}

		/// <summary>
		/// A placeholder method for signaling call removal. Not actually called.
		/// </summary>
		private static void RemoveMethodCallPrivate() { }

		/// <summary>
		/// Transpiles a method to replace all calls to the specified victim method with
		/// another method, altering the call type if necessary. The argument types and return
		/// type must match exactly, including in/out/ref parameters.
		/// 
		/// If replacing an instance method call with a static method, the first argument
		/// will receive the "this" which the old method would have received.
		/// 
		/// If newMethod is null, the calls will all be removed silently instead. This will
		/// fail if the method call being removed had a return type (what would it be replaced
		/// with?); in those cases, declare an empty method with the same signature and
		/// replace it instead.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="victim">The old method calls to remove.</param>
		/// <param name="newMethod">The new method to replace, or null to delete the calls.</param>
		/// <returns>A transpiled version of that method that replaces or removes all calls
		/// to method.</returns>
		/// <exception cref="ArgumentException">If the new method's argument types do not
		/// exactly match the old method's argument types.</exception>
		[Obsolete("This method is unsafe. Use the RemoveMethodCall or ReplaceMethodCallSafe versions instead.")]
		public static TranspiledMethod ReplaceMethodCall(TranspiledMethod method,
				MethodInfo victim, MethodInfo newMethod = null) {
			if (newMethod == null)
				newMethod = RemoveCall;
			return ReplaceMethodCallSafe(method, new Dictionary<MethodInfo, MethodInfo>() {
				{ victim, newMethod }
			});
		}

		/// <summary>
		/// Transpiles a method to replace all calls to the specified victim method with
		/// another method, altering the call type if necessary. The argument types and return
		/// type must match exactly, including in/out/ref parameters.
		/// 
		/// If replacing an instance method call with a static method, the first argument
		/// will receive the "this" which the old method would have received.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="victim">The old method calls to remove.</param>
		/// <param name="newMethod">The new method to replace.</param>
		/// <returns>A transpiled version of that method that replaces all calls to method.</returns>
		/// <exception cref="ArgumentException">If the new method's argument types do not
		/// exactly match the old method's argument types.</exception>
		public static TranspiledMethod ReplaceMethodCallSafe(TranspiledMethod method,
				MethodInfo victim, MethodInfo newMethod) {
			if (newMethod == null)
				throw new ArgumentNullException(nameof(newMethod));
			return ReplaceMethodCallSafe(method, new Dictionary<MethodInfo, MethodInfo>() {
				{ victim, newMethod }
			});
		}

		/// <summary>
		/// Transpiles a method to replace calls to the specified victim methods with
		/// replacement methods, altering the call type if necessary.
		/// 
		/// Each key to value pair must meet the criteria defined in
		/// ReplaceMethodCall(TranspiledMethod, MethodInfo, MethodInfo).
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="translation">A mapping from the old method calls to replace, to the
		/// new method calls to use instead.</param>
		/// <returns>A transpiled version of that method that replaces or removes all calls
		/// to the specified methods.</returns>
		/// <exception cref="ArgumentException">If any of the new methods' argument types do
		/// not exactly match the old methods' argument types.</exception>
		[Obsolete("This method is unsafe. Use ReplaceMethodCallSafe instead.")]
		public static TranspiledMethod ReplaceMethodCall(TranspiledMethod method,
				IDictionary<MethodInfo, MethodInfo> translation) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			if (translation == null)
				throw new ArgumentNullException(nameof(translation));
			// Sanity check arguments
			foreach (var pair in translation) {
				var victim = pair.Key;
				var newMethod = pair.Value;
				if (victim == null)
					throw new ArgumentNullException(nameof(victim));
				if (newMethod != null)
					PTranspilerTools.CompareMethodParams(victim, victim.GetParameterTypes(),
						newMethod);
				else if (victim.ReturnType != typeof(void))
					throw new ArgumentException("Cannot remove method {0} with a return value".
						F(victim.Name));
			}
			return DoReplaceMethodCalls(method, translation);
		}

		/// <summary>
		/// Transpiles a method to replace calls to the specified victim methods with
		/// replacement methods, altering the call type if necessary.
		/// 
		/// Each key to value pair must meet the criteria defined in
		/// ReplaceMethodCallSafe(TranspiledMethod, MethodInfo, MethodInfo).
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="translation">A mapping from the old method calls to replace, to the
		/// new method calls to use instead.</param>
		/// <returns>A transpiled version of that method that replaces or removes all calls
		/// to the specified methods.</returns>
		/// <exception cref="ArgumentException">If any of the new methods' argument types do
		/// not exactly match the old methods' argument types.</exception>
		public static TranspiledMethod ReplaceMethodCallSafe(TranspiledMethod method,
				IDictionary<MethodInfo, MethodInfo> translation) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			if (translation == null)
				throw new ArgumentNullException(nameof(translation));
			// Sanity check arguments
			var remove = RemoveCall;
			foreach (var pair in translation) {
				var victim = pair.Key;
				var newMethod = pair.Value;
				if (victim == null)
					throw new ArgumentNullException(nameof(victim));
				if (newMethod == null)
					throw new ArgumentNullException(nameof(newMethod));
				if (newMethod == remove) {
					if (victim.ReturnType != typeof(void))
						throw new ArgumentException("Cannot remove method {0} with a return value".
							F(victim.Name));
				} else
					PTranspilerTools.CompareMethodParams(victim, victim.GetParameterTypes(),
						newMethod);
			}
			return DoReplaceMethodCalls(method, translation);
		}

		/// <summary>
		/// Attempts to read a static field value from an object of a type not in this assembly.
		/// 
		/// If this operation is expected to be performed more than once on the same object,
		/// use a delegate. If the type of the object is known, use Detours.
		/// </summary>
		/// <typeparam name="T">The type of the value to read.</typeparam>
		/// <param name="type">The type whose static field should be read.</param>
		/// <param name="name">The field name.</param>
		/// <param name="value">The location where the field value will be stored.</param>
		/// <returns>true if the field was read, or false if the field was not found or
		/// has the wrong type.</returns>
		public static bool TryGetFieldValue<T>(Type type, string name, out T value) {
			bool ok = false;
			if (type != null && !string.IsNullOrEmpty(name)) {
				var field = type.GetFieldSafe(name, true);
				if (field != null && field.GetValue(null) is T newValue) {
					ok = true;
					value = newValue;
				} else
					value = default;
			} else
				value = default;
			return ok;
		}

		/// <summary>
		/// Attempts to read a non-static field value from an object of a type not in this
		/// assembly.
		/// 
		/// If this operation is expected to be performed more than once on the same object,
		/// use a delegate. If the type of the object is known, use Detours.
		/// </summary>
		/// <typeparam name="T">The type of the value to read.</typeparam>
		/// <param name="source">The source object.</param>
		/// <param name="name">The field name.</param>
		/// <param name="value">The location where the field value will be stored.</param>
		/// <returns>true if the field was read, or false if the field was not found or
		/// has the wrong type.</returns>
		public static bool TryGetFieldValue<T>(object source, string name, out T value) {
			bool ok = false;
			if (source != null && !string.IsNullOrEmpty(name)) {
				var type = source.GetType();
				var field = type.GetFieldSafe(name, false);
				if (field != null && field.GetValue(source) is T newValue) {
					ok = true;
					value = newValue;
				} else
					value = default;
			} else
				value = default;
			return ok;
		}

		/// <summary>
		/// Attempts to read a property value from an object of a type not in this assembly.
		/// 
		/// If this operation is expected to be performed more than once on the same object,
		/// use a delegate. If the type of the object is known, use Detours.
		/// </summary>
		/// <typeparam name="T">The type of the value to read.</typeparam>
		/// <param name="source">The source object.</param>
		/// <param name="name">The property name.</param>
		/// <param name="value">The location where the property value will be stored.</param>
		/// <returns>true if the property was read, or false if the property was not found or
		/// has the wrong type.</returns>
		public static bool TryGetPropertyValue<T>(object source, string name, out T value) {
			bool ok = false;
			if (source != null && !string.IsNullOrEmpty(name)) {
				var type = source.GetType();
				var prop = type.GetPropertySafe<T>(name, false);
				ParameterInfo[] indexes;
				if (prop != null && ((indexes = prop.GetIndexParameters()) == null || indexes.
						Length < 1) && prop.GetValue(source, null) is T newValue) {
					ok = true;
					value = newValue;
				} else
					value = default;
			} else
				value = default;
			return ok;
		}

		/// <summary>
		/// Transpiles a method to wrap it with a try/catch that logs and rethrows all
		/// exceptions.
		/// </summary>
		/// <param name="method">The method body to patch.</param>
		/// <param name="generator">The IL generator to make labels.</param>
		/// <returns>A transpiled version of that method that is wrapped with an error
		/// logger.</returns>
		public static TranspiledMethod WrapWithErrorLogger(TranspiledMethod method,
				ILGenerator generator) {
			var logger = typeof(PUtil).GetMethodSafe(nameof(PUtil.LogException), true,
				typeof(Exception));
			var ee = method.GetEnumerator();
			// Emit all but the last instruction
			if (ee.MoveNext()) {
				CodeInstruction last;
				bool hasNext, isFirst = true;
				var endMethod = generator.DefineLabel();
				do {
					last = ee.Current;
					if (isFirst)
						last.blocks.Add(new ExceptionBlock(ExceptionBlockType.
							BeginExceptionBlock, null));
					hasNext = ee.MoveNext();
					isFirst = false;
					if (hasNext)
						yield return last;
				} while (hasNext);
				CodeInstruction startHandler;
				// Preserves the labels "ret" might have had
				last.opcode = OpCodes.Nop;
				last.operand = null;
				yield return last;
				// Add a "leave"
				yield return new CodeInstruction(OpCodes.Leave, endMethod);
				// The exception is already on the stack
				if (logger != null)
					startHandler = new CodeInstruction(OpCodes.Call, logger);
				else
					startHandler = new CodeInstruction(OpCodes.Pop);
				startHandler.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock,
					typeof(Exception)));
				yield return startHandler;
				// Rethrow exception
				yield return new CodeInstruction(OpCodes.Rethrow);
				// End catch block
				var endCatch = new CodeInstruction(OpCodes.Leave, endMethod);
				endCatch.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock,
					null));
				yield return endCatch;
				// Actual new ret
				var ret = new CodeInstruction(OpCodes.Ret);
				ret.labels.Add(endMethod);
				yield return ret;
			} // Otherwise, there were no instructions to wrap
		}
	}
}
