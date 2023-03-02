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
using Klei.AI;
using System;
using System.Collections.Generic;

namespace PeterHan.FastTrack.CritterPatches {
	/// <summary>
	/// Groups patches used to optimize plant fertilization.
	/// </summary>
	public static class FertilizerMonitorPatches {
		/// <summary>
		/// Gets the available fertilizers or irrigants.
		/// </summary>
		/// <param name="source">The source location for the materials.</param>
		/// <param name="fertilizer">The location where the fertilizers will be placed.</param>
		internal static void GetFertilizers(Storage source, IList<KPrefabID> fertilizer) {
			var items = source.items;
			int n = items.Count;
			for (int i = 0; i < n; i++)
				// No guarantee sadly that the Element of PrimaryElement has the tag for
				// which FertilizationMonitor is looking
				if (items[i].TryGetComponent(out KPrefabID kpid))
					fertilizer.Add(kpid);
		}

		/// <summary>
		/// Gets the current fertilizer usage modifier.
		/// </summary>
		/// <param name="plant">The target plant.</param>
		/// <returns>The current fertilizer usage multiplier (mutations etc).</returns>
		internal static float GetFertilizerUsage(UnityEngine.GameObject plant) {
			return plant.GetAttributes().Get(Db.Get().PlantAttributes.FertilizerUsageMod).
				GetTotalValue();
		}

		/// <summary>
		/// Gets the available mass for a given fertilizer requirement.
		/// </summary>
		/// <param name="fertilizer">The available fertilizers.</param>
		/// <param name="targetTag">The type of fertilizer desired.</param>
		/// <param name="wrongTag">The type of item that is considered the wrong fertilizer.</param>
		/// <param name="wrong">Whether the wrong type of fertilizer has been found.</param>
		/// <returns>The mass available of this fertilizer type, or 0.0f if no match is found.</returns>
		internal static float GetMass(IList<KPrefabID> fertilizer, Tag targetTag, Tag wrongTag,
				ref bool wrong) {
			int n = fertilizer.Count;
			float mass = 0.0f;
			bool hasInvalid = wrong;
			for (int i = 0; i < n; i++) {
				var item = fertilizer[i];
				if (item.HasTag(targetTag)) {
					// Can theoretically double-count but this occurs in base game too
					if (item.TryGetComponent(out PrimaryElement pe))
						mass += pe.Mass;
				} else if (!hasInvalid && item.HasTag(wrongTag))
					wrong = hasInvalid = true;
			}
			return mass;
		}

		/// <summary>
		/// Applied to FertilizationMonitor.Instance to reduce the number of GetComponent calls
		/// every frame.
		/// </summary>
		[HarmonyPatch(typeof(FertilizationMonitor.Instance), nameof(FertilizationMonitor.
			Instance.UpdateFertilization))]
		internal static class UpdateFertilization_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

			/// <summary>
			/// Applied before UpdateFertilization runs.
			/// </summary>
			internal static bool Prefix(FertilizationMonitor.Instance __instance, float dt) {
				var consumed = __instance.def.consumedElements;
				var storage = __instance.storage;
				var wrongTag = __instance.def.wrongFertilizerTestTag;
				if (consumed != null && storage != null) {
					bool correct = true, wrong = false;
					var sm = __instance.sm;
					float modifier = GetFertilizerUsage(__instance.gameObject) * dt;
					int n = consumed.Length;
					var fertilizer = ListPool<KPrefabID, FertilizationMonitor>.Allocate();
					GetFertilizers(storage, fertilizer);
					for (int i = 0; i < n && correct; i++) {
						ref var consumeInfo = ref consumed[i];
						float mass = GetMass(fertilizer, consumeInfo.tag, wrongTag, ref wrong);
						__instance.total_available_mass = mass;
						if (mass < consumeInfo.massConsumptionRate * modifier)
							correct = false;
					}
					fertilizer.Recycle();
					sm.hasCorrectFertilizer.Set(correct, __instance);
					sm.hasIncorrectFertilizer.Set(wrong, __instance);
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to IrrigationMonitor.Instance to reduce the number of GetComponent calls
		/// every frame.
		/// </summary>
		[HarmonyPatch(typeof(IrrigationMonitor.Instance), nameof(IrrigationMonitor.Instance.
			UpdateIrrigation))]
		internal static class UpdateIrrigation_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

			/// <summary>
			/// Applied before UpdateIrrigation runs.
			/// </summary>
			internal static bool Prefix(IrrigationMonitor.Instance __instance, float dt) {
				var consumed = __instance.def.consumedElements;
				var storage = __instance.storage;
				var wrongTag = __instance.def.wrongIrrigationTestTag;
				var sm = __instance.sm;
				bool correct = true;
				bool wrong = false;
				bool canRecover = true;
				if (consumed != null && storage != null) {
					float modifier = GetFertilizerUsage(__instance.gameObject) * dt;
					int n = consumed.Length;
					var irrigant = ListPool<KPrefabID, IrrigationMonitor>.Allocate();
					GetFertilizers(storage, irrigant);
					for (int i = 0; i < n; i++) {
						ref var consumeInfo = ref consumed[i];
						float mass = GetMass(irrigant, consumeInfo.tag, wrongTag, ref wrong),
							target = consumeInfo.massConsumptionRate * modifier;
						__instance.total_available_mass = mass;
						if (mass < target) {
							correct = false;
							break;
						}
						// Could not find this constant in the game code
						if (mass < target * 30.0f) {
							canRecover = false;
							break;
						}
					}
					irrigant.Recycle();
				} else {
					correct = false;
					canRecover = false;
				}
				sm.hasCorrectLiquid.Set(correct, __instance);
				sm.hasIncorrectLiquid.Set(wrong, __instance);
				sm.enoughCorrectLiquidToRecover.Set(canRecover && correct, __instance);
				return false;
			}
		}
	}

	/// <summary>
	/// Applied to PlantElementAbsorbers to stop allocating (and leaking) accumulators that
	/// are never read.
	/// </summary>
	[HarmonyPatch(typeof(PlantElementAbsorbers), nameof(PlantElementAbsorbers.Add))]
	public static class PlantElementAbsorbers_Add_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before Add runs.
		/// </summary>
		internal static bool Prefix(Storage storage, PlantElementAbsorbers __instance,
				PlantElementAbsorber.ConsumeInfo[] consumed_elements,
				ref HandleVector<int>.Handle __result) {
			var absorber = HandleVector<int>.InvalidHandle;
			int n;
			if (consumed_elements != null && (n = consumed_elements.Length) > 0) {
				if (n == 1)
					// Optimized path that allocates less for just one element consumed
					absorber = __instance.Allocate(new PlantElementAbsorber {
						storage = storage,
						consumedElements = null,
						accumulators = null,
						localInfo = new PlantElementAbsorber.LocalInfo {
							tag = consumed_elements[0].tag,
							massConsumptionRate = consumed_elements[0].massConsumptionRate
						}
					});
				else
					absorber = __instance.Allocate(new PlantElementAbsorber {
						storage = storage,
						consumedElements = consumed_elements,
						accumulators = null,
						localInfo = new PlantElementAbsorber.LocalInfo {
							tag = Tag.Invalid,
							massConsumptionRate = 0f
						}
					});
			}
			__result = absorber;
			return false;
		}
	}

	/// <summary>
	/// Applied to PlantElementAbsorbers to not even bother updating the accumulators that
	/// are never read.
	/// </summary>
	[HarmonyPatch(typeof(PlantElementAbsorbers), nameof(PlantElementAbsorbers.Sim200ms))]
	public static class PlantElementAbsorbers_Sim200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Consumes mass for a plant from storage.
		/// </summary>
		/// <param name="from">The storage to search.</param>
		/// <param name="mass">The amount required.</param>
		/// <param name="targetTag">The target element tag to consume.</param>
		private static void ConsumeMass(Storage from, float mass, Tag targetTag) {
			PrimaryElement pe;
			while (mass > 0.0f && (pe = from.FindFirstWithMass(targetTag)) != null) {
				float actualMass = Math.Min(mass, pe.Mass);
				pe.Mass -= actualMass;
				mass -= actualMass;
				if (actualMass > 0.0f)
					// If something was successfully taken
					from.Trigger((int)GameHashes.OnStorageChange, pe.gameObject);
			}
		}

		/// <summary>
		/// Applied before Sim200ms runs.
		/// </summary>
		internal static bool Prefix(PlantElementAbsorbers __instance, float dt) {
			var data = __instance.data;
			var queue = __instance.queuedRemoves;
			int n = data.Count;
			__instance.updating = true;
			for (int i = 0; i < n; i++) {
				var absorber = data[i];
				if (absorber.storage != null) {
					var elements = absorber.consumedElements;
					var storage = absorber.storage;
					if (elements == null) {
						ref var targetElement = ref absorber.localInfo;
						ConsumeMass(storage, targetElement.massConsumptionRate * dt,
							targetElement.tag);
					} else {
						int ne = elements.Length;
						for (int j = 0; j < ne; j++) {
							ref var targetElement = ref elements[j];
							ConsumeMass(storage, targetElement.massConsumptionRate * dt,
								targetElement.tag);
						}
					}
				}
			}
			__instance.updating = false;
			// Destroy anything that was queued by attempts to destroy while updating
			n = queue.Count;
			for (int i = 0; i < n; i++)
				// Accumulators are always null, so Remove will leak nothing
				__instance.Free(queue[i]);
			queue.Clear();
			return false;
		}
	}
}
