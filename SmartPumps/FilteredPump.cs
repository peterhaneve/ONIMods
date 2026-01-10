/*
 * Copyright 2026 Peter Han
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

using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// A filtered version of the PumpFixed component.
	/// </summary>
	public class FilteredPump : PumpFixed {
		/// <summary>
		/// Displayed when no matching gas is available to pump.
		/// </summary>
		private static StatusItem NO_GAS_MATCH_TO_PUMP;

		/// <summary>
		/// Displayed when no matching liquid is available to pump.
		/// </summary>
		private static StatusItem NO_LIQUID_MATCH_TO_PUMP;

		/// <summary>
		/// Creates the status items for filtered pumps.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void CreateStatusItems() {
			const string Category = "BUILDING", NoGasMatch = "NoGasMatchToPump",
				NoLiquidMatch = "NoLiquidMatchToPump";
			PDatabaseUtils.AddStatusItemStrings(NoGasMatch, Category, SmartPumpsStrings.
				NOGASTOPUMP_NAME, SmartPumpsStrings.NOGASTOPUMP_DESC);
			PDatabaseUtils.AddStatusItemStrings(NoLiquidMatch, Category, SmartPumpsStrings.
				NOLIQUIDTOPUMP_NAME, SmartPumpsStrings.NOLIQUIDTOPUMP_DESC);
			// String add must occur first
			NO_GAS_MATCH_TO_PUMP = new StatusItem(NoGasMatch, Category,
				"status_item_no_gas_to_pump", StatusItem.IconType.Custom,
				NotificationType.Neutral, false, OverlayModes.GasConduits.ID);
			NO_LIQUID_MATCH_TO_PUMP = new StatusItem(NoLiquidMatch, Category,
				"status_item_no_liquid_to_pump", StatusItem.IconType.Custom,
				NotificationType.Neutral, false, OverlayModes.LiquidConduits.ID);
		}
		
		// These components are automatically populated by KMonoBehaviour
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpReq]
		private Filterable filterable;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// The last element consumed to force sim updates.
		/// </summary>
		private SimHashes lastElement;

		/// <summary>
		/// Checks for pumpable media of the right type in the pump's radius.
		/// 
		/// This version has the detect radius synchronized with the absorb radius.
		/// </summary>
		/// <param name="state">The media state required.</param>
		/// <param name="radius">The radius to check.</param>
		/// <returns>Whether the pump can run.</returns>
		protected override bool IsPumpable(Element.State state, int radius) {
			var element = ElementLoader.GetElementID(filterable.SelectedTag);
			bool validElement = element != SimHashes.Void && element != SimHashes.Vacuum;
			// Force sim update if the element changed
			if (validElement && element != lastElement) {
				consumer.elementToConsume = element;
				RecreateSimHandle();
				consumer.RefreshConsumptionRate();
				lastElement = element;
			}
			return validElement && IsPumpableFixed(gameObject, state, element, radius);
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			lastElement = SimHashes.Void;
			noGasAvailable = NO_GAS_MATCH_TO_PUMP;
			noLiquidAvailable = NO_LIQUID_MATCH_TO_PUMP;
		}
	}
}
