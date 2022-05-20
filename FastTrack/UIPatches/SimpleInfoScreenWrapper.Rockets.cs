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

using System.Collections.Generic;
using UnityEngine;

using ROCKETS = STRINGS.UI.CLUSTERMAP.ROCKETS;
using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Updates the rocket sections of the default "simple" info screen.
	/// </summary>
	internal sealed partial class SimpleInfoScreenWrapper {
		/// <summary>
		/// Gets the formatted remaining rocket range.
		/// </summary>
		/// <param name="engine">The rocket's engine.</param>
		/// <param name="fuelMass">The fuel mass remaining.</param>
		/// <param name="oxyMass">The oxidizer mass remaining.</param>
		/// <param name="fuelPerDist">The fuel consumed per tile travelled.</param>
		/// <returns>The range tool tip text. The range title is in CACHED_BUILDER.</returns>
		private static string GetRangeLeft(RocketEngineCluster engine, float fuelMass,
				float oxyMass, float fuelPerDist) {
			var text = CACHED_BUILDER;
			string fuelUsage, fuelLeft;
			float burnable = fuelMass, usage = fuelPerDist * Constants.SECONDS_PER_CYCLE;
			bool oxidizerNeeded = false;
			if (engine == null) {
				// You cheater!
				fuelUsage = "0";
				fuelLeft = "0";
			} else if (engine.TryGetComponent(out HEPFuelTank _)) {
				fuelUsage = GameUtil.GetFormattedHighEnergyParticles(usage);
				fuelLeft = GameUtil.GetFormattedHighEnergyParticles(fuelMass);
				// Radbolt engine does not require oxidizer
			} else {
				fuelUsage = GameUtil.GetFormattedMass(usage);
				fuelLeft = GameUtil.GetFormattedMass(fuelMass);
				oxidizerNeeded = engine.requireOxidizer;
				if (oxidizerNeeded)
					burnable = Mathf.Min(burnable, oxyMass);
			}
			text.Clear().AppendLine(ROCKETS.RANGE.TOOLTIP).Append(Constants.
				TABBULLETSTRING).AppendLine(ROCKETS.FUEL_PER_HEX.NAME).Replace("{0}",
				fuelUsage).Append(Constants.TABBULLETSTRING).Append(ROCKETS.FUEL_REMAINING.
				NAME).Append(fuelLeft);
			if (oxidizerNeeded) {
				text.AppendLine().Append(Constants.TABBULLETSTRING).Append(ROCKETS.
					OXIDIZER_REMAINING.NAME);
				FormatStringPatches.GetFormattedMass(text, oxyMass);
			}
			string tooltip = text.ToString();
			text.Clear().Append(ROCKETS.RANGE.NAME);
			float range = (fuelPerDist == 0.0f) ? 0.0f : burnable / fuelPerDist;
			FormatStringPatches.GetFormattedRocketRange(text, range, TimeSlice.None, true);
			return tooltip;
		}

		/// <summary>
		/// Gets the formatted rocket speed.
		/// </summary>
		/// <param name="rocket">The currently selected rocket.</param>
		/// <param name="enginePower">The rocket engine power.</param>
		/// <param name="burden">The total rocket burden.</param>
		/// <returns>The speed tool tip text. The speed title is in CACHED_BUILDER.</returns>
		private static string GetSpeed(CraftModuleInterface rocket, float enginePower,
				float burden) {
			var clustercraft = rocket.m_clustercraft;
			var text = CACHED_BUILDER;
			text.Clear().AppendLine(ROCKETS.SPEED.TOOLTIP).Append(Constants.TABBULLETSTRING).
				Append(ROCKETS.POWER_TOTAL.NAME);
			enginePower.ToRyuHardString(text, 0);
			text.AppendLine().Append(Constants.TABBULLETSTRING).Append(ROCKETS.BURDEN_TOTAL.
				NAME);
			burden.ToRyuHardString(text, 0);
			string tooltip = text.ToString();
			text.Clear().Append(ROCKETS.SPEED.NAME);
			float speed = (clustercraft == null || burden == 0.0f) ? 0.0f : enginePower *
				clustercraft.AutoPilotMultiplier * clustercraft.PilotSkillMultiplier / burden;
			FormatStringPatches.GetFormattedRocketRange(text, speed, TimeSlice.PerCycle, true);
			return tooltip;
		}

		/// <summary>
		/// Refreshes the cargo of a Spaced Out rocket.
		/// </summary>
		/// <param name="parent">The parent where the cargo labels will be placed.</param>
		/// <param name="allCargoBays">The cargo bays found in the rocket.</param>
		private void RefreshCargo(GameObject parent, IList<CargoBayCluster> allCargoBays) {
			int count = 0, n = allCargoBays.Count;
			var text = CACHED_BUILDER;
			for (int i = 0; i < n; i++) {
				var cargoBay = allCargoBays[i];
				var label = GetStorageLabel(parent, "cargoBay_" + count.ToString());
				var storage = cargoBay.storage;
				var items = storage.GetItems();
				float mass = 0.0f;
				int nitems = items.Count;
				count++;
				text.Clear();
				for (int j = 0; j < nitems; j++) {
					var item = items[j];
					if (item.TryGetComponent(out PrimaryElement pe)) {
						float m = pe.Mass;
						if (text.Length > 0)
							text.AppendLine();
						text.Append(item.GetProperName()).Append(" : ");
						FormatStringPatches.GetFormattedMass(text, m);
						mass += m;
					}
				}
				label.tooltip.SetSimpleTooltip(text.ToString());
				label.SetAllowDrop(false, storage, null);
				text.Clear().Append(cargoBay.GetProperName()).Append(": ");
				FormatStringPatches.GetFormattedMass(text, mass);
				text.Append('/');
				FormatStringPatches.GetFormattedMass(text, storage.capacityKg);
				label.text.SetText(text);
				label.FreezeIfMatch(text.Length);
				rocketLabels.Add(label);
			}
		}

		/// <summary>
		/// Refreshes the Spaced Out rocket information.
		/// </summary>
		private void RefreshRocket() {
			var rocketInterface = lastSelection.rocketInterface;
			var rocketModule = lastSelection.rocketModule;
			var rocketStatus = sis.rocketStatusContainer;
			var text = CACHED_BUILDER;
			string tooltip;
			if (rocketInterface != null) {
				RefreshRocketStats(rocketStatus, rocketInterface);
				rocketStatus.SetLabel("RocketSpacer2", "", "");
				RefreshRocketModules();
			}
			if (rocketModule != null) {
				float burden = rocketModule.performanceStats.Burden;
				float enginePower = rocketModule.performanceStats.EnginePower;
				// 1 string concat is no worse than the builder
				rocketStatus.SetLabel("ModuleStats", ROCKETS.MODULE_STATS.NAME + lastSelection.
					selectable.GetProperName(), ROCKETS.MODULE_STATS.TOOLTIP);
				if (burden != 0.0f) {
					text.Clear();
					burden.ToRyuHardString(text, 0);
					tooltip = text.ToString();
					text.Clear().Append(Constants.TABBULLETSTRING).Append(ROCKETS.
						BURDEN_MODULE.NAME).Append(tooltip);
					rocketStatus.SetLabel("LocalBurden", text.ToString(), ROCKETS.
						BURDEN_MODULE.TOOLTIP.Format(tooltip));
				}
				if (enginePower != 0.0f) {
					text.Clear();
					enginePower.ToRyuHardString(text, 0);
					tooltip = text.ToString();
					text.Clear().Append(Constants.TABBULLETSTRING).Append(ROCKETS.
						POWER_MODULE.NAME).Append(tooltip);
					rocketStatus.SetLabel("LocalPower", text.ToString(), ROCKETS.POWER_MODULE.
						TOOLTIP.Format(tooltip));
				}
			}
			rocketStatus.Commit();
		}

		/// <summary>
		/// Refreshes the Spaced Out rocket module statistics.
		/// </summary>
		/// <param name="rocketStatus">The info panel to update.</param>
		/// <param name="rocket">The currently selected rocket.</param>
		private static void RefreshRocketStats(CollapsibleDetailContentPanel rocketStatus,
				CraftModuleInterface rocket) {
			RocketEngineCluster engine = null;
			int height = 0, maxHeight = TUNING.ROCKETRY.ROCKET_HEIGHT.MAX_MODULE_STACK_HEIGHT;
			var modules = rocket.clusterModules;
			int n = modules.Count;
			float oxyMass = 0.0f, fuelMass = 0.0f, fuelPerDist = 0.0f, burden = 0.0f,
				enginePower = 0.0f;
			var text = CACHED_BUILDER;
			// Find the engine first
			for (int i = 0; i < n; i++) {
				var module = modules[i].Get();
				if (module != null && module.TryGetComponent(out engine)) {
					var perf = module.performanceStats;
					fuelPerDist = perf.FuelKilogramPerDistance;
					enginePower = perf.EnginePower;
					break;
				}
			}
			for (int i = 0; i < n; i++) {
				var module = modules[i].Get();
				if (module != null) {
					if (engine != null) {
						// Some engines have built-in fuel tanks
						if (module.TryGetComponent(out IFuelTank fuelTank))
							fuelMass += fuelTank.Storage.GetAmountAvailable(engine.fuelTag);
						// Do not exclude future combo LF/O tanks from mods ;)
						if (engine.requireOxidizer && module.TryGetComponent(
								out OxidizerTank oxyTank))
							oxyMass += oxyTank.TotalOxidizerPower;
					}
					if (module.TryGetComponent(out Building building))
						height += building.Def.HeightInCells;
					burden += module.performanceStats.Burden;
				}
			}
			// Range
			string tooltip = GetRangeLeft(engine, Mathf.Ceil(fuelMass), Mathf.Ceil(oxyMass),
				fuelPerDist);
			rocketStatus.SetLabel("RangeRemaining", text.ToString(), tooltip);
			// Speed
			tooltip = GetSpeed(rocket, enginePower, burden);
			rocketStatus.SetLabel("Speed", text.ToString(), tooltip);
			if (engine != null)
				maxHeight = engine.maxHeight;
			// Height
			string maxHeightStr = maxHeight.ToString();
			tooltip = text.Clear().Append(ROCKETS.MAX_HEIGHT.TOOLTIP).Replace("{0}",
				engine.GetProperName()).Replace("{1}", maxHeightStr).ToString();
			text.Clear().Append(ROCKETS.MAX_HEIGHT.NAME).Replace("{0}", height.
				ToString()).Replace("{1}", maxHeightStr);
			rocketStatus.SetLabel("MaxHeight", text.ToString(), tooltip);
		}

		/// <summary>
		/// Refreshes the rocket modules on this object.
		/// </summary>
		private void RefreshRocketModules() {
			string moduleName = null;
			var rocketInterface = lastSelection.rocketInterface;
			var parent = sis.rocketStatusContainer.Content.gameObject;
			var text = CACHED_BUILDER;
			var allModules = rocketInterface.ClusterModules;
			int count = 0, n = allModules.Count;
			var allCargoBays = ListPool<CargoBayCluster, RocketSimpleInfoPanel>.
				Allocate();
			setInactive.UnionWith(rocketLabels);
			rocketLabels.Clear();
			// Iterates the rocket again, but needs to be done after the engine stats
			for (int i = 0; i < n; i++) {
				var module = allModules[i].Get();
				if (module != null) {
					if (module.TryGetComponent(out ArtifactModule artModule)) {
						var label = GetStorageLabel(parent, "artifactModule_" + count);
						var occupant = artModule.Occupant;
						count++;
						if (moduleName == null)
							moduleName = artModule.GetProperName();
						text.Clear().Append(moduleName).Append(": ");
						if (occupant != null)
							text.Append(occupant.GetProperName());
						else
							text.Append(ROCKETS.ARTIFACT_MODULE.EMPTY);
						label.text.SetText(text);
						label.FreezeIfMatch(text.Length);
						label.SetAllowDrop(false, null, artModule.Occupant);
						rocketLabels.Add(label);
					} else if (module.TryGetComponent(out CargoBayCluster cargoBay))
						allCargoBays.Add(cargoBay);
				}
			}
			if (allCargoBays.Count > 0)
				RefreshCargo(parent, allCargoBays);
			allCargoBays.Recycle();
			// Only turn off the things that are gone
			setInactive.ExceptWith(rocketLabels);
			foreach (var inactive in setInactive)
				inactive.SetActive(false);
			setInactive.Clear();
		}
	}
}
