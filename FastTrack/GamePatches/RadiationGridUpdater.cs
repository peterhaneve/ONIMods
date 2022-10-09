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
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Updates the radiation grid in a more efficient way.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class RadiationGridUpdater : KMonoBehaviour, ISlicedSim1000ms {
#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private Radiator radiator;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void SlicedSim1000ms(float dt) {
			RadiationGridEmitter emitter;
			if (radiator != null && (emitter = radiator.emitter) != null) {
				emitter.originCell = Grid.PosToCell(transform.position);
				emitter.Emit();
			}
		}
	}

	/// <summary>
	/// A sliced updater for radiation emitters.
	/// </summary>
	internal sealed class SlicedRadiationGridUpdater :
		SlicedUpdaterSim1000ms<RadiationGridUpdater> { }

	/// <summary>
	/// Applied to Game to stop using the GameScheduler for radiation grid updates which it is
	/// not designed to do efficiently.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.RefreshRadiationLoop))]
	public static class Game_RefreshRadiationLoop_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied before RefreshRadiationLoop runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to RadiationGridEmitter to speed up the emission calculations slightly.
	/// </summary>
	[HarmonyPatch(typeof(RadiationGridEmitter), nameof(RadiationGridEmitter.Emit))]
	public static class RadiationGridEmitter_Emit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied before Emit runs.
		/// </summary>
		internal static bool Prefix(RadiationGridEmitter __instance) {
			int n = __instance.projectionCount;
			if (n > 0) {
				var startPos = (Vector2)Grid.CellToPosCCC(__instance.originCell, Grid.
					SceneLayer.Building);
				float angle = __instance.angle, direction = __instance.direction - 0.5f *
					angle, step = angle / n, intensity = __instance.intensity;
				var scanCells = __instance.scanCells;
				scanCells.Clear();
				for (int i = 0; i < n; i++) {
					float netAngle = Mathf.Deg2Rad * (direction + Random.Range(-step, step) *
						0.5f);
					var unitVector = new Vector2(Mathf.Cos(netAngle), Mathf.Sin(netAngle));
					float rads = intensity;
					float dist = 0f;
					while (rads > 0.01f && dist < RadiationGridEmitter.MAX_EMIT_DISTANCE) {
						int cell = Grid.PosToCell(startPos + unitVector * dist);
						// 1 / 3
						dist += 0.333333f;
						if (!Grid.IsValidCell(cell))
							break;
						if (!scanCells.Contains(cell)) {
							SimMessages.ModifyRadiationOnCell(cell, Mathf.RoundToInt(rads));
							scanCells.Add(cell);
						}
						float mass = Grid.Mass[cell];
						// Attenuate over distance, with a slight random factor
						rads *= (mass > 0.0f ? Mathf.Max(0f, 1f - Mathf.Pow(mass, 1.25f) *
							Grid.Element[cell].molarMass / 1000000.0f) : 1.0f) *
							Random.Range(0.96f, 0.98f);
					}
					direction += step;
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to Radiator to add a copy of our radiation emitter component.
	/// </summary>
	[HarmonyPatch(typeof(Radiator), nameof(Radiator.OnSpawn))]
	public static class Radiator_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(Radiator __instance) {
			if (__instance != null)
				__instance.gameObject.AddOrGet<RadiationGridUpdater>();
		}
	}

	/// <summary>
	/// Applied to Radiator to turn off its Update method.
	/// </summary>
	[HarmonyPatch(typeof(Radiator), nameof(Radiator.Update))]
	public static class Radiator_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
