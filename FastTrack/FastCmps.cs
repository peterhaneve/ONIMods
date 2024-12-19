/*
 * Copyright 2024 Peter Han
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

using OldList = System.Collections.IList;
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
			var result = instructions;
			if (cmpType != null && cmpType.IsConstructedGenericType && cmpType.
					GetGenericTypeDefinition() == typeof(Components.Cmps<>)) {
				// Components.Cmps<argument>
				var t = cmpType.GenericTypeArguments[0];
				var itemsField = cmpType.GetFieldSafe(nameof(Components.Cmps<Brain>.items),
					false);
				var eventField = cmpType.GetFieldSafe(evtName, false);
				var kcv = typeof(KCompactedVector<>).MakeGenericType(t);
				// Need concrete versions of the methods in that class
				var getDataList = kcv.GetMethodSafe(nameof(KCompactedVector<Brain>.
					GetDataList), false);
				var invokeAction = eventField?.FieldType.GetMethodSafe(nameof(Action<Brain>.
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
				PUtil.LogWarning("Unable to patch " + cmpType?.FullName + "." + originalMethod.
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
			private static TranspiledMethod GenerateMethodBody(Type t, FieldInfo itemsField,
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
					GenerateMethodBody);
			}
		}

		/// <summary>
		/// Applied to Components.Cmps to reduce allocations in GetWorldItems.
		/// </summary>
		[HarmonyPatch]
		internal static class GetWorldItems_Patch {
			/// <summary>
			/// The preallocate size to pass to the factory created Lists.
			/// </summary>
			private static readonly object[] CONSTRUCTOR_ARGS = { 32 };

			/// <summary>
			/// The signature of the constructor to call with preallocate size.
			/// </summary>
			private static readonly Type[] CONSTRUCTOR_SIG = { typeof(int) };

			/// <summary>
			/// Stores pooled lists to limit allocations.
			/// </summary>
			private static readonly IDictionary<Type, OldList> POOL = new Dictionary<Type,
				OldList>(64);

			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Gets a shared list instance that works with the specified container type.
			/// </summary>
			/// <param name="container">The container requesting a list.</param>
			/// <returns>A shared list compatible with that container type.</returns>
			private static OldList GetList(object container) {
				var type = container.GetType();
				var pool = POOL;
				if (!pool.TryGetValue(type, out var list)) {
					// Due to the Harmony bug, the type that actually is being used has to be
					// computed at runtime, as opposed to static pooled lists like ListPool
					var types = type.GenericTypeArguments;
					var elementType = typeof(KMonoBehaviour);
					if (types != null && types.Length > 0)
						elementType = types[0];
					// Only called once per type, not worth making a delegate
					var constructor = typeof(List<>).MakeGenericType(elementType).
						GetConstructor(PPatchTools.BASE_FLAGS | BindingFlags.Instance, null,
						CONSTRUCTOR_SIG, null);
					if (constructor != null && constructor.Invoke(CONSTRUCTOR_ARGS) is
							OldList newList)
						list = newList;
					if (list == null)
						list = new List<KMonoBehaviour>(32);
#if DEBUG
					PUtil.LogDebug("Created world items list for type " + type);
#endif
					pool.Add(type, list);
				}
				list.Clear();
				return list;
			}

			/// <summary>
			/// Target GetWorldItems() on each required type.
			/// </summary>
			internal static IEnumerable<MethodBase> TargetMethods() {
				ComputeTargetTypes();
				foreach (var type in TYPES_TO_PATCH)
					yield return typeof(Components.Cmps<>).MakeGenericType(type).GetMethodSafe(
						nameof(Components.Cmps<MinionIdentity>.GetWorldItems), false,
						typeof(int), typeof(ICollection<int>), typeof(Func<,>).
						MakeGenericType(type, typeof(bool)));
			}

			/// <summary>
			/// Replace the new list call to get a list from the pool instead. Due to the
			/// Harmony bug this transpiler has to pretty much always work.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase originalMethod) {
				var containerType = originalMethod.DeclaringType;
				var replacement = typeof(GetWorldItems_Patch).GetMethodSafe(nameof(
					GetList), true, typeof(object));
				ConstructorInfo targetConstructor = null;
				bool patched = false;
				// Find the target List<T> constructor to patch
				if (containerType != null && !containerType.ContainsGenericParameters) {
					var typeArgs = containerType.GenericTypeArguments;
					if (typeArgs != null && typeArgs.Length > 0) {
						var targetType = typeof(List<>).MakeGenericType(typeArgs[0]);
						targetConstructor = targetType.GetConstructor(PPatchTools.
							BASE_FLAGS | BindingFlags.Instance, null, Type.EmptyTypes, null);
					}
				}
				if (targetConstructor != null && replacement != null)
					foreach (var instr in instructions) {
						if (instr.Is(OpCodes.Newobj, targetConstructor)) {
							yield return new CodeInstruction(OpCodes.Ldarg_0);
							instr.opcode = OpCodes.Call;
							instr.operand = replacement;
#if DEBUG
							PUtil.LogDebug("Patched " + containerType + "." + originalMethod.
								Name);
#endif
							patched = true;
						}
						yield return instr;
					}
				else
					foreach (var instr in instructions)
						yield return instr;
				if (!patched)
					PUtil.LogWarning("Unable to patch " + containerType + "." +
						originalMethod.Name);
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
			private static TranspiledMethod GenerateMethodBody(Type t, FieldInfo itemsField,
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
					GenerateMethodBody);
			}
		}
	}

	/// <summary>
	/// Applied to Workable to prevent the use of GetWorldItems in the event handler, which
	/// could be called from a delegate in rocket landing that is really hard to patch.
	/// </summary>
	[HarmonyPatch(typeof(Workable), nameof(Workable.UpdateStatusItem))]
	public static class Workable_UpdateStatusItem_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before UpdateStatusItem runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Workable __instance) {
			if (__instance.TryGetComponent(out KSelectable selectable)) {
				var working = __instance.workingStatusItem;
				ref var statusHandle = ref __instance.workStatusItemHandle;
				selectable.RemoveStatusItem(statusHandle);
				if (__instance.worker == null)
					UpdateStatusItem(__instance, selectable, ref statusHandle);
				else if (working != null)
					statusHandle = selectable.AddStatusItem(working, __instance);
			}
			return false;
		}

		/// <summary>
		/// Updates the status item shown on each skill-required workable, when Duplicants
		/// transfer between colonies (or new skills granted / Duplicants printed).
		/// </summary>
		/// <param name="instance">The workable to update.</param>
		/// <param name="selectable">The location where the status item will be added.</param>
		/// <param name="statusHandle">Stores a reference to the status item so it can be
		/// destroyed later.</param>
		private static void UpdateStatusItem(Workable instance, KSelectable selectable,
				ref Guid statusHandle) {
			string perk = instance.requiredSkillPerk;
			int worldID = instance.GetMyWorldId();
			var duplicants = Components.LiveMinionIdentities.Items;
			var dbb = Db.Get().BuildingStatusItems;
			int n = duplicants.Count;
			// Manually filter to avoid GetWorldItems mutating the shared list again
			bool noMinions = instance.requireMinionToWork;
			for (int i = 0; i < n && noMinions; i++) {
				var duplicant = duplicants[i];
				noMinions = duplicant == null || duplicant.GetMyWorldId() != worldID;
			}
			if (noMinions)
				statusHandle = selectable.AddStatusItem(dbb.WorkRequiresMinion);
			else if (instance.shouldShowSkillPerkStatusItem && !string.IsNullOrEmpty(perk)) {
				if (MinionResume.AnyMinionHasPerk(perk, worldID))
					statusHandle = selectable.AddStatusItem(instance.
						readyForSkillWorkStatusItem, perk);
				else {
					var statusItem = DlcManager.FeatureClusterSpaceEnabled() ? dbb.
						ClusterColonyLacksRequiredSkillPerk : dbb.ColonyLacksRequiredSkillPerk;
					statusHandle = selectable.AddStatusItem(statusItem, perk);
				}
			}
		}
	}
}
