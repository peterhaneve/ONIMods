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
using Klei.AI;
using System.Collections.Generic;
using System.Reflection;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// Patches for MD-471618: Breath of Fresh Air.
	/// </summary>
	internal static class MD471618 {
		private const byte LI_GI = (byte)(Sim.Cell.Properties.GasImpermeable |
			Sim.Cell.Properties.LiquidImpermeable | Sim.Cell.Properties.SolidImpermeable |
			Sim.Cell.Properties.Opaque);

		/// <summary>
		/// Set one time to trigger element rediscovery on every load.
		/// </summary>
		internal static bool AllDiscovered;

		/// <summary>
		/// Runs after the Db is initialized.
		/// </summary>
		internal static void AfterDbInit() {
			if (TurnBackTheClockOptions.Instance.MD471618_Traits)
				TraitsFix();
			if (TurnBackTheClockOptions.Instance.MD471618_DiagonalAccess)
				DiagonalAccessFix();
		}

		/// <summary>
		/// Resets the offset tables to their values before MD-471618.
		/// </summary>
		private static void DiagonalAccessFix() {
			OffsetGroups.InvertedStandardTable = LegacyVanillaOffsetTables.
				InvertedStandardTable;
			OffsetGroups.InvertedStandardTableWithCorners = LegacyVanillaOffsetTables.
				InvertedStandardTableWithCorners;
		}

		/// <summary>
		/// Removes a trait from the available Duplicant starting traits.
		/// </summary>
		/// <param name="traits">The list of traits to modify.</param>
		/// <param name="id">The ID of the trait to remove.</param>
		internal static void RemoveTrait(IList<TUNING.DUPLICANTSTATS.TraitVal> traits, string id) {
			int n = traits.Count;
			for (int i = 0; i < n; i++) {
				var trait = traits[i];
				if (trait.id == id) {
					traits.RemoveAt(i);
					n--;
				}
			}
		}

		/// <summary>
		/// Alters the tech tree for MD-471618 changes.
		/// </summary>
		internal static void TechTreeFix() {
			var techs = Db.Get().Techs;
			Tech solidTransport = techs.TryGet("SolidTransport"),
				solidSpace = techs.TryGet("SolidSpace"),
				roboticTools = techs.TryGet("RoboticTools"),
				improvedGasPiping = techs.TryGet("ImprovedGasPiping"),
				portableGases = techs.TryGet("PortableGasses"),
				distillation = techs.TryGet("Distillation"),
				renaissanceArt = techs.TryGet("RenaissanceArt"),
				monuments = techs.TryGet("Monuments"),
				highTempForging = techs.TryGet("HighTempForging");
			solidTransport.AddUnlockedItemIDs(SolidConduitOutboxConfig.ID,
				SolidLogicValveConfig.ID, AutoMinerConfig.ID);
			solidSpace.RemoveUnlockedItemIDs(SolidLogicValveConfig.ID,
				SolidConduitOutboxConfig.ID);
			improvedGasPiping.AddUnlockedItemIDs(GasBottlerConfig.ID);
			roboticTools.RemoveUnlockedItemIDs(AutoMinerConfig.ID);
			portableGases.RemoveUnlockedItemIDs(GasBottlerConfig.ID);
			distillation.AddUnlockedItemIDs(BottleEmptierGasConfig.ID);
			portableGases.RemoveUnlockedItemIDs(BottleEmptierGasConfig.ID);
			renaissanceArt.AddUnlockedItemIDs(MonumentBottomConfig.ID, MonumentMiddleConfig.ID,
				MonumentTopConfig.ID);
			monuments.RemoveUnlockedItemIDs(MonumentBottomConfig.ID, MonumentMiddleConfig.ID,
				MonumentTopConfig.ID);
			highTempForging.RemoveUnlockedItemIDs(GantryConfig.ID);
		}

		/// <summary>
		/// Removes traits introduced in The Breath of Fresh Air update from Duplicant starting
		/// stats. The traits still exist on Duplicants that have them, but cannot be obtained.
		/// </summary>
		private static void TraitsFix() {
			var good = TUNING.DUPLICANTSTATS.GOODTRAITS;
			var bad = TUNING.DUPLICANTSTATS.BADTRAITS;
			RemoveTrait(bad, "ConstructionDown");
			RemoveTrait(bad, "RanchingDown");
			RemoveTrait(bad, "CaringDown");
			RemoveTrait(bad, "BotanistDown");
			RemoveTrait(bad, "ArtDown");
			RemoveTrait(bad, "CookingDown");
			RemoveTrait(bad, "MachineryDown");
			RemoveTrait(bad, "DiggingDown");
			RemoveTrait(bad, "DecorDown");
			RemoveTrait(bad, "NightLight");
			RemoveTrait(good, "DecorUp");
			RemoveTrait(good, "Thriver");
			RemoveTrait(good, "GreenThumb");
			RemoveTrait(good, "ConstructionUp");
			RemoveTrait(good, "RanchingUp");
			RemoveTrait(good, "GrantSkill_Mining1");
			RemoveTrait(good, "GrantSkill_Mining2");
			RemoveTrait(good, "GrantSkill_Mining3");
			RemoveTrait(good, "GrantSkill_Farming2");
			RemoveTrait(good, "GrantSkill_Ranching1");
			RemoveTrait(good, "GrantSkill_Cooking1");
			RemoveTrait(good, "GrantSkill_Arting1");
			RemoveTrait(good, "GrantSkill_Arting2");
			RemoveTrait(good, "GrantSkill_Arting3");
			RemoveTrait(good, "GrantSkill_Suits1");
			RemoveTrait(good, "GrantSkill_Technicals2");
			RemoveTrait(good, "GrantSkill_Engineering1");
			RemoveTrait(good, "GrantSkill_Basekeeping2");
			RemoveTrait(good, "GrantSkill_Medicine2");
		}

		/// <summary>
		/// Applied to AirFilterConfig to remove power and heat requirements from the Deodorizer.
		/// </summary>
		[HarmonyPatch(typeof(AirFilterConfig), nameof(IBuildingConfig.CreateBuildingDef))]
		public static class AirFilterConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DeodorizerPower;

			internal static void Postfix(BuildingDef __result) {
				__result.RequiresPowerInput = false;
				__result.EnergyConsumptionWhenActive = 0.0f;
				__result.ExhaustKilowattsWhenActive = 0.0f;
				__result.SelfHeatKilowattsWhenActive = 0.0f;
			}
		}

		/// <summary>
		/// Applied to BasePacuConfig to remove seeds from pacu diets.
		/// </summary>
		[HarmonyPatch(typeof(BasePacuConfig), nameof(BasePacuConfig.SeedDiet))]
		public static class BasePacuConfig_SeedDiet_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_PacuDiet;

			internal static void Postfix(List<Diet.Info> __result) {
				__result.Clear();
			}
		}

		/// <summary>
		/// Applied to CoughMonitor to prevent application of Yucky Lungs.
		/// </summary>
		[HarmonyPatch(typeof(CoughMonitor), "OnBreatheDirtyAir")]
		public static class CoughMonitor_OnBreatheDirtyAir_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_Debuffs;

			internal static bool Prefix(CoughMonitor __instance, CoughMonitor.Instance smi) {
				__instance.shouldCough.Set(false, smi);
				smi.lastConsumeTime = 0.0f;
				smi.amountConsumed = 0.0f;
				return false;
			}
		}

		/// <summary>
		/// Applied to CraftingTableConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(CraftingTableConfig), nameof(IBuildingConfig.CreateBuildingDef))]
		public static class CraftingTableConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to DesalinatorConfig to set a min 40 C output temperature on brine.
		/// </summary>
		[HarmonyPatch(typeof(DesalinatorConfig), nameof(IBuildingConfig.
			ConfigureBuildingTemplate))]
		public static class DesalinatorConfig_ConfigureBuildingTemplate_Patch {
			private static readonly Tag BRINE = new Tag(nameof(SimHashes.Brine));

			/// <summary>
			/// Clay please, why did you just have to rename the field!?
			///
			/// TODO
			/// </summary>
			private static readonly FieldInfo TAG_OLD = typeof(ElementConverter.
				ConsumedElement).GetFieldSafe("tag", false);

			private static readonly FieldInfo TAG_NEW = typeof(ElementConverter.
				ConsumedElement).GetFieldSafe(nameof(ElementConverter.ConsumedElement.
				Tag), false);

			private const float TEMPERATURE = Constants.CELSIUS2KELVIN + 40.0f;

			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DesalinatorTemperature;

			internal static void Postfix(GameObject go) {
				var ec = go.GetComponents<ElementConverter>();
				if (ec != null)
					foreach (var converter in ec) {
						var inputs = converter.consumedElements;
						// Brine recipe
						if (inputs.Length == 1) {
							var firstInput = inputs[0];
							var tag = Tag.Invalid;
							if (TAG_OLD != null && TAG_OLD.GetValue(firstInput) is Tag
									fieldTag)
								tag = fieldTag;
							else if (TAG_NEW != null && TAG_NEW.GetValue(
									firstInput) is Tag propertyTag)
								tag = propertyTag;
							if (tag == BRINE) {
								converter.outputElements[0].minOutputTemperature = TEMPERATURE;
								converter.outputElements[1].minOutputTemperature = TEMPERATURE;
							}
						}
					}
			}
		}

		/// <summary>
		/// Applied to DiscoveredResources to automatically rediscover all resources on the
		/// map on load.
		/// </summary>
		[HarmonyPatch(typeof(DiscoveredResources), nameof(DiscoveredResources.Sim4000ms))]
		public static class DiscoveredResources_Sim4000ms_Patch {
			private static void Discover(DiscoveredResources di, Pickupable pickupable) {
				int cell = Grid.PosToCell(pickupable);
				Tag tag;
				var prefabID = pickupable.GetComponent<KPrefabID>();
				if (prefabID != null && !di.IsDiscovered(tag = prefabID.PrefabTag) && Grid.
						IsValidCell(cell) && Grid.Revealed[cell])
					di.Discover(tag, DiscoveredResources.GetCategoryForEntity(prefabID));
			}

			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DiscoverAll;

			internal static void Postfix(DiscoveredResources __instance) {
				if (!AllDiscovered) {
					foreach (var item in Components.Pickupables)
						if (item is Pickupable pickupable && pickupable != null)
							Discover(__instance, pickupable);
					AllDiscovered = true;
				}
			}
		}

		/// <summary>
		/// Applied to GasLimitValveConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(GasLimitValveConfig), nameof(IBuildingConfig.CreateBuildingDef))]
		public static class GasLimitValveConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to GasLiquidExposureMonitor to prevent the application of any exposure
		/// effects for eye irritation.
		/// </summary>
		[HarmonyPatch(typeof(GasLiquidExposureMonitor), "ApplyEffects")]
		public static class GasLiquidExposureMonitor_ApplyEffects_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_Debuffs;

			internal static void Prefix(GasLiquidExposureMonitor __instance,
					GasLiquidExposureMonitor.Instance smi) {
				smi.exposure = 0.0f;
				__instance.isIrritated.Set(false, smi);
			}
		}

		/// <summary>
		/// Applied to LiquidLimitValveConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(LiquidLimitValveConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class LiquidLimitValveConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to MinionStartingStats to reset starting attribute points distributions
		/// to strictly 7/3/1.
		/// </summary>
		[HarmonyPatch(typeof(MinionStartingStats), "GenerateAttributes")]
		public static class MinionStartingStats_GenerateAttributes_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_Traits;

			internal static void Prefix(ref int pointsDelta) {
				pointsDelta = 0;
			}
		}

		/// <summary>
		/// Applied to OxygenMaskLockerConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(OxygenMaskLockerConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class OxygenMaskLockerConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to OxygenMaskMarkerConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(OxygenMaskMarkerConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class OxygenMaskMarkerConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to RefrigeratorConfig to disable Eco mode on refrigerators if legacy food
		/// storage is in effect (you gain some, you lose some!)
		/// </summary>
		[HarmonyPatch(typeof(RefrigeratorConfig), nameof(IBuildingConfig.
			DoPostConfigureComplete))]
		public static class RefrigeratorConfig_DoPostConfigureComplete_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_EzFoodStorage;

			internal static void Postfix(GameObject go) {
				go.AddOrGetDef<RefrigeratorController.Def>().powerSaverEnergyUsage = 120.0f;
			}
		}

		/// <summary>
		/// Applied to Rottable.Instance to change refrigeration rules as they were before.
		/// </summary>
		[HarmonyPatch(typeof(Rottable.Instance), MethodType.Constructor,
			typeof(IStateMachineTarget), typeof(Rottable.Def))]
		public static class Rottable_Instance_Constructor_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_EzFoodStorage;

			internal static void Postfix(AttributeModifier ___unrefrigeratedModifier,
					AttributeModifier ___refrigeratedModifier,
					AttributeModifier ___frozenModifier,
					AttributeModifier ___contaminatedAtmosphereModifier,
					AttributeModifier ___normalAtmosphereModifier,
					AttributeModifier ___sterileAtmosphereModifier) {
				// Modifiers were initialized by the constructor
				// Normal: -0.5, Sterile: x0, Contaminated: -1
				// Refrigerated/Frozen: +0.5, Unrefrigerated: +0
				___unrefrigeratedModifier.SetValue(0.0f);
				___refrigeratedModifier.SetValue(0.5f);
				___frozenModifier.SetValue(0.5f);
				___normalAtmosphereModifier.SetValue(-0.5f);
				___contaminatedAtmosphereModifier.SetValue(-1.0f);
				// IsReadonly is dead code, thankfully
				___sterileAtmosphereModifier.SetValue(0.0f);
			}
		}

		/// <summary>
		/// Applied to Rottable.Instance to avoid sterile foods gaining freshness.
		/// </summary>
		[HarmonyPatch(typeof(Rottable.Instance), nameof(Rottable.Instance.RefreshModifiers))]
		public static class Rottable_Instance_RefreshModifiers_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_EzFoodStorage;

			internal static void Postfix(AmountInstance ___rotAmountInstance,
					AttributeModifier ___refrigeratedModifier,
					AttributeModifier ___frozenModifier) {
				var delta = ___rotAmountInstance.deltaAttribute;
				if (delta.GetTotalValue() > 0.0f) {
					// Cannot allow food to gain freshness!
					delta.Remove(___refrigeratedModifier);
					delta.Remove(___frozenModifier);
				}
			}
		}

		/// <summary>
		/// Applied to SolarPanel to unset the base as LI/GI on removal.
		/// </summary>
		[HarmonyPatch(typeof(SolarPanel), "OnCleanUp")]
		public static class SolarPanel_OnCleanUp_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_SolarPanelWiring;

			internal static void Postfix(SolarPanel __instance) {
				var def = __instance.GetComponent<BuildingComplete>().Def;
				int baseCell = Grid.PosToCell(__instance), n = def.WidthInCells;
				var solidChanged = GameScenePartitioner.Instance.solidChangedLayer;
				for (int i = 0; i < n; i++) {
					int cell = Grid.OffsetCell(baseCell, new CellOffset(i - (n - 1) / 2, 0));
					SimMessages.ClearCellProperties(cell, LI_GI);
					Grid.Foundation[cell] = false;
					Grid.SetSolid(cell, false, CellEventLogger.Instance.
						SimCellOccupierForceSolid);
					World.Instance.OnSolidChanged(cell);
					GameScenePartitioner.Instance.TriggerEvent(cell, solidChanged, null);
					Grid.RenderedByWorld[cell] = true;
				}
			}
		}

		/// <summary>
		/// Applied to SolarPanel to set the base as LI/GI on creation.
		/// </summary>
		[HarmonyPatch(typeof(SolarPanel), "OnSpawn")]
		public static class SolarPanel_OnSpawn_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_SolarPanelWiring;

			internal static void Postfix(SolarPanel __instance) {
				var def = __instance.GetComponent<BuildingComplete>().Def;
				int baseCell = Grid.PosToCell(__instance), n = def.WidthInCells;
				var solidChanged = GameScenePartitioner.Instance.solidChangedLayer;
				for (int i = 0; i < n; i++) {
					int cell = Grid.OffsetCell(baseCell, new CellOffset(i - (n - 1) / 2, 0));
					SimMessages.SetCellProperties(cell, LI_GI);
					Grid.Foundation[cell] = true;
					Grid.SetSolid(cell, true, CellEventLogger.Instance.
						SimCellOccupierForceSolid);
					World.Instance.OnSolidChanged(cell);
					GameScenePartitioner.Instance.TriggerEvent(cell, solidChanged, null);
					Grid.RenderedByWorld[cell] = false;
				}
			}
		}

		/// <summary>
		/// Applied to SolarPanelConfig to remove the solid base.
		/// </summary>
		[HarmonyPatch(typeof(SolarPanelConfig), nameof(IBuildingConfig.
			DoPostConfigureComplete))]
		public static class SolarPanelConfig_DoPostConfigureComplete_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_SolarPanelWiring;

			internal static bool Prefix(GameObject go) {
				// Removing defs is harder than just skipping the method
				go.AddOrGet<Repairable>().expectedRepairTime = 52.5f;
				go.AddOrGet<SolarPanel>().powerDistributionOrder = 9;
				go.AddOrGetDef<PoweredActiveController.Def>();
				return false;
			}
		}

		/// <summary>
		/// Applied to SolidLimitValveConfig to disable it when MD-471618 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(SolidLimitValveConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class SolidLimitValveConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD471618_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}
	}
}
