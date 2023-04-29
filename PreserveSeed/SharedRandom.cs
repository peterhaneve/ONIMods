/*
 * Copyright  Peter Han
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

using Klei.CustomSettings;
using System;
using System.Collections.Generic;

namespace PeterHan.PreserveSeed {
	/// <summary>
	/// Generates random numbers based on reproducible seeds.
	/// </summary>
	internal static class SharedRandom {
		/// <summary>
		/// The current shared random value to use, or null if true random is desired.
		/// </summary>
		private static Random random;

		/// <summary>
		/// Whether the shared random value is in use.
		/// </summary>
		internal static bool UseSharedRandom => random != null && PreserveSeedOptions.Instance.
			PreservePodSeed;

		/// <summary>
		/// Retrieves the next seed to use for the KRandom instance.
		/// </summary>
		/// <returns>The seed based on the shared random value.</returns>
		internal static int GetNextSeed() {
			return UseSharedRandom ? random.Next() : UnityEngine.Random.Range(0, int.MaxValue);
		}
		
		/// <summary>
		/// A replacement method for UnityEngine.Random.Range that uses the current
		/// immigration index.
		/// </summary>
		/// <param name="min">The minimum value to report, inclusive.</param>
		/// <param name="max">The maximum value to report, exclusive.</param>
		/// <returns>A random number inside that range.</returns>
		internal static int GetRange(int min, int max) {
			return UseSharedRandom ? random.Next(min, max) : UnityEngine.Random.Range(
				min, max);
		}

		/// <summary>
		/// Resets the shared random seed to again use random Duplicants.
		/// </summary>
		internal static void Reset() {
			random = null;
		}

		/// <summary>
		/// Resets the active distribution decks. ONI does sampling without replacement, which
		/// unfortunately messes up subsequent reloads without clearing the list.
		/// </summary>
		internal static void ResetDistributionDeck() {
			TUNING.DUPLICANTSTATS.rarityDeckActive.Clear();
			TUNING.DUPLICANTSTATS.podTraitConfigurationsActive.Clear();
		}
		
		/// <summary>
		/// Sets the shared random seed to a cluster+seed specific value.
		/// </summary>
		/// <param name="world">The world being used for the seed.</param>
		internal static void SetForWorld(int world) {
			var inst = CustomGameSettings.Instance;
			if (inst != null && PreserveSeedOptions.Instance.PreservePodSeed) {
				var seed = inst.GetCurrentQualitySetting(CustomGameSettingConfigs.
					WorldgenSeed);
				if (seed != null) {
					ResetDistributionDeck();
					random = new Random((int)(seed.coordinate_offset + (world ==
						ClusterManager.INVALID_WORLD_IDX ? 0 : world)));
				}
			}
		}

		/// <summary>
		/// Sets the shared random seed.
		/// </summary>
		/// <param name="seed">The fixed seed from which to genrate random numbers.</param>
		internal static void SetSeed(int seed) {
			random = new Random(seed);
			ResetDistributionDeck();
		}

		/// <summary>
		/// Shuffles a list using the shared random seed.
		/// </summary>
		/// <typeparam name="T">The list type to shuffle.</typeparam>
		/// <param name="list">The list to shuffle.</param>
		internal static void ShuffleSeeded<T>(IList<T> list) {
			list.ShuffleSeeded(new KRandom(GetNextSeed()));
		}
	}
}
