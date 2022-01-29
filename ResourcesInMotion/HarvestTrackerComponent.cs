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

using KSerialization;
using PeterHan.PLib;
using System.Runtime.Serialization;

namespace PeterHan.ResourcesInMotion {
	/// <summary>
	/// Tracks how long a plant has been waiting for harvest across save/load.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class HarvestTrackerComponent : KMonoBehaviour, ISaveLoadable {
		/// <summary>
		/// The old age value at the last save.
		/// </summary>
		[Serialize]
		private float oldAgeTracker;

		/// <summary>
		/// The state name that this component is tracking.
		/// </summary>
		[Serialize]
		private string targetState;

		/// <summary>
		/// Enters the specified state and starts tracking the harvesting age. If the current
		/// state is already this state, the attribute continues tracking from the last entry.
		/// </summary>
		/// <param name="state">The state which was entered.</param>
		public void EnterState(string state) {
			if (!string.IsNullOrEmpty(state) && targetState != state)
				targetState = state;
		}

		/// <summary>
		/// Exits the specified state and stops tracking the age counter.
		/// </summary>
		/// <param name="state">The state which was entered.</param>
		public void ExitState(string state) {
			if (!string.IsNullOrEmpty(state) && targetState == state) {
				oldAgeTracker = 0.0f;
				targetState = null;
			}
		}

		/// <summary>
		/// Gets the last saved old age value. If the state does not match the state being
		/// tracked, 0.0 will be returned.
		/// </summary>
		/// <param name="state">The state name to check.</param>
		/// <returns></returns>
		public float GetOldAgeTracker(string state) {
			return (string.IsNullOrEmpty(targetState) || state != targetState) ? 0.0f :
				oldAgeTracker;
		}

		[OnSerializing]
		private void OnSerializing() {
			var amountInstance = Db.Get().Amounts.OldAge.Lookup(gameObject);
			if (amountInstance != null && !string.IsNullOrEmpty(targetState))
				oldAgeTracker = amountInstance.value;
		}

		public override string ToString() {
			return "PlantHarvestTracker[state={0},progress={1:F2}]".F(targetState,
				oldAgeTracker);
		}
	}
}
