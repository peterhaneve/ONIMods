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
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

using EnergySource = StructureTemperaturePayload.EnergySource;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to StructureTemperatureComponents to speed up its "sim" method. This gets
	/// lumped into KComponentsInitializer in the metrics.
	/// </summary>
	[HarmonyPatch(typeof(StructureTemperatureComponents), nameof(
		StructureTemperatureComponents.Sim200ms))]
	public static class StructureTemperatureComponents_Sim200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastStructureTemperature;

		/// <summary>
		/// Adds up the total produced energy into the building's list of energy sources, or
		/// creates one if none is found.
		/// </summary>
		/// <param name="sources">The building's current energy sources.</param>
		/// <param name="kw">The energy emitted in kilowatts this frame.</param>
		/// <param name="targetSource">The source of the energy emission.</param>
		private static void AccumulateProducedEnergy(ref List<EnergySource> sources,
				float kw, string targetSource) {
			var src = sources;
			bool found = false;
			if (src == null)
				sources = src = new List<EnergySource>();
			int n = src.Count;
			for (int i = 0; i < n && !found; i++) {
				var source = sources[i];
				if (source.source == targetSource) {
					source.Accumulate(kw);
					found = true;
				}
			}
			if (!found)
				src.Add(new EnergySource(kw, targetSource));
		}

		/// <summary>
		/// Applied before Sim200ms runs.
		/// </summary>
		internal static bool Prefix(StructureTemperatureComponents __instance, float dt) {
			__instance.GetDataLists(out List<StructureTemperatureHeader> headers,
				out List<StructureTemperaturePayload> payloads);
			int n = headers.Count;
			var energyCategory = Db.Get().StatusItemCategories.OperatingEnergy;
			var energyStatus = __instance.operatingEnergyStatusItem;
			// No allocation required at all!
			for (int i = 0; i < n; i++) {
				var header = headers[i];
				if (Sim.IsValidHandle(header.simHandle)) {
					var payload = payloads[i];
					bool dirty = false;
					if (header.dirty) {
						header.dirty = false;
						UpdateSimState(ref payload);
						if (payload.pendingEnergyModifications != 0.0f) {
							SimMessages.ModifyBuildingEnergy(payload.simHandleCopy, payload.
								pendingEnergyModifications, Sim.MinTemperature, Sim.
								MaxTemperature);
							payload.pendingEnergyModifications = 0.0f;
							dirty = true;
						}
						headers[i] = header;
					}
					if (header.isActiveBuilding && !payload.bypass)
						dirty |= UpdateActive(ref payload, dt, energyCategory, energyStatus);
					if (dirty)
						payloads[i] = payload;
				}
			}
			return false;
		}

		/// <summary>
		/// Updates an active building's exhaust and self-heat.
		/// </summary>
		/// <param name="payload">The building being updated.</param>
		/// <param name="dt">The time since the last update in seconds.</param>
		/// <param name="energyCategory">The status item category for heat emission.</param>
		/// <param name="operatingEnergy">The status item to display when emitting heat.</param>
		/// <returns></returns>
		private static bool UpdateActive(ref StructureTemperaturePayload payload, float dt,
				StatusItemCategory energyCategory, StatusItem operatingEnergy) {
			bool dirty = false;
			const float MAX_PRESSURE = StructureTemperatureComponents.MAX_PRESSURE;
			var operational = payload.operational;
			if (operational == null || operational.IsActive) {
				float exhaust = payload.ExhaustKilowatts;
				if (!payload.isActiveStatusItemSet) {
					// Turn on the "active" status item
					if (payload.primaryElement.TryGetComponent(out KSelectable selectable))
						selectable.SetStatusItem(energyCategory, operatingEnergy,
							payload.simHandleCopy);
					payload.isActiveStatusItemSet = true;
				}
				AccumulateProducedEnergy(ref payload.energySourcesKW, payload.
					OperatingKilowatts, STRINGS.BUILDING.STATUSITEMS.OPERATINGENERGY.
					OPERATING);
				if (exhaust != 0.0f) {
					var extents = payload.GetExtents();
					int h = extents.height, w = extents.width;
					float kjPerM2 = exhaust * dt / (w * h);
					int gw = Grid.WidthInCells, cell = extents.y * gw + extents.x;
					// Going up one row is +grid width -building width from the last cell of
					// the previous row
					gw -= w;
					for (int y = h; y > 0; y--) {
						for (int x = w; x > 0; x--) {
							float mass = Grid.Mass[cell];
							// Avoid emitting into Vacuum
							if (mass > 0.0f)
								SimMessages.ModifyEnergy(cell, kjPerM2 * Mathf.Min(mass,
									MAX_PRESSURE) / MAX_PRESSURE, payload.maxTemperature,
									SimMessages.EnergySourceID.StructureTemperature);
							cell++;
						}
						cell += gw;
					}
					AccumulateProducedEnergy(ref payload.energySourcesKW, exhaust, STRINGS.
						BUILDING.STATUSITEMS.OPERATINGENERGY.EXHAUSTING);
				}
				dirty = true;
			} else if (payload.isActiveStatusItemSet) {
				// Turn off the "active" status item
				if (payload.primaryElement.TryGetComponent(out KSelectable selectable))
					selectable.SetStatusItem(energyCategory, null, null);
				payload.isActiveStatusItemSet = false;
				dirty = true;
			}
			return dirty;
		}

		/// <summary>
		/// Updates the sim with changes to a building's attributes.
		/// </summary>
		/// <param name="payload">The building to modify.</param>
		private static void UpdateSimState(ref StructureTemperaturePayload payload) {
			var def = payload.building.Def;
			float mass = def.MassForTemperatureModification;
			float overheatTemperature = (payload.overheatable != null) ? payload.overheatable.
				OverheatTemperature : Sim.MaxTemperature;
			if (!payload.enabled || payload.bypass)
				mass = 0.0f;
			SimMessages.ModifyBuildingHeatExchange(payload.simHandleCopy, payload.GetExtents(),
				mass, payload.primaryElement.InternalTemperature, def.ThermalConductivity,
				overheatTemperature, payload.OperatingKilowatts, payload.primaryElement.
				Element.idx);
		}
	}

	/// <summary>
	/// Applied to StructureTemperaturePayload.EnergySource to reduce a rather excessive
	/// memory allocation and computation of the average heat emitted.
	/// </summary>
	[HarmonyPatch(typeof(EnergySource), MethodType.Constructor, typeof(float),
		typeof(string))]
	public static class StructureTemperaturePayload_EnergySource_Constructor_Patch {
		/// <summary>
		/// The number of samples to keep instead.
		/// </summary>
		private const int SAMPLES = 15;

		internal static bool Prepare() => FastTrackOptions.Instance.FastStructureTemperature;

		/// <summary>
		/// Transpiles the constructor to reduce the memory usage by allocating just 3 seconds
		/// (15 samples if Sim200ms runs at speed) of heat emission rather than 186 samples.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			// Prefixing and skipping constructors is bad, do not attempt
			var targetMethod = typeof(Mathf).GetMethodSafe(nameof(Mathf.RoundToInt), true,
				typeof(float));
			foreach (var instr in instructions) {
				if (instr.Is(OpCodes.Call, targetMethod)) {
					// Pop the float argument
					yield return new CodeInstruction(OpCodes.Pop);
					// Push our constant instead
					instr.opcode = OpCodes.Ldc_I4;
					instr.operand = SAMPLES;
#if DEBUG
					PUtil.LogDebug("Patched EnergySource constructor");
#endif
				}
				yield return instr;
			}
		}
	}
}
