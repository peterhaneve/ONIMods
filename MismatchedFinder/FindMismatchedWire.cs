/*
 * Copyright 2021 Peter Han
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

using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.MismatchedFinder {
	/// <summary>
	/// Conditionally adds a button to wire networks to find segments that have a different
	/// wattage rating than the rest of the network.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class FindMismatchedWire : KMonoBehaviour, IRefreshUserMenu {
		/// <summary>
		/// Handles user menu refresh events system-wide.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<FindMismatchedWire>
			ON_REFRESH_MENU = PGameUtils.CreateUserMenuHandler<FindMismatchedWire>();

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private Wire wire;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// Gets the current network for this wire.
		/// </summary>
		/// <returns>The connected network, or none if the wire is not connected.</returns>
		private ElectricalUtilityNetwork GetNetwork() {
			int cell = Grid.PosToCell(this);
			return Grid.IsValidCell(cell) ? Game.Instance.electricalConduitSystem.
				GetNetworkForCell(cell) as ElectricalUtilityNetwork : null;
		}

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
			base.OnCleanUp();
		}

		/// <summary>
		/// Called to select and center the mismatched wires.
		/// </summary>
		private void OnFindMismatched() {
			var enet = GetNetwork();
			if (wire != null && enet != null) {
				var wattageRating = wire.MaxWattageRating;
				// Just locate the first wire with a mismatched wattage that is less than
				// this wire and select it
				foreach (var badWire in enet.allWires)
					if (badWire.MaxWattageRating < wattageRating && badWire != wire) {
						PGameUtils.CenterAndSelect(badWire);
						break;
					}
			}
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Subscribe((int)GameHashes.RefreshUserMenu, ON_REFRESH_MENU);
		}

		/// <summary>
		/// Called when the info screen for the wire is refreshed.
		/// </summary>
		public void OnRefreshUserMenu() {
			var enet = GetNetwork();
			if (wire != null && enet != null) {
				float wattage = Wire.GetMaxWattageAsFloat(wire.MaxWattageRating);
				if (!Mathf.Approximately(wattage, enet.GetMaxSafeWattage())) {
					Game.Instance?.userMenu?.AddButton(gameObject, new KIconButtonMenu.
						ButtonInfo("action_follow_cam", MismatchedFinderStrings.UI.
						USERMENUOPTIONS.FIND_WIRE, OnFindMismatched, PAction.MaxAction, null,
						null, null, MismatchedFinderStrings.UI.TOOLTIPS.FIND_WIRE));
				}
			}
		}
	}
}
