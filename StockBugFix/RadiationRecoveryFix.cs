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

using Klei.AI;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// A component which listens for incapacitation recovery and updates the rad level when
	/// the Duplicant makes it to a cot.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class RadiationRecoveryFix : KMonoBehaviour {
		// This component is automatically populated by KMonoBehaviour
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private Health health;

		[MyCmpGet]
		private Modifiers modifiers;

		[MyCmpReq]
		private KPrefabID prefabID;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// Cures fatal radiation sickness, reducing it to only extreme.
		/// </summary>
		public void CureRadiationSickness(object _) {
			if (modifiers != null) {
				// Yeah yeah this will give Duplicants in the other medical stations a free
				// pass too
				var rads = modifiers.amounts.Get(Db.Get().Amounts.RadiationBalance);
				var smi = gameObject.GetSMI<RadiationMonitor.Instance>();
				float cutoff = 800.0f * (smi?.difficultySettingMod ?? 1.0f);
				if (rads != null && rads.value > cutoff) {
					rads.ApplyDelta(cutoff - rads.value);
					prefabID.RemoveTag(GameTags.RadiationSicknessIncapacitation);
					// Update the state machine too
					if (smi != null) {
						smi.sm.radiationExposure.Set(cutoff, smi);
						smi.GoTo(smi.sm.active.idle);
						// Increase to at least 1 HP to stop loop incapacitation
						if (health != null && health.hitPoints < 1.0f)
							health.hitPoints = 1.0f;
					}
				}
			}
		}

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.IncapacitationRecovery);
			base.OnCleanUp();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.IncapacitationRecovery, CureRadiationSickness);
		}
	}
}
