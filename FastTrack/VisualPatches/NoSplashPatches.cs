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
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to BubbleManager to turn off its dead but possibly slow RenderEveryTick method.
	/// </summary>
	[HarmonyPatch(typeof(BubbleManager), nameof(BubbleManager.RenderEveryTick))]
	public static class BubbleManager_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() => false;
	}

	/// <summary>
	/// Applied to CO2Manager to turn off breath effects.
	/// </summary>
	[HarmonyPatch(typeof(CO2Manager), nameof(CO2Manager.SpawnBreath))]
	public static class CO2Manager_SpawnBreath_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Applied before SpawnBreath runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Vector3 position, float mass, float temperature) {
			var offsets = GasBreatherFromWorldProvider.DEFAULT_BREATHABLE_OFFSETS;
			int gameCell = Grid.PosToCell(position), count = offsets.Length, spawnCell = -1;
			for (int i = 0; i < count; i++) {
				int testCell = Grid.OffsetCell(gameCell, offsets[i]);
				if (Grid.IsValidCell(testCell)) {
					var element = Grid.Element[testCell];
					// Prioritize oxygen, polluted oxygen, salty oxygen (!), CO2
					if (element.id == SimHashes.CarbonDioxide || element.HasTag(GameTags.
							Breathable)) {
						spawnCell = testCell;
						break;
					}
					if (element.IsGas)
						// Only in gases, not in vacuums, liquids, or solids
						spawnCell = testCell;
				}
			}
			if (spawnCell >= 0)
				SimMessages.ModifyMass(spawnCell, mass, byte.MaxValue, 0, CellEventLogger.
					Instance.CO2ManagerFixedUpdate, temperature, SimHashes.CarbonDioxide);
			return false;
		}
	}

	/// <summary>
	/// Applied to FallingWater to turn off the splash sounds.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.AddToSim))]
	public static class FallingWater_AddToSim_Patch {
		/// <summary>
		/// The target method to replace for this patch.
		/// </summary>
		internal static MethodInfo MAYBE_AUDIBLE = typeof(CameraController).GetMethodSafe(
			nameof(CameraController.IsAudibleSound), false, typeof(Vector2));

		/// <summary>
		/// A replacement for CameraController.IsAudibleSound that always returns false.
		/// </summary>
		internal static MethodInfo NEVER_AUDIBLE = typeof(FallingWater_AddToSim_Patch).
			GetMethodSafe(nameof(NeverAudible), true, typeof(CameraController),
			typeof(Vector2));

		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Replaces CameraController.IsAudibleSound and always reports that it is inaudible.
		/// </summary>
		private static bool NeverAudible(CameraController controller, Vector2 vector) {
			_ = controller;
			_ = vector;
			return false;
		}

		/// <summary>
		/// Transpiles AddToSim to make it never spawn the sound.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, MAYBE_AUDIBLE,
				NEVER_AUDIBLE);
		}
	}

	/// <summary>
	/// Applied to FallingWater to turn off splashes.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.SpawnLiquidSplash))]
	public static class FallingWater_SpawnLiquidSplash_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Applied before SpawnLiquidSplash runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to FallingWater to turn off mist effects.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.SpawnLiquidTopDecor))]
	public static class FallingWater_SpawnLiquidTopDecor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Applied before SpawnLiquidTopDecor runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref bool __result) {
			__result = false;
			return false;
		}
	}

	/// <summary>
	/// Applied to GravityComponents to disable effects and sound.
	/// </summary>
	[HarmonyPatch(typeof(GravityComponents), nameof(GravityComponents.FixedUpdate))]
	public static class GravityComponents_FixedUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Reports that the item is never visibly in liquid, thus preventing the splash.
		/// </summary>
		private static bool NeverInLiquid(Vector2 _) => false;

		/// <summary>
		/// Transpiles FixedUpdate to turn off the effects and sound calls.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = FallingWater_AddToSim_Patch.MAYBE_AUDIBLE;
			var replacement = FallingWater_AddToSim_Patch.NEVER_AUDIBLE;
			var isLiquid = typeof(Grid).GetMethodSafe(nameof(Grid.IsVisiblyInLiquid), true,
				typeof(Vector2));
			var noAnim = typeof(GravityComponents_FixedUpdate_Patch).GetMethodSafe(nameof(
				NeverInLiquid), true, typeof(Vector2));
			if (target != null && replacement != null && isLiquid != null && noAnim != null) {
				int count = 0;
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Callvirt, target))
						instr.operand = replacement;
					else if (instr.Is(OpCodes.Callvirt, isLiquid) && ++count == 2) {
						// Second instance
						instr.operand = noAnim;
#if DEBUG
						PUtil.LogDebug("Patched GravityComponents.FixedUpdate");
#endif
					}
					yield return instr;
				}
			} else {
				PUtil.LogWarning("Unable to patch GravityComponents.FixedUpdate");
				foreach (var instr in instructions)
					yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to Pickupable to turn off the absorb animation.
	/// </summary>
	[HarmonyPatch(typeof(Pickupable), nameof(Pickupable.TryAbsorb))]
	public static class Pickupable_TryAbsorb_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoSplash;

		/// <summary>
		/// Applied before TryAbsorb runs.
		/// </summary>
		internal static void Prefix(ref bool hide_effects) {
			hide_effects = true;
		}
	}
}
