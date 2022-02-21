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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeterHan.FastTrack {
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
	/// Applied to BubbleManager to turn off its dead but possibly slow RenderEveryTick method.
	/// </summary>
	[HarmonyPatch(typeof(BubbleManager), nameof(BubbleManager.RenderEveryTick))]
	public static class BubbleManager_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix() => false;
	}

	/// <summary>
	/// Applied to Light2D to add SlowLightSymbolTracker when necessary.
	/// </summary>
	[HarmonyPatch(typeof(Light2D), "OnSpawn")]
	public static class Light2D_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(Light2D __instance) {
			var go = __instance.gameObject;
			if (go.GetComponentSafe<LightSymbolTracker>() != null)
				go.AddOrGet<SlowLightSymbolTracker>();
		}
	}

	/// <summary>
	/// Applied to LightSymbolTracker to reduce its update frequency to 200ms.
	/// </summary>
	[HarmonyPatch(typeof(LightSymbolTracker), nameof(LightSymbolTracker.RenderEveryTick))]
	public static class LightSymbolTrackerRenderer {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(nameof(LightSymbolTracker.RenderEveryTick))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void RenderEveryTick(LightSymbolTracker instance, float dt) {
			_ = instance;
			_ = dt;
			// Dummy code to ensure no inlining
			while (System.DateTime.Now.Ticks > 0L)
				throw new NotImplementedException("Reverse patch stub");
		}
	}

	/// <summary>
	/// Groups patches for the logic bit selector sidescreen.
	/// </summary>
	public static class LogicBitSelectorSideScreenPatches {
		// Side screen is a singleton so this is safe for now
		private static readonly IList<bool> lastValues = new List<bool>(4);

		/// <summary>
		/// A delegate to call the UpdateInputOutputDisplay method.
		/// </summary>
		private static readonly Action<LogicBitSelectorSideScreen> UPDATE_IO_DISPLAY =
			typeof(LogicBitSelectorSideScreen).Detour<Action<LogicBitSelectorSideScreen>>(
			"UpdateInputOutputDisplay");

		/// <summary>
		/// Updates all bits of the logic bit selector side screen.
		/// </summary>
		private static void ForceUpdate(LogicBitSelectorSideScreen instance,
				Color activeColor, Color inactiveColor, ILogicRibbonBitSelector target) {
			lastValues.Clear();
			foreach (var pair in instance.toggles_by_int) {
				int bit = pair.Key;
				bool active = target.IsBitActive(bit);
				while (lastValues.Count <= bit)
					lastValues.Add(false);
				lastValues[bit] = active;
				UpdateBit(pair.Value, active, activeColor, inactiveColor);
			}
		}

		/// <summary>
		/// Updates one bit of the logic bit selector side screen.
		/// </summary>
		private static void UpdateBit(MultiToggle multiToggle, bool active,
				Color activeColor, Color inactiveColor) {
			if (multiToggle != null) {
				var hr = multiToggle.gameObject.GetComponentSafe<HierarchyReferences>();
				hr.GetReference<KImage>("stateIcon").color = active ? activeColor :
					inactiveColor;
				hr.GetReference<LocText>("stateText").SetText(active ?
					STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_ACTIVE :
					STRINGS.UI.UISIDESCREENS.LOGICBITSELECTORSIDESCREEN.STATE_INACTIVE);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to update the visuals after it spawns
		/// (because side screens can have targets set for the first time before they are
		/// initialized).
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), "OnSpawn")]
		public static class OnSpawn_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				if (__instance != null)
					UPDATE_IO_DISPLAY.Invoke(__instance);
				ForceUpdate(__instance, ___activeColor, ___inactiveColor, ___target);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to set the initial states of LAST_VALUES
		/// for each bit.
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), "RefreshToggles")]
		public static class RefreshToggles_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied after RefreshToggles runs.
			/// </summary>
			internal static void Postfix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				ForceUpdate(__instance, ___activeColor, ___inactiveColor, ___target);
			}
		}

		/// <summary>
		/// Applied to LogicBitSelectorSideScreen to optimize down its RenderEveryTick method,
		/// limiting it only to when visible and to only what is necessary.
		/// </summary>
		[HarmonyPatch(typeof(LogicBitSelectorSideScreen), nameof(LogicBitSelectorSideScreen.
			RenderEveryTick))]
		public static class RenderEveryTick_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix(LogicBitSelectorSideScreen __instance,
					Color ___activeColor, Color ___inactiveColor,
					ILogicRibbonBitSelector ___target) {
				if (__instance != null && __instance.isActiveAndEnabled && ___target != null)
					foreach (var pair in __instance.toggles_by_int) {
						int bit = pair.Key;
						bool active = ___target.IsBitActive(bit), update = bit >= lastValues.
							Count;
						if (!update) {
							// If in range, see if bit changed
							update = active != lastValues[bit];
							if (update)
								lastValues[bit] = active;
						}
						if (update)
							UpdateBit(pair.Value, active, ___activeColor, ___inactiveColor);
					}
				return false;
			}
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
	/// Only updates LightSymbolTracker every 200 ms realtime, not every frame.
	/// </summary>
	internal sealed class SlowLightSymbolTracker : KMonoBehaviour, IRender200ms {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpReq]
		private LightSymbolTracker tracker;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void Render200ms(float dt) {
			if (tracker != null)
				LightSymbolTrackerRenderer.RenderEveryTick(tracker, dt);
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
		internal static IEnumerable<CodeInstruction> Transpiler(
				IEnumerable<CodeInstruction> instructions) {
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
				}
				yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to TimerSideScreen to only update the side screen if it is active.
	/// </summary>
	[HarmonyPatch(typeof(TimerSideScreen), nameof(TimerSideScreen.RenderEveryTick))]
	public static class TimerSideScreen_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static bool Prefix(TimerSideScreen __instance) {
			return __instance != null && __instance.isActiveAndEnabled;
		}
	}
}
