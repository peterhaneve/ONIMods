/*
 * Copyright 2025 Peter Han
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
using System.Text;
using UnityEngine;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Optimizes the horribly slow Drillcone side screen.
	/// </summary>
	public sealed class HarvestSideScreenWrapper {
		/// <summary>
		/// The length of a drill cone mining "cycle". Could not find this constant in the
		/// game code.
		/// </summary>
		public const float MINING_CYCLE = 4.0f;

		/// <summary>
		/// The time in sim seconds between drillcone screen refreshes.
		/// </summary>
		private const double REFRESH_INTERVAL = 0.2;

		/// <summary>
		/// Avoids allocating a new instance every time text needs to be formatted.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static HarvestSideScreenWrapper instance;
		
		/// <summary>
		/// Applies all drill cone optimization patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			harmony.Patch(typeof(HarvestModuleSideScreen), nameof(HarvestModuleSideScreen.
				IsValidForTarget), prefix: new HarmonyMethod(typeof(HarvestSideScreenWrapper),
				nameof(IsValidForTarget_Prefix)));
			harmony.Patch(typeof(HarvestModuleSideScreen), nameof(HarvestModuleSideScreen.
				OnShow), postfix: new HarmonyMethod(typeof(HarvestSideScreenWrapper),
				nameof(OnShow_Postfix)));
			harmony.Patch(typeof(HarvestModuleSideScreen), nameof(HarvestModuleSideScreen.
				SetTarget), postfix: new HarmonyMethod(typeof(HarvestSideScreenWrapper),
				nameof(SetTarget_Postfix)));
			harmony.Patch(typeof(HarvestModuleSideScreen), nameof(HarvestModuleSideScreen.
				SimEveryTick), prefix: new HarmonyMethod(typeof(HarvestSideScreenWrapper),
				nameof(SimEveryTick_Prefix)));
		}

		/// <summary>
		/// Called at shutdown to avoid leaking references.
		/// </summary>
		internal static void Cleanup() {
			instance = null;
		}
		
		/// <summary>
		/// Initializes and resets the last selected item.
		/// </summary>
		internal static void Init() {
			Cleanup();
			instance = new HarvestSideScreenWrapper();
		}

		/// <summary>
		/// Applied before IsValidForTarget runs.
		/// </summary>
		private static bool IsValidForTarget_Prefix(GameObject target, ref bool __result) {
			__result = DlcManager.FeatureClusterSpaceEnabled() && target.
				TryGetComponent(out Clustercraft craft) && GetDrillcone(craft) != null;
			return false;
		}

		/// <summary>
		/// Finds any drill cone module on the selected rocket.
		/// </summary>
		/// <param name="craft">The rocket to search.</param>
		/// <returns>The drill cone module, or null if none is found.</returns>
		internal static RocketModuleCluster GetDrillcone(Clustercraft craft) {
			var modules = craft.ModuleInterface.ClusterModules;
			int n = modules.Count;
			RocketModuleCluster result = null;
			for (int i = 0; i < n; i++) {
				var module = modules[i].Get();
				if (module != null && module.TryGetComponent(out StateMachineController smc)) {
					var def = smc.GetDef<ResourceHarvestModule.Def>();
					if (def != null) {
						result = module;
						break;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Applied after OnShow runs.
		/// </summary>
		private static void OnShow_Postfix(HarvestModuleSideScreen __instance, bool show) {
			if (!show) {
				var inst = instance;
				__instance.targetCraft = null;
				if (inst != null)
					inst.Reset();
			}
		}

		/// <summary>
		/// Applied after SetTarget runs.
		/// </summary>
		private static void SetTarget_Postfix(HarvestModuleSideScreen __instance) {
			var inst = instance;
			if (inst != null)
				// Reset for the next update
				instance.Reset();
		}

		/// <summary>
		/// Applied before SimEveryTick runs.
		/// </summary>
		private static bool SimEveryTick_Prefix(HarvestModuleSideScreen __instance) {
			var inst = instance;
			if (__instance != null && inst != null && __instance.isActiveAndEnabled)
				inst.Update(__instance);
			return inst == null;
		}

		/// <summary>
		/// The invariant text describing how much diamond is left.
		/// </summary>
		private readonly string diamondHeader;

		/// <summary>
		/// The last mass displayed.
		/// </summary>
		private float lastMass;
		
		/// <summary>
		/// The last object selected in the drillcone pane.
		/// </summary>
		private LastSelectionDetails lastSelection;

		/// <summary>
		/// Prevent updates until the next update.
		/// </summary>
		private double lastUpdate;

		/// <summary>
		/// Whether the drillcone was harvesting at the last update.
		/// </summary>
		private int wasHarvesting;

		internal HarvestSideScreenWrapper() {
			diamondHeader = ElementLoader.GetElement(SimHashes.Diamond.CreateTag()).name +
				": ";
			Reset();
		}

		/// <summary>
		/// Updates the side screen UI.
		/// </summary>
		private void Redraw() {
			var drillcone = lastSelection.drillcone;
			var progressBar = lastSelection.drillProgress;
			var diamonds = lastSelection.diamondsLeft;
			var storage = lastSelection.drillStorage;
			float mass = storage.MassStored();
			if (drillcone.sm.canHarvest.Get(drillcone)) {
				progressBar.SetFillPercentage((drillcone.timeinstate % MINING_CYCLE) /
					MINING_CYCLE);
				if (wasHarvesting != 1)
					progressBar.label.SetText(STRINGS.UI.UISIDESCREENS.
						HARVESTMODULESIDESCREEN.MINING_IN_PROGRESS);
				wasHarvesting = 1;
			} else {
				if (wasHarvesting != 0) {
					progressBar.SetFillPercentage(0f);
					progressBar.label.SetText(STRINGS.UI.UISIDESCREENS.
						HARVESTMODULESIDESCREEN.MINING_STOPPED);
				}
				wasHarvesting = 0;
			}
			if (!Mathf.Approximately(lastMass, mass)) {
				diamonds.SetFillPercentage(mass / storage.Capacity());
				CACHED_BUILDER.Clear().Append(diamondHeader);
				FormatStringPatches.GetFormattedMass(CACHED_BUILDER, mass);
				diamonds.label.SetText(CACHED_BUILDER.ToString());
				lastMass = mass;
			}
		}

		/// <summary>
		/// Resets the cached drillcone information.
		/// </summary>
		private void Reset() {
			lastMass = float.NegativeInfinity;
			lastSelection = default;
			lastUpdate = 0.0;
			wasHarvesting = -1;
		}

		/// <summary>
		/// Updates the screen, but only once every 200ms.
		/// </summary>
		/// <param name="screen">The side screen to update.</param>
		internal void Update(HarvestModuleSideScreen screen) {
			double now = Time.timeAsDouble;
			var targetCraft = screen.targetCraft;
			if (targetCraft != null && (double.IsNaN(lastUpdate) || now - lastUpdate >
					REFRESH_INTERVAL)) {
				if (lastSelection.drillStorage == null)
					lastSelection = new LastSelectionDetails(screen, targetCraft);
				if (lastSelection.drillStorage != null)
					Redraw();
				lastUpdate = now;
			}
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// </summary>
		private readonly struct LastSelectionDetails {
			internal readonly GenericUIProgressBar diamondsLeft;

			internal readonly ResourceHarvestModule.StatesInstance drillcone;

			internal readonly GenericUIProgressBar drillProgress;
			
			internal readonly Storage drillStorage;

			internal LastSelectionDetails(HarvestModuleSideScreen instance,
					Clustercraft craft) {
				RocketModuleCluster rmc;
				if (craft != null && (rmc = GetDrillcone(craft)) != null) {
					drillcone = rmc.GetSMI<ResourceHarvestModule.StatesInstance>();
					drillStorage = drillcone?.GetComponent<Storage>();
				} else {
					drillcone = null;
					drillStorage = null;
				}
				if (instance.TryGetComponent(out HierarchyReferences refs)) {
					drillProgress = refs.GetReference<GenericUIProgressBar>("progressBar");
					diamondsLeft = refs.GetReference<GenericUIProgressBar>(
						"diamondProgressBar");
				} else {
					drillProgress = null;
					diamondsLeft = null;
				}
			}
		}
	}
}
