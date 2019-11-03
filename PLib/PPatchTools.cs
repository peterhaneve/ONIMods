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
		/// <param name="newMethod">The replacement method.</param>
		private static void CompareMethodParams(MethodInfo victim, Type[] paramTypes,
				MethodInfo newMethod) {
			int n = paramTypes.Length;
			Type[] newTypes, initTypes = newMethod.GetParameterTypes();
			if (!victim.IsStatic && newMethod.IsStatic) {
				// Allow special case of passing "this" as first static arg
				var newParamTypes = new List<Type>(initTypes);
				newParamTypes.Insert(0, victim.DeclaringType);
				newTypes = newParamTypes.ToArray();
			} else
				newTypes = initTypes;
			// Argument count check
			if (newTypes.Length != n)
				throw new ArgumentException("New method {0} ({1:D} arguments) does not " +
					"match method {2} ({3:D} arguments)".F(newMethod.Name, newTypes.Length,
					victim.Name, n));
			// Argument type check
			for (int i = 0; i < n; i++)
				if (paramTypes[i] != newTypes[i])
					throw new ArgumentException("Argument {0:D}: New method type {1} does " +
						"not match old method type {2}".F(i, paramTypes[i].FullName,
						newTypes[i].FullName));
			if (victim.ReturnType != newMethod.ReturnType)
				throw new ArgumentException("New method {0} (returns {1}) does not match " +
					"method {2} (returns {3})".F(newMethod.Name, newMethod.
					ReturnType, victim.Name, victim.ReturnType));
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
		/// Adds a logger to all unhandled exceptions.
		/// </summary>
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
			if (method == null)
				throw new ArgumentNullException("method");
			if (victim == null)
				throw new ArgumentNullException("victim");
			// Sanity check arguments
			var types = victim.GetParameterTypes();
			int n = types.Length;
			if (newMethod != null)
				CompareMethodParams(victim, types, newMethod);
			else if (victim.ReturnType != typeof(void))
				throw new ArgumentException("Cannot remove method {0} with a return value".F(
					victim.Name));
			// Pop "this" in removal cases
			if (!victim.IsStatic) n++;
			foreach (var instruction in method) {
				var opcode = instruction.opcode;
				if ((opcode == OpCodes.Call || opcode == OpCodes.Calli || opcode == OpCodes.
						Callvirt) && instruction.operand == victim) {
					if (newMethod != null) {
						// Replace with new method
						instruction.opcode = newMethod.IsStatic ? OpCodes.Call :
							OpCodes.Callvirt;
						instruction.operand = newMethod;
						yield return instruction;
					} else {
						// Pop the arguments off the stack
						instruction.opcode = (n == 0) ? OpCodes.Nop : OpCodes.Pop;
						instruction.operand = null;
						yield return instruction;
						for (int i = 0; i < n - 1; i++)
							yield return new CodeInstruction(OpCodes.Pop);
					}
				} else
					yield return instruction;
			}
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
