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

using System.Collections.Generic;
using Klei.AI;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.FoodTooltip {
	/// <summary>
	/// Refreshes the SimpleInfoScreen when critter parameters or plant parameters change.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class InfoScreenRefresher : KMonoBehaviour {
		private static readonly ICollection<string> EFFECTS = new HashSet<string>() {
			"Happy", "Neutral", "Glum", "Miserable", "FarmTinker"
		};

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private SimpleInfoScreen infoScreen;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// Refreshes the information panel only if a happiness or farmer's touch effect is
		/// modified.
		/// </summary>
		/// <param name="data">The effect that was added or removed.</param>
		private void EffectRefresh(object data) {
			// Effect IDs are hard coded in HappinessMonitor and Tinkerable
			if (data is Effect effect && EFFECTS.Contains(effect.Id) && infoScreen != null)
				infoScreen.RefreshInfoScreen(true);
		}

		/// <summary>
		/// Invoked when an object is selected.
		/// </summary>
		/// <param name="target">The object which was selected.</param>
		internal void OnDeselectTarget(GameObject target) {
			if (target != null) {
				Unsubscribe(target, (int)GameHashes.EffectAdded, EffectRefresh);
				Unsubscribe(target, (int)GameHashes.EffectRemoved, EffectRefresh);
				Unsubscribe(target, (int)GameHashes.Wilt, RefreshInfoPanel);
				Unsubscribe(target, (int)GameHashes.WiltRecover, RefreshInfoPanel);
				Unsubscribe(target, (int)GameHashes.CropSleep, RefreshInfoPanel);
				Unsubscribe(target, (int)GameHashes.CropWakeUp, RefreshInfoPanel);
			}
		}

		/// <summary>
		/// Invoked when an object is deselected.
		/// </summary>
		/// <param name="target">The object which was deselected.</param>
		internal void OnSelectTarget(GameObject target) {
			if (target != null) {
				// These events are only triggered if applicable, so can subscribe to all
				Subscribe(target, (int)GameHashes.EffectAdded, EffectRefresh);
				Subscribe(target, (int)GameHashes.EffectRemoved, EffectRefresh);
				Subscribe(target, (int)GameHashes.Wilt, RefreshInfoPanel);
				Subscribe(target, (int)GameHashes.WiltRecover, RefreshInfoPanel);
				Subscribe(target, (int)GameHashes.CropSleep, RefreshInfoPanel);
				Subscribe(target, (int)GameHashes.CropWakeUp, RefreshInfoPanel);
			}
		}

		/// <summary>
		/// Refreshes the information panel. The argument is always null when reached.
		/// </summary>
		private void RefreshInfoPanel(object _) {
			if (infoScreen != null)
				infoScreen.RefreshInfoScreen(true);
		}
	}
}
