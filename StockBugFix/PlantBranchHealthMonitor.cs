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

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Monitors PlantBranch health and updates the trunk when it changes.
	/// </summary>
	[SkipSaveFileSerialization]
	internal class PlantBranchHealthMonitor : KMonoBehaviour {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MySmiGet]
		private PlantBranch.Instance branch;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		protected override void OnCleanUp() {
			TriggerEvent();
			Unsubscribe((int)GameHashes.Grow);
			Unsubscribe((int)GameHashes.Wilt);
			Unsubscribe((int)GameHashes.WiltRecover);
			base.OnCleanUp();
		}

		private void OnGrow(object _) {
			TriggerEvent();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.Grow, OnGrow);
			Subscribe((int)GameHashes.Wilt, OnGrow);
			Subscribe((int)GameHashes.WiltRecover, OnGrow);
		}

		private void TriggerEvent() {
			if (branch != null && branch.HasTrunk)
				branch.trunk.gameObject.Trigger((int)GameHashes.Grow);
		}
	}
}
