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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using PeterHan.PLib.Detours;
using UnityEngine;

using RotCallback = StateMachine<Rottable, Rottable.Instance, IStateMachineTarget,
	Rottable.Def>.State.Callback;
using SapTreeStates = SapTree.StatesInstance;

namespace PeterHan.NoWasteWant {
	/// <summary>
	/// Patches which will be applied via annotations for Waste Not, Want Not.
	/// </summary>
	public sealed class NoWasteWantPatches : KMod.UserMod2 {
		private static readonly Tag[] EDIBLE_TAGS = {
			GameTags.CookingIngredient, GameTags.Edible
		};

		/// <summary>
		/// The maximum mass of food in kilograms that will rot.
		/// </summary>
		private const float MASS_TO_ROT = 0.01f;

		// Fix Efficient Supply to be compatible with this mod.
		[PLibPatch(RunAt.AfterModsLoad, "Compare", PatchType = HarmonyPatchType.Transpiler,
			RequireType = "PeterHan.EfficientFetch.EfficientFetchManager+FetchData",
			RequireAssembly = "EfficientFetch")]
		internal static IEnumerable<CodeInstruction> FixEfficientSupply(
				IEnumerable<CodeInstruction> method) {
			PUtil.LogDebug("Applying patch for Efficient Supply");
			return TranspileNegateLast(method);
		}

		/// <summary>
		/// For mods which want to add freshness limits to their custom food storage, call
		/// this method to add the slider to their Storage. It must be called in
		/// IBuildingConfig.DoPostConfigureComplete.
		/// 
		/// PPatchTools.GetTypeSafe("PeterHan.NoWasteWant.NoWasteWantPatches")?.GetMethodSafe(
		/// "AddFreshnessControl", true, typeof(GameObject))?.Invoke(null, new object[] { go });
		/// </summary>
		/// <param name="go">The prefab template to modify.</param>
		public static void AddFreshnessControl(GameObject go) {
			go.AddOrGet<FreshnessControl>();
		}
		
		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(NoWasteWantPatches));
			LocString.CreateLocStringKeys(typeof(NoWasteWantStrings.UI));
			new PLocalization().Register();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Only runs the rot handlers on food if it has a minimum threshold mass to rot.
		/// Prevents tiny microgram chunks caused by rounding errors from generating unwanted
		/// polluted dirt.
		/// </summary>
		private static void ReplaceRotHandler(Rottable sm) {
			var spoiledActions = sm.Spoiled.enterActions;
			if (spoiledActions != null) {
				var targets = new List<RotCallback>(spoiledActions.Count);
				foreach (var action in spoiledActions)
					if (action.callback is RotCallback originalCode)
						targets.Add(originalCode);
				spoiledActions.Clear();
				sm.Spoiled.Enter((smi) => {
					var go = smi.master.gameObject;
					var rotted = go.GetComponentSafe<PrimaryElement>();
					if (rotted == null || rotted.Mass > MASS_TO_ROT)
						foreach (var action in targets)
							action.Invoke(smi);
					else if (go != null)
						Util.KDestroyGameObject(go);
				});
			}
		}

		/// <summary>
		/// Transpiles a method, negating the value from the last CompareTo.
		/// </summary>
		private static IEnumerable<CodeInstruction> TranspileNegateLast(
				IEnumerable<CodeInstruction> method) {
			// Difficult to stream as the last instruction needs to be modified
			var newMethod = new List<CodeInstruction>(method);
			int n = newMethod.Count;
			var target = typeof(int).GetMethodSafe(nameof(int.CompareTo), false, typeof(int));
			for (int i = n - 1; i > 0; i--) {
				var instr = newMethod[i];
				if (instr.opcode == OpCodes.Call && (instr.operand as MethodBase) == target) {
					// OK, this fails if CompareTo returns int.MinValue, but that does not
					// happen on the freshness ranges that will be encountered (0-100).
#if DEBUG
					PUtil.LogDebug("Patching food freshness at offset {0:D}".F(i));
#endif
					newMethod.Insert(i + 1, new CodeInstruction(OpCodes.Neg));
					break;
				}
			}
			return newMethod;
		}

		/// <summary>
		/// Applied to FetchManager to ban fetching stale items to refrigerators.
		/// </summary>
		[HarmonyPatch(typeof(FetchManager), nameof(FetchManager.IsFetchablePickup_Exclude),
			typeof(KPrefabID), typeof(Storage), typeof(float), typeof(HashSet<Tag>),
			typeof(Tag), typeof(Storage))]
		public static class FetchManager_IsFetchablePickupExclude_Patch {
			/// <summary>
			/// Applied after IsFetchablePickup runs.
			/// </summary>
			internal static void Postfix(KPrefabID pickup_id, Storage destination,
					ref bool __result) {
				if (__result && pickup_id != null && destination != null && pickup_id.
						HasAnyTags(EDIBLE_TAGS) && destination.TryGetComponent(
						out FreshnessControl freshness))
					__result = freshness.IsAcceptable(pickup_id.gameObject);
			}
		}

		/// <summary>
		/// Applied to FetchManager to ban fetching stale items to refrigerators.
		/// </summary>
		[HarmonyPatch]
		public static class FetchManager_IsFetchablePickup_Patch {
			/// <summary>
			/// The target method to patch.
			/// </summary>
			private static readonly MethodBase TARGET = typeof(FetchManager).GetMethodSafe(
				nameof(FetchManager.IsFetchablePickup), true, typeof(Pickupable),
				typeof(FetchChore), typeof(Storage));

			internal static bool Prepare() {
				return TARGET != null;
			}

			internal static MethodBase TargetMethod() {
				return TARGET;
			}

			/// <summary>
			/// Applied after IsFetchablePickup runs.
			/// </summary>
			internal static void Postfix(Pickupable pickup, Storage destination,
					ref bool __result) {
				if (__result && pickup != null && destination != null && pickup.
						KPrefabID.HasAnyTags(EDIBLE_TAGS) && destination.TryGetComponent(
						out FreshnessControl freshness))
					__result = freshness.IsAcceptable(pickup.gameObject);
			}
		}

		/// <summary>
		/// Applied to FetchManager.PickupComparerIncludingPriority to sort using a comparator
		/// that flips food freshness.
		/// </summary>
		[HarmonyPatch]
		public static class FetchManager_PickupComparerIncludingPriority_Patch {
			internal static MethodBase TargetMethod() {
				return typeof(FetchManager).GetNestedType("PickupComparerIncludingPriority",
					PPatchTools.BASE_FLAGS)?.GetMethodSafe(nameof(IComparer<int>.Compare),
					false, typeof(FetchManager.Pickup), typeof(FetchManager.Pickup));
			}

			/// <summary>
			/// Transpiles Compare to flip the food freshness comparison.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return TranspileNegateLast(method);
			}
		}

		/// <summary>
		/// Applied to FetchManager.PickupComparerNoPriority to sort using a comparator that
		/// flips food freshness.
		/// </summary>
		[HarmonyPatch]
		public static class FetchManager_PickupComparerNoPriority_Patch {
			internal static MethodBase TargetMethod() {
				return typeof(FetchManager).GetNestedType("PickupComparerNoPriority",
					PPatchTools.BASE_FLAGS)?.GetMethodSafe(nameof(IComparer<int>.Compare),
					false, typeof(FetchManager.Pickup), typeof(FetchManager.Pickup));
			}

			/// <summary>
			/// Transpiles Compare to flip the food freshness comparison.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return TranspileNegateLast(method);
			}
		}

		/// <summary>
		/// Applied to RefrigeratorConfig to allow freshness to be controlled on refrigerators.
		/// </summary>
		[HarmonyPatch(typeof(RefrigeratorConfig), nameof(IBuildingConfig.
			DoPostConfigureComplete))]
		public static class RefrigeratorConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				AddFreshnessControl(go);
			}
		}

		/// <summary>
		/// Applied to RationBoxConfig to allow freshness to be controlled on ration boxes.
		/// </summary>
		[HarmonyPatch(typeof(RationBoxConfig), nameof(IBuildingConfig.
			DoPostConfigureComplete))]
		public static class RationBoxConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				AddFreshnessControl(go);
			}
		}

		/// <summary>
		/// Applied to Rottable to instantly destroy small chunks of rotten food when they
		/// rot.
		/// </summary>
		[HarmonyPatch(typeof(Rottable), "InitializeStates")]
		public static class Rottable_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(Rottable __instance) {
				ReplaceRotHandler(__instance);
			}
		}

		/// <summary>
		/// Applied to SapTree.StatesInstance to make the tree also look for the most rotten
		/// food instead of the "first one it finds". As the previous method did not respect
		/// the food quality at all, neither will this implementation.
		/// </summary>
		[HarmonyPatch(typeof(SapTreeStates), nameof(SapTreeStates.CheckForFood))]
		public static class SapTree_StatesInstance_CheckForFood_Patch {
			// Private fields ahoy!
			private static readonly IDetouredField<SapTreeStates, Extents> FEED_EXTENTS =
				PDetours.DetourFieldLazy<SapTreeStates, Extents>("feedExtents");

			private static readonly IDetouredField<SapTree, StateMachine<SapTree,
				SapTreeStates, IStateMachineTarget, SapTree.Def>.TargetParameter> FOOD_ITEM =
				PDetours.DetourFieldLazy<SapTree, StateMachine<SapTree, SapTreeStates,
				IStateMachineTarget, SapTree.Def>.TargetParameter>("foodItem");

			/// <summary>
			/// Applied before CheckForFood runs.
			/// </summary>
			[HarmonyPriority(Priority.HigherThanNormal)]
			internal static bool Prefix(SapTreeStates __instance) {
				var items = ListPool<ScenePartitionerEntry, SapTree>.Allocate();
				var gsp = GameScenePartitioner.Instance;
				GameObject foodItem = null;
				float rotten = float.MaxValue;
				gsp.GatherEntries(FEED_EXTENTS.Get(__instance), gsp.pickupablesLayer, items);
				int n = items.Count;
				for (int i = 0; i < n; i++)
					if (items[i].obj is Pickupable item && item.TryGetComponent(
							out Edible food)) {
						float rotValue = float.MaxValue;
						Rottable.Instance smi;
						// Only look for stale food on food that can rot in the first place
						if (food.FoodInfo.CanRot && (smi = food.GetSMI<Rottable.Instance>()) !=
								null)
							rotValue = smi.RotConstitutionPercentage;
						if (rotValue <= rotten) {
							foodItem = item.gameObject;
							rotten = rotValue;
						}
					}
				FOOD_ITEM.Get(__instance.sm).Set(foodItem, __instance);
				items.Recycle();
				return false;
			}
		}
	}
}
