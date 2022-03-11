/*
 * Copyright 2022 Peter Han
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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Patches the Components.Cmps class to avoid the KCompactedVector altogether.
	/// </summary>
	public static class FastCmps {
		/// <summary>
		/// The generic types to patch.
		/// </summary>
		private static readonly ISet<Type> TYPES_TO_PATCH = new HashSet<Type>();

		/// <summary>
		/// Generates method bodies for Add and Remove.
		/// </summary>
		private delegate TranspiledMethod GenerateBody(Type t, FieldInfo itemsField,
			Label end);

		/// <summary>
		/// Computes the types that will be patched since Harmony can only patch concrete
		/// implementations.
		/// </summary>
		private static void ComputeTargetTypes() {
			var target = typeof(Components.Cmps<>);
			var container = typeof(Components.CmpsByWorld<>);
			if (TYPES_TO_PATCH.Count < 1)
				foreach (var field in typeof(Components).GetFields(PPatchTools.BASE_FLAGS |
						BindingFlags.Static | BindingFlags.Instance)) {
					var type = field.FieldType;
					if (type.IsConstructedGenericType && !type.ContainsGenericParameters) {
						var def = type.GetGenericTypeDefinition();
						if (def == target || def == container) {
							// Components.CmpsByWorld<...>
							var subtype = type.GenericTypeArguments[0];
							TYPES_TO_PATCH.Add(subtype);
#if DEBUG
							PUtil.LogDebug("Will patch type: " + subtype.FullName);
#endif
						}
					}
				}
		}

		/// <summary>
		/// Shares common code between the Add and Remove transpilers.
		/// </summary>
		/// <param name="instructions">The original method to transpile.</param>
		/// <param name="originalMethod">The Cmps method being transpiled.</param>
		/// <param name="generator">The current IL generator.</param>
		/// <param name="evtName">The event field name to invoke on success.</param>
		/// <param name="methodBody">A delegate which generates the meat of the method.</param>
		/// <returns>The replacement method.</returns>
		private static TranspiledMethod TranspilerBase(TranspiledMethod instructions,
				MethodBase originalMethod, ILGenerator generator, string evtName,
				GenerateBody methodBody) {
			var cmpType = originalMethod.DeclaringType;
			bool patched = false;
			TranspiledMethod result = instructions;
			if (cmpType.IsConstructedGenericType && cmpType.GetGenericTypeDefinition() ==
					typeof(Components.Cmps<>)) {
				// Components.Cmps<argument>
				var t = cmpType.GenericTypeArguments[0];
				var itemsField = cmpType.GetFieldSafe("items", false);
				var eventField = cmpType.GetFieldSafe(evtName, false);
				var kcv = typeof(KCompactedVector<>).MakeGenericType(t);
				// Need concrete versions of the methods in that class
				var getDataList = kcv.GetMethodSafe(nameof(KCompactedVector<Brain>.
					GetDataList), false);
				var invokeAction = eventField?.FieldType?.GetMethodSafe(nameof(Action<Brain>.
					Invoke), false, t);
				var newMethod = new List<CodeInstruction>(32);
				if (itemsField != null && eventField != null && getDataList != null &&
						invokeAction != null) {
					var evt = generator.DeclareLocal(eventField.FieldType);
					var end = generator.DefineLabel();
					// Load items field
					newMethod.Add(new CodeInstruction(OpCodes.Ldarg_0));
					newMethod.Add(new CodeInstruction(OpCodes.Ldfld, itemsField));
					// Call GetDataList()
					newMethod.Add(new CodeInstruction(OpCodes.Callvirt, getDataList));
					newMethod.Add(new CodeInstruction(OpCodes.Ldarg_1));
					newMethod.AddRange(methodBody.Invoke(t, itemsField, end));
					// Load event field
					newMethod.Add(new CodeInstruction(OpCodes.Ldarg_0));
					newMethod.Add(new CodeInstruction(OpCodes.Ldfld, eventField));
					// Exit if null
					newMethod.Add(new CodeInstruction(OpCodes.Dup));
					newMethod.Add(new CodeInstruction(OpCodes.Stloc_S, (byte)evt.LocalIndex));
					newMethod.Add(new CodeInstruction(OpCodes.Brfalse_S, end));
					// Call Invoke
					newMethod.Add(new CodeInstruction(OpCodes.Ldloc_S, (byte)evt.LocalIndex));
					newMethod.Add(new CodeInstruction(OpCodes.Ldarg_1));
					newMethod.Add(new CodeInstruction(OpCodes.Callvirt, invokeAction));
					// Return
					newMethod.Add(new CodeInstruction(OpCodes.Ret).WithLabels(end));
#if DEBUG
					PUtil.LogDebug("Patched " + cmpType.FullName + "." + originalMethod.Name);
#endif
					patched = true;
				}
				result = newMethod;
			}
			if (!patched)
				PUtil.LogWarning("Unable to patch " + cmpType.FullName + "." + originalMethod.
					Name);
			return result;
		}

		/// <summary>
		/// Applied to Components.Cmps to speed up Add.
		/// </summary>
		[HarmonyPatch]
		internal static class Add_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.MinimalKCV;

			/// <summary>
			/// Target Add() on each required type.
			/// </summary>
			internal static IEnumerable<MethodBase> TargetMethods() {
				ComputeTargetTypes();
				foreach (var type in TYPES_TO_PATCH)
					yield return typeof(Components.Cmps<>).MakeGenericType(type).
						GetMethodSafe(nameof(Components.Cmps<Brain>.Add), false, type);
			}

			/// <summary>
			/// Generates the Cmps.Add body.
			/// </summary>
			private static TranspiledMethod GenerateBody(Type t, FieldInfo itemsField,
					Label _) {
				var addToList = typeof(List<>).MakeGenericType(t).GetMethodSafe(nameof(
					List<Brain>.Add), false, t);
				if (addToList == null)
					throw new ArgumentException("Unable to find List.Add");
				// Add to the list
				yield return new CodeInstruction(OpCodes.Callvirt, addToList);
			}

			/// <summary>
			/// Replace the method body entirely with a new one, which unfortunately needs to
			/// be done here to handle the generic types.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase originalMethod, ILGenerator generator) {
				return TranspilerBase(instructions, originalMethod, generator, "OnAdd",
					GenerateBody);
			}
		}

		/// <summary>
		/// Applied to Components.Cmps to speed up Remove.
		/// </summary>
		[HarmonyPatch]
		internal static class Remove_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.MinimalKCV;

			/// <summary>
			/// Target Add() on each required type.
			/// </summary>
			internal static IEnumerable<MethodBase> TargetMethods() {
				ComputeTargetTypes();
				foreach (var type in TYPES_TO_PATCH)
					yield return typeof(Components.Cmps<>).MakeGenericType(type).
						GetMethodSafe(nameof(Components.Cmps<Brain>.Remove), false, type);
			}

			/// <summary>
			/// Generates the Cmps.Remove body.
			/// </summary>
			private static TranspiledMethod GenerateBody(Type t, FieldInfo itemsField,
					Label end) {
				var removeFromList = typeof(List<>).MakeGenericType(t).GetMethodSafe(nameof(
					List<Brain>.Remove), false, t);
				if (removeFromList == null)
					throw new ArgumentException("Unable to find List.Remove");
				// Try removing from the list
				yield return new CodeInstruction(OpCodes.Callvirt, removeFromList);
				// If failed, skip
				yield return new CodeInstruction(OpCodes.Brfalse_S, end);
			}

			/// <summary>
			/// Replace the method body entirely with a new one, which unfortunately needs to
			/// be done here to handle the generic types.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase originalMethod, ILGenerator generator) {
				return TranspilerBase(instructions, originalMethod, generator, "OnRemove",
					GenerateBody);
			}
		}
	}
}
