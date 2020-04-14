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
using Harmony.ILCopying;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using TranspiledMethod = System.Collections.Generic.IEnumerable<Harmony.CodeInstruction>;

namespace PeterHan.PLib {
	/// <summary>
	/// Contains tools to aid with patching.
	/// </summary>
	public static class PPatchTools {
		/// <summary>
		/// The base binding flags for all reflection methods.
		/// </summary>
		private const BindingFlags BASE_FLAGS = BindingFlags.Public | BindingFlags.NonPublic;

		/// <summary>
		/// Opcodes to load an integer onto the stack.
		/// </summary>
		private static readonly OpCode[] LOAD_INT = {
			OpCodes.Ldc_I4_M1, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2,
			OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6,
			OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8
		};

		/// <summary>
		/// Passed to GetMethodSafe to match any method arguments.
		/// </summary>
		public static Type[] AnyArguments {
			get {
				return new Type[] { null };
			}
		}

		/// <summary>
		/// Compares the method parameters and throws ArgumentException if they do not match.
		/// </summary>
		/// <param name="victim">The victim method.</param>
		/// <param name="paramTypes">The method's parameter types.</param>
		/// <param name="newMethod">The replacement method.</param>
		private static void CompareMethodParams(MethodInfo victim, Type[] paramTypes,
				MethodInfo newMethod) {
			Type[] newTypes = newMethod.GetParameterTypes();
			if (!newMethod.IsStatic)
				newTypes = PushDeclaringType(newTypes, newMethod.DeclaringType);
			if (!victim.IsStatic)
				paramTypes = PushDeclaringType(paramTypes, victim.DeclaringType);
			int n = paramTypes.Length;
			// Argument count check
			if (newTypes.Length != n)
				throw new ArgumentException(("New method {0} ({1:D} arguments) does not " +
					"match method {2} ({3:D} arguments)").F(newMethod.Name, newTypes.Length,
					victim.Name, n));
			// Argument type check
			for (int i = 0; i < n; i++)
				if (!newTypes[i].IsAssignableFrom(paramTypes[i]))
					throw new ArgumentException(("Argument {0:D}: New method type {1} does " +
						"not match old method type {2}").F(i, paramTypes[i].FullName,
						newTypes[i].FullName));
			if (!victim.ReturnType.IsAssignableFrom(newMethod.ReturnType))
				throw new ArgumentException(("New method {0} (returns {1}) does not match " +
					"method {2} (returns {3})").F(newMethod.Name, newMethod.
					ReturnType, victim.Name, victim.ReturnType));
		}

		/// <summary>
		/// Creates a delegate for a private instance method. This delegate is over ten times
		/// faster than reflection, so useful if called frequently on the same object.
		/// </summary>
		/// <typeparam name="T">A delegate type which matches the method signature.</typeparam>
		/// <param name="type">The declaring type of the target method.</param>
		/// <param name="method">The target method name.</param>
		/// <param name="caller">The object on which to call the method.</param>
		/// <param name="arguments">The types of the target method arguments, or PPatchTools.
		/// AnyArguments (not recommended, type safety is good) to match any static method with
		/// that name.</param>
		/// <returns>A delegate which calls this method, or null if the method could not be
		/// found or did not match the types.</returns>
		public static T CreateDelegate<T>(this Type type, string method, object caller,
				params Type[] arguments) where T : Delegate {
			var del = default(T);
			if (type == null)
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(method))
				throw new ArgumentNullException("method");
			var reflectMethod = GetMethodSafe(type, method, false, arguments);
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(T), caller, reflectMethod, false) as T;
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
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(property))
				throw new ArgumentNullException("property");
			var reflectMethod = GetPropertySafe<T>(type, property, false)?.GetGetMethod();
			if (reflectMethod != null)
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
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(property))
				throw new ArgumentNullException("property");
			var reflectMethod = GetPropertySafe<T>(type, property, false)?.GetSetMethod();
			if (reflectMethod != null)
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
		/// <param name="arguments">The types of the target method arguments, or PPatchTools.
		/// AnyArguments (not recommended, type safety is good) to match any static method with
		/// that name.</param>
		/// <returns>A delegate which calls this method, or null if the method could not be
		/// found or did not match the types.</returns>
		public static T CreateStaticDelegate<T>(this Type type, string method,
				params Type[] arguments) where T : Delegate {
			var del = default(T);
			if (type == null)
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(method))
				throw new ArgumentNullException("method");
			var reflectMethod = GetMethodSafe(type, method, true, arguments);
			if (reflectMethod != null)
				del = Delegate.CreateDelegate(typeof(T), reflectMethod, false) as T;
			return del;
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
		/// Gets the method's parameter types.
		/// </summary>
		/// <param name="method">The method to query.</param>
		/// <returns>The type of each parameter of the method.</returns>
		internal static Type[] GetParameterTypes(this MethodInfo method) {
			if (method == null)
				throw new ArgumentNullException("method");
			var pm = method.GetParameters();
			int n = pm.Length;
			var types = new Type[n];
			for (int i = 0; i < n; i++)
				types[i] = pm[i].ParameterType;
			return types;
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
		/// Adds a logger to all unhandled exceptions.
		/// </summary>
		[Obsolete("Do not use this method in production code. Make sure to remove it in release builds, or disable it with #if DEBUG.")]
		public static void LogAllExceptions() {
			// This is not for production use
			PUtil.LogWarning("PLib in mod " + Assembly.GetCallingAssembly().GetName()?.Name +
				" is logging ALL unhandled exceptions!");
			AppDomain.CurrentDomain.UnhandledException += OnThrown;
		}

		/// <summary>
		/// Adds a logger to all failed assertions. The assertions will still fail, but a stack
		/// trace will be printed for each failed assertion.
		/// </summary>
		[Obsolete("Do not use this method in production code. Make sure to remove it in release builds, or disable it with #if DEBUG.")]
		public static void LogAllFailedAsserts() {
			var inst = HarmonyInstance.Create("PeterHan.PLib.LogFailedAsserts");
			MethodBase assert;
			var handler = new HarmonyMethod(typeof(PPatchTools), nameof(OnAssertFailed));
			// This is not for production use
			PUtil.LogWarning("PLib in mod " + Assembly.GetCallingAssembly().GetName()?.Name +
				" is logging ALL failed assertions!");
			try {
				// Assert(bool)
				assert = GetMethodSafe(typeof(Debug), "Assert", true, typeof(bool));
				if (assert != null)
					inst.Patch(assert, handler);
				// Assert(bool, object)
				assert = GetMethodSafe(typeof(Debug), "Assert", true, typeof(bool), typeof(
					object));
				if (assert != null)
					inst.Patch(assert, handler);
				// Assert(bool, object, UnityEngine.Object)
				assert = GetMethodSafe(typeof(Debug), "Assert", true, typeof(bool), typeof(
					object), typeof(UnityEngine.Object));
				if (assert != null)
					inst.Patch(assert, handler);
			} catch (Exception e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Logs a failed assertion that is about to occur.
		/// </summary>
		private static void OnAssertFailed(bool condition) {
			if (!condition) {
				Debug.LogError("Assert is about to fail:");
				Debug.LogError(new System.Diagnostics.StackTrace().ToString());
			}
		}

		/// <summary>
		/// An optional handler for all unhandled exceptions.
		/// </summary>
		private static void OnThrown(object sender, UnhandledExceptionEventArgs e) {
			if (!e.IsTerminating) {
				Debug.LogError("Unhandled exception on Thread " + Thread.CurrentThread.Name);
				if (e.ExceptionObject is Exception ex)
					Debug.LogException(ex);
				else
					Debug.LogError(e.ExceptionObject);
			}
		}

		/// <summary>
		/// Inserts the declaring instance type to the front of the specified array.
		/// </summary>
		/// <param name="types">The parameter types.</param>
		/// <param name="declaringType">The type which declared this method.</param>
		/// <returns>The types with declaringType inserted at the beginning.</returns>
		private static Type[] PushDeclaringType(Type[] types, Type declaringType) {
			int n = types.Length;
			// Allow special case of passing "this" as first static arg
			var newParamTypes = new Type[n + 1];
			newParamTypes[0] = declaringType;
			for (int i = 0; i < n; i++)
				newParamTypes[i + 1] = types[i];
			return newParamTypes;
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
				throw new ArgumentNullException("method");
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
				throw new ArgumentNullException("method");
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
			if (method == null)
				throw new ArgumentNullException("method");
			int replaced = 0;
			bool quickCode = oldValue >= -1 && oldValue <= 8;
			// Quick test for the opcode on the shorthand forms
			OpCode qc = OpCodes.Nop;
			if (quickCode)
				qc = LOAD_INT[oldValue + 1];
			foreach (var inst in method) {
				var instruction = inst;
				var opcode = instruction.opcode;
				object operand = instruction.operand;
				if ((opcode == OpCodes.Ldc_I4 && (operand is int ival) && ival == oldValue) ||
						(opcode == OpCodes.Ldc_I4_S && (operand is byte bval) && bval ==
						oldValue) || (quickCode && qc == opcode)) {
					// Replace instruction if first instance, or all to be replaced
					if (all || replaced == 0) {
						if (newValue >= -1 && newValue <= 8) {
							// Short form: constant
							instruction.opcode = LOAD_INT[newValue + 1];
							instruction.operand = null;
						} else if (newValue >= byte.MinValue && newValue <= byte.MaxValue) {
							// Short form: 0-255
							instruction.opcode = OpCodes.Ldc_I4_S;
							instruction.operand = (byte)newValue;
						} else {
							// Long form
							instruction.opcode = OpCodes.Ldc_I4;
							instruction.operand = newValue;
						}
					}
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
				throw new ArgumentNullException("method");
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
		public static TranspiledMethod ReplaceMethodCall(TranspiledMethod method,
				MethodInfo victim, MethodInfo newMethod = null) {
			return ReplaceMethodCall(method, new Dictionary<MethodInfo, MethodInfo>() {
				{ victim, newMethod }
			});
		}

		/// <summary>
		/// Transpiles a method to replace calls to the specified victim methods with
		/// replacement methods, altering the call type if necessary.
		/// 
		/// Each key to value pair must meet the criteria defined in ReplaceMethodCall.
		/// </summary>
		/// <param name="method">The method to patch.</param>
		/// <param name="victim">The old method calls to remove.</param>
		/// <param name="newMethod">The new method to replace, or null to delete the calls.</param>
		/// <returns>A transpiled version of that method that replaces or removes all calls
		/// to the specified methods.</returns>
		/// <exception cref="ArgumentException">If any of the new methods' argument types do
		/// not exactly match the old methods' argument types.</exception>
		public static TranspiledMethod ReplaceMethodCall(TranspiledMethod method,
				IDictionary<MethodInfo, MethodInfo> translation) {
			if (method == null)
				throw new ArgumentNullException("method");
			if (translation == null)
				throw new ArgumentNullException("translation");
			// Sanity check arguments
			int replaced = 0;
			foreach (var pair in translation) {
				var victim = pair.Key;
				var newMethod = pair.Value;
				if (victim == null)
					throw new ArgumentNullException("victim");
				if (newMethod != null)
					CompareMethodParams(victim, victim.GetParameterTypes(), newMethod);
				else if (victim.ReturnType != typeof(void))
					throw new ArgumentException("Cannot remove method {0} with a return value".
						F(victim.Name));
			}
			foreach (var instruction in method) {
				var opcode = instruction.opcode;
				MethodInfo target;
				if ((opcode == OpCodes.Call || opcode == OpCodes.Calli || opcode == OpCodes.
						Callvirt) && translation.TryGetValue(target = instruction.operand as
						MethodInfo, out MethodInfo newMethod)) {
					if (newMethod != null) {
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
			if (replaced == 0)
				PUtil.LogWarning("No method calls replaced (multiple replacements)");
#endif
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
			CodeInstruction last = null;
			bool hasNext, isFirst = true;
			var endMethod = generator.DefineLabel();
			// Emit all but the last instruction
			if (ee.MoveNext())
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
			if (last != null) {
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
