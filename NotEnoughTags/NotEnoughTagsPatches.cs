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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using KMod;

using Label = System.Reflection.Emit.Label;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.NotEnoughTags {
	/// <summary>
	/// Patches which will be applied via annotations for NotEnoughTags.
	/// </summary>
	public sealed class NotEnoughTagsPatches : KMod.UserMod2 {
		/// <summary>
		/// The tags which should always be in the lower bits for speed.
		/// </summary>
		private static readonly Tag[] FORCE_LOWER_BITS = {
			GameTags.Alloy, GameTags.Agriculture, GameTags.Breathable, GameTags.BuildableAny,
			GameTags.BuildableProcessed, GameTags.BuildableRaw, GameTags.Clothes,
			GameTags.Compostable, GameTags.ConsumableOre, GameTags.CookingIngredient,
			GameTags.Creature, GameTags.Creatures.Attack, GameTags.Creatures.Deliverable,
			GameTags.Creatures.Bagged, GameTags.Creatures.Burrowed,
			GameTags.Creatures.Confined, GameTags.Creatures.Defend, GameTags.Creatures.Die,
			GameTags.Creatures.Expecting, GameTags.Creatures.Falling, GameTags.Creatures.Flee,
			GameTags.Creatures.Flopping, GameTags.Creatures.Flyer, GameTags.Creatures.Hungry,
			GameTags.Creatures.Submerged, GameTags.Creatures.Overcrowded,
			GameTags.Creatures.ReservedByCreature, GameTags.Creatures.Swimmer,
			GameTags.Creatures.Walker, GameTags.Creatures.Wild,
			GameTags.CreatureBrain, GameTags.Dead, GameTags.DupeBrain,
			GameTags.Edible, GameTags.Egg, GameTags.Entombed, GameTags.Equipped,
			GameTags.Farmable, GameTags.Filter, GameTags.Garbage, GameTags.Gas,
			GameTags.GrowingPlant, GameTags.HasChores, GameTags.HoldingBreath, GameTags.Idle,
			GameTags.IncubatableEgg, GameTags.IndustrialIngredient, GameTags.IndustrialProduct,
			GameTags.Liquid, GameTags.Liquifiable, GameTags.ManufacturedMaterial,
			GameTags.MedicalSupplies, GameTags.Medicine, GameTags.Metal, GameTags.Minion,
			GameTags.Operational, GameTags.Ore, GameTags.Organics, GameTags.Other,
			GameTags.Overjoyed, GameTags.PedestalDisplayable, GameTags.PerformingWorkRequest,
			GameTags.Preserved, GameTags.Pickupable, GameTags.Reachable, GameTags.RefinedMetal,
			GameTags.RareMaterials, GameTags.Sealed, GameTags.Seed, GameTags.Solid,
			GameTags.Stored, GameTags.Trapped, GameTags.Unbreathable,
		};

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			base.OnAllModsLoaded(harmony, mods);
			FetchManager.disallowedTagMask = TagBitOps.Not(FetchManager.disallowedTagBits);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
#if DEBUG
			SpamObjectsHandler.PrepareSpamHandler(new PLib.PatchManager.PPatchManager(harmony));
#endif
			var inst = ExtendedTagBits.Instance;
			// Force these tags into the efficient lower bits
			foreach (var tag in FORCE_LOWER_BITS)
				inst.ManifestFlagIndex(tag);
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Replaces all instructions between the last AND and the RET immediately after
		/// it with the specified method call. It should return bool and has the bits5/7 of A
		/// and B on the stack (in the OR case it also has the values from the previous
		/// bit compares).
		/// </summary>
		/// <param name="method">The method to transpile.</param>
		/// <param name="replacement">The method to call instead.</param>
		/// <returns>true if the method was transpiled, or false if the references were not
		/// found.</returns>
		private static bool ReplaceAnd(List<CodeInstruction> method, MethodBase replacement) {
			int n = method.Count;
			bool transpiled = false;
			for (int i = n - 1; i > 0; i--) {
				var instr = method[i];
				// LAST and
				if (instr.opcode == OpCodes.And) {
					int and = ++i;
					// Find the next RET or branch after it
					for (; i < n && !transpiled; i++) {
						var ni = method[i];
						var opcode = ni.opcode;
						// Ignore branches that branch to the immediately following statement
						if (opcode == OpCodes.Ret || ((opcode == OpCodes.Br || opcode ==
								OpCodes.Br_S) && (!(ni.operand is Label lbl) || i >= n ||
								!method[i + 1].labels.Contains(lbl)))) {
							// Replace the AND with static tag compare
							instr.opcode = OpCodes.Call;
							instr.operand = replacement;
							// Remove everything in between
							if (i > and)
								method.RemoveRange(and, i - and);
							transpiled = true;
						}
					}
					break;
				}
			}
			if (!transpiled)
				PUtil.LogWarning("Unable to transpile method {0}".F(replacement.Name));
			return transpiled;
		}

		/// <summary>
		/// Replaces the last occurrence of the specified opcode with a method call. The method
		/// should accept the appropriate number of ulongs as parameters and return a ulong.
		/// </summary>
		/// <param name="method">The method to transpile.</param>
		/// <param name="operation">The bit operation to replace.</param>
		/// <param name="replacement">The method to call instead.</param>
		/// <returns>true if the method was transpiled, or false if the reference was not
		/// found.</returns>
		private static bool ReplaceLastBitOp(IList<CodeInstruction> method, OpCode operation,
				MethodBase replacement) {
			int n = method.Count;
			bool transpiled = false;
			for (int i = n - 1; i > 0 && !transpiled; i--) {
				var instr = method[i];
				if (instr.opcode == operation) {
					instr.opcode = OpCodes.Call;
					instr.operand = replacement;
					transpiled = true;
				}
			}
			if (!transpiled)
				PUtil.LogWarning("Unable to transpile method {0}".F(replacement.Name));
			return transpiled;
		}

		/// <summary>
		/// Applied to ComplexFabricator to properly OR the tag bits in the face of inlining
		/// of Or().
		/// </summary>
		[HarmonyPatch(typeof(ComplexFabricator), "DropExcessIngredients")]
		public static class ComplexFabricator_DropExcessIngredients_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase original) {
				var rtb = typeof(TagBits).MakeByRefType();
				return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(TagBits).
					GetMethodSafe(nameof(TagBits.Or), false, rtb), typeof(TagBitOps).
					GetMethodSafe(nameof(TagBitOps.Or), true, rtb, rtb));
			}
		}

		/// <summary>
		/// Applied to FetchAreaChore.StatesInstance to properly AND the tag bits in the face
		/// of inlining of And().
		/// </summary>
		[HarmonyPatch(typeof(FetchAreaChore.StatesInstance), nameof(FetchAreaChore.
			StatesInstance.SetupDelivery))]
		public static class FetchAreaChore_StatesInstance_SetupDelivery_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase original) {
				var rtb = typeof(TagBits).MakeByRefType();
				return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(TagBits).
					GetMethodSafe(nameof(TagBits.And), false, rtb), typeof(TagBitOps).
					GetMethodSafe(nameof(TagBitOps.And), true, rtb, rtb));
			}
		}

		/// <summary>
		/// Applied to GlobalChoreProvider to properly OR the tag bits in the face of inlining
		/// of Or().
		/// </summary>
		[HarmonyPatch(typeof(GlobalChoreProvider), "UpdateStorageFetchableBits")]
		public static class GlobalChoreProvider_UpdateStorageFetchableBits_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
					MethodBase original) {
				var rtb = typeof(TagBits).MakeByRefType();
				return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(TagBits).
					GetMethodSafe(nameof(TagBits.Or), false, rtb), typeof(TagBitOps).
					GetMethodSafe(nameof(TagBitOps.Or), true, rtb, rtb));
			}
		}

		/// <summary>
		/// Applied to KPrefabID to properly AND the tag bits in the face of inlining of And().
		/// </summary>
		[HarmonyPatch(typeof(KPrefabID), nameof(KPrefabID.AndTagBits))]
		public static class KPrefabID_AndTagBits_Patch {
			/// <summary>
			/// Applied before AndTagBits runs.
			/// </summary>
			internal static bool Prefix(KPrefabID __instance, ref TagBits ___tagBits,
					ref TagBits rhs) {
				__instance.UpdateTagBits();
				TagBitOps.And(ref rhs, ref ___tagBits);
				return false;
			}
		}

		/// <summary>
		/// Applied to TagBits to make And use the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.And))]
		public static class TagBits_And_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceLastBitOp(method, OpCodes.And, typeof(TagBitOps).GetMethodSafe(
					nameof(TagBitOps.TranspileAnd), true, typeof(ulong), typeof(ulong)));
				return method;
			}
		}

		/// <summary>
		/// Applied to TagBits to properly clear tags with extended indexes.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.Clear))]
		public static class TagBits_Clear_Patch {
			/// <summary>
			/// Applied before Clear runs.
			/// </summary>
			internal static bool Prefix(ref ulong ___bits4, Tag tag) {
				var inst = ExtendedTagBits.Instance;
				int index = inst.ManifestFlagIndex(tag) - ExtendedTagBits.VANILLA_LIMIT;
				bool vanilla = index <= 0;
				if (!vanilla && ___bits4 != 0UL) {
					int id = inst.GetIDWithTagClear(TagBitOps.GetUpperBits(___bits4), index);
					___bits4 = TagBitOps.GetLowerBits(___bits4) | ((ulong)id << 32);
				}
				return vanilla;
			}
		}

		/// <summary>
		/// Applied to TagBits to make Complement use the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.Complement))]
		public static class TagBits_Complement_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceLastBitOp(method, OpCodes.Not, typeof(TagBitOps).GetMethodSafe(
					nameof(TagBitOps.TranspileNot), true, typeof(ulong)));
				return method;
			}
		}

		/// <summary>
		/// Applied to TagBits to add extended tags to the GetTagsVerySlow method.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.GetTagsVerySlow), typeof(int),
			typeof(ulong), typeof(List<Tag>))]
		public static class TagBits_GetTagsVerySlow_Patch {
			/// <summary>
			/// Applied before GetTagsVerySlow runs.
			/// </summary>
			internal static bool Prefix(int bits_idx, ulong bits, List<Tag> tags) {
				bool extended = bits_idx >= 5;
				var inst = ExtendedTagBits.Instance;
				int ubits = TagBitOps.GetUpperBits(bits), nlower = extended ? 32 : 64,
					baseIdx = BitSet.ULONG_BITS * bits_idx;
				// Vanilla tags in lowest N bits
				for (int i = 0; i < nlower; i++) {
					if ((bits & 1UL) != 0UL) {
						var tag = inst.GetTagForIndex(baseIdx + i);
						if (tag != Tag.Invalid)
							tags.Add(tag);
					}
					bits >>= 1;
				}
				// Extended tags in highest bits
				if (extended && ubits != 0) {
					var bitSet = inst.GetTagBits(ubits);
					for (int i = 0; i < bitSet.Capacity; i++)
						if (bitSet.Get(i)) {
							var tag = inst.GetTagForIndex(ExtendedTagBits.VANILLA_LIMIT + i);
							if (tag != Tag.Invalid)
								tags.Add(tag);
						}
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to TagBits to make HasAll check the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.HasAll))]
		public static class TagBits_HasAll_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceAnd(method, typeof(TagBitOps).GetMethodSafe(nameof(
					TagBitOps.HasAll), true, typeof(ulong), typeof(ulong)));
				return method;
			}
		}

		/// <summary>
		/// Applied to TagBits to make HasAny check the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.HasAny))]
		public static class TagBits_HasAny_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceAnd(method, typeof(TagBitOps).GetMethodSafe(nameof(
					TagBitOps.HasAny), true, typeof(ulong), typeof(ulong),
					typeof(ulong)));
				return method;
			}
		}

		/// <summary>
		/// Applied to TagBits to handle tags and extended tags when manifesting flag indexes.
		/// 
		/// Skipping is required because the side effect of crashing when out of tags must not
		/// execute.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.ManifestFlagIndex))]
		public static class TagBits_ManifestFlagIndex_Patch {
			/// <summary>
			/// Applied before ManifestFlagIndex runs.
			/// </summary>
			internal static bool Prefix(Tag tag, ref int __result) {
				__result = ExtendedTagBits.Instance.ManifestFlagIndex(tag);
				return false;
			}
		}

		/// <summary>
		/// Applied to TagBits to make Or use the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.Or))]
		public static class TagBits_Or_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceLastBitOp(method, OpCodes.Or, typeof(TagBitOps).GetMethodSafe(
					nameof(TagBitOps.TranspileOr), true, typeof(ulong), typeof(ulong)));
				return method;
			}
		}

		/// <summary>
		/// Applied to TagBits to properly set tags with extended indexes.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.SetTag))]
		public static class TagBits_SetTag_Patch {
			/// <summary>
			/// Applied before SetTag runs.
			/// </summary>
			internal static bool Prefix(ref ulong ___bits4, Tag tag) {
				var inst = ExtendedTagBits.Instance;
				int index = inst.ManifestFlagIndex(tag) - ExtendedTagBits.VANILLA_LIMIT;
				bool vanilla = index <= 0;
				if (!vanilla) {
					int id = inst.GetIDWithTagSet(TagBitOps.GetUpperBits(___bits4), index);
					___bits4 = (___bits4 & 0xFFFFFFFFUL) | ((ulong)id << 32);
				}
				return vanilla;
			}
		}

		/// <summary>
		/// Applied to TagBits to make Xor use the correct high bits.
		/// </summary>
		[HarmonyPatch(typeof(TagBits), nameof(TagBits.Xor))]
		public static class TagBits_Xor_Patch {
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var method = new List<CodeInstruction>(instructions);
				ReplaceLastBitOp(method, OpCodes.Xor, typeof(TagBitOps).GetMethodSafe(
					nameof(TagBitOps.TranspileXor), true, typeof(ulong), typeof(ulong)));
				return method;
			}
		}
	}
}
