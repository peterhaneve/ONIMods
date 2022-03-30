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
using System.Runtime.CompilerServices;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to ArtifactModule to only update the artifact's position (and thus lag) when
	/// the module is being launched or landed.
	/// </summary>
	[HarmonyPatch(typeof(ArtifactModule), nameof(ArtifactModule.RenderEveryTick))]
	public static class ArtifactModuleRenderer {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix(Clustercraft ___craft) {
			bool run = true;
			if (___craft != null) {
				var status = ___craft.Status;
				run = status == Clustercraft.CraftStatus.Landing || status == Clustercraft.
					CraftStatus.Launching;
			}
			return run;
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(nameof(ArtifactModule.RenderEveryTick))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void RenderEveryTick(ArtifactModule instance, float dt) {
			_ = instance;
			_ = dt;
			// Dummy code to ensure no inlining
			while (System.DateTime.Now.Ticks > 0L)
				throw new NotImplementedException("Reverse patch stub");
		}
	}

	/// <summary>
	/// Applied to Assets to disable ALL tile decorations.
	/// </summary>
	[HarmonyPatch(typeof(Assets), nameof(Assets.GetBlockTileDecorInfo))]
	public static class Assets_GetBlockTileDecorInfo_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoTileDecor;

		/// <summary>
		/// Applied after GetBlockTileDecorInfo runs.
		/// </summary>
		internal static bool Prefix(ref BlockTileDecorInfo __result) {
			__result = null;
			return false;
		}
	}

	/// <summary>
	/// Applied to PopFXManager to hush the torrent of popups at the start of the game.
	/// </summary>
	[HarmonyPatch]
	public static class PopFXManager_SpawnFX_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Patch all of the SpawnFX overloads.
		/// </summary>
		internal static IEnumerable<MethodBase> TargetMethods() {
			foreach (var method in typeof(PopFXManager).GetMethods(BindingFlags.Instance |
					PPatchTools.BASE_FLAGS))
				if (method.Name == nameof(PopFXManager.SpawnFX))
					yield return method;
			yield break;
		}

		/// <summary>
		/// Applied before SpawnFX runs.
		/// </summary>
		internal static bool Prefix() {
			return FastTrackMod.GameRunning;
		}
	}

	/// <summary>
	/// Applied to PumpingStationGuide to add an auxiliary component which renders for it
	/// at a slower rate.
	/// </summary>
	[HarmonyPatch(typeof(PumpingStationGuide), "OnSpawn")]
	public static class PumpingStationGuide_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(PumpingStationGuide __instance) {
			if (__instance != null)
				__instance.gameObject.AddOrGet<PumpingStationUpdater>();
		}
	}

	/// <summary>
	/// Applied to PumpingStationGuide to cut down the visual depth adjustment to every
	/// 200ms.
	/// </summary>
	[HarmonyPatch(typeof(PumpingStationGuide), nameof(PumpingStationGuide.RenderEveryTick))]
	public static class PumpingStationGuideRenderer {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(nameof(PumpingStationGuide.RenderEveryTick))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void RenderEveryTick(PumpingStationGuide instance, float dt) {
			_ = instance;
			_ = dt;
			// Dummy code to ensure no inlining
			while (System.DateTime.Now.Ticks > 0L)
				throw new NotImplementedException("Reverse patch stub");
		}
	}

	/// <summary>
	/// Updates the Pitcher Pump visuals every 200ms instead of every frame.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class PumpingStationUpdater : KMonoBehaviour, IRender200ms,
			IRenderEveryTick {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpReq]
		private PumpingStationGuide guide;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void Render200ms(float dt) {
			if (guide != null && guide.occupyTiles)
				PumpingStationGuideRenderer.RenderEveryTick(guide, dt);
		}

		public void RenderEveryTick(float dt) {
			if (guide != null && !guide.occupyTiles)
				PumpingStationGuideRenderer.RenderEveryTick(guide, dt);
		}
	}

	/// <summary>
	/// Applied to SingleEntityReceptacle to draw artifact modules every 1000ms even if not
	/// launching.
	/// </summary>
	[HarmonyPatch(typeof(SingleEntityReceptacle), nameof(SingleEntityReceptacle.Render1000ms))]
	public static class SingleEntityReceptacle_Render1000ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied after Render1000ms runs.
		/// </summary>
		internal static void Postfix(SingleEntityReceptacle __instance, float dt) {
			if (__instance is ArtifactModule am) // and is thus not null too!
				ArtifactModuleRenderer.RenderEveryTick(am, dt);
		}
	}

	/// <summary>
	/// Applied to Sublimates to stop spawning the bubble effect if the sublimated item is
	/// in a different world or not on screen.
	/// </summary>
	[HarmonyPatch(typeof(Sublimates), "Emit")]
	public static class Sublimates_Emit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Spawns an FX object only if it is on screen.
		/// </summary>
		private static void SpawnFXIf(Game instance, SpawnFXHashes fx, Vector3 position,
				float rotation, Sublimates caller) {
			if (caller != null && instance != null) {
				int cell = Grid.PosToCell(caller);
				// GetMyWorldId but with precomputed cell
				if (Grid.IsValidCell(cell)) {
					byte id = Grid.WorldIdx[cell];
					Grid.GetVisibleExtents(out int minX, out int minY, out int maxX,
						out int maxY);
					Grid.CellToXY(cell, out int x, out int y);
					if (id != ClusterManager.INVALID_WORLD_IDX && ClusterManager.Instance.
							activeWorldId == id && Grid.IsVisible(cell) && x >= minX &&
							x <= maxX && y >= minY && y <= maxY) {
						// Properly do the Z layer setting
						position.z = Grid.GetLayerZ(Grid.SceneLayer.Front);
						instance.SpawnFX(fx, position, rotation);
					}
				}
			}
		}

		/// <summary>
		/// Transpiles Emit to use our SpawnFX function instead which delegates to Game.SpawnFX
		/// only if the item is on screen.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = typeof(Game).GetMethodSafe(nameof(Game.SpawnFX), false,
				typeof(SpawnFXHashes), typeof(Vector3), typeof(float));
			if (target == null)
				PUtil.LogWarning("Unable to find SpawnFX for Sublimates patch");
			foreach (var instr in instructions) {
				if (target != null && instr.Is(OpCodes.Callvirt, target)) {
					// Push the Sublimates onto the stack
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					instr.operand = typeof(Sublimates_Emit_Patch).GetMethodSafe(nameof(
						SpawnFXIf), true, PPatchTools.AnyArguments);
#if DEBUG
					PUtil.LogDebug("Patched Sublimates.SpawnFX");
#endif
				}
				yield return instr;
			}
		}
	}
}
