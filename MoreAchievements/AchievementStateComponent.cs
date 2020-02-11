/*
 * Copyright 2020 Peter Han
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

using Database;
using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib;
using System;
using System.Collections.Generic;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Tracks the state of custom achievements as a component on Game.
	/// </summary>
	public sealed class AchievementStateComponent : KMonoBehaviour {
		/// <summary>
		/// Collects and caches colony achievement requirements of the parameter type.
		/// 
		/// Achievements cannot change during a game so cache the requirements that need to be
		/// updated.
		/// </summary>
		/// <typeparam name="T">The type of requirement to collect.</typeparam>
		/// <param name="requirements">The location to save those requirements.</param>
		private static void CollectRequirements<T>(ICollection<T> requirements) where T :
				ColonyAchievementRequirement {
			var achievements = Db.Get().ColonyAchievements.resources;
			if (requirements == null)
				throw new ArgumentNullException("requirements");
			requirements.Clear();
			if (achievements != null)
				// Works with any number of achievements that use this type
				foreach (var achievement in achievements)
					foreach (var requirement in achievement.requirementChecklist)
						if (requirement is T ourRequirement)
							requirements.Add(ourRequirement);
		}

		/// <summary>
		/// Updates the maximum temperature seen on any building.
		/// </summary>
		/// <param name="temp">The temperature in Kelvin.</param>
		public static void UpdateMaxKelvin(float temp) {
			if (temp > 0.0f && !temp.IsNaNOrInfinity()) {
				var asc = Game.Instance?.GetComponent<AchievementStateComponent>();
				if (asc != null)
					asc.MaxKelvinSeen = Math.Max(asc.MaxKelvinSeen, temp);
			}
		}

		/// <summary>
		/// The maximum temperature seen on a building.
		/// </summary>
		public float MaxKelvinSeen { get; private set; }

		/// <summary>
		/// Cached build N buildings requirements.
		/// </summary>
		private readonly ICollection<BuildNBuildings> buildRequirements;

		/// <summary>
		/// Cached dig N tiles requirements.
		/// </summary>
		private readonly ICollection<DigNTiles> digRequirements;

		/// <summary>
		/// Cached use neural vacillator N times requirements.
		/// </summary>
		private readonly ICollection<UseGeneShufflerNTimes> geneRequirements;

		/// <summary>
		/// Cached kill N critters requirements.
		/// </summary>
		private readonly ICollection<KillNCritters> killRequirements;

		internal AchievementStateComponent() {
			buildRequirements = new List<BuildNBuildings>(8);
			digRequirements = new List<DigNTiles>(8);
			geneRequirements = new List<UseGeneShufflerNTimes>(4);
			killRequirements = new List<KillNCritters>(8);
		}

		/// <summary>
		/// Called when a building is completed.
		/// </summary>
		private void OnBuildingCompleted(object _) {
			foreach (var requirement in buildRequirements)
				requirement.AddBuilding();
		}

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.NewBuilding);
			Unsubscribe(DigNTiles.DigComplete);
			base.OnCleanUp();
		}

		/// <summary>
		/// Called when a dig errand is completed.
		/// </summary>
		private void OnDigCompleted(object _) {
			foreach (var requirement in digRequirements)
				requirement.AddDugTile();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			CollectRequirements(buildRequirements);
			CollectRequirements(digRequirements);
			CollectRequirements(geneRequirements);
			CollectRequirements(killRequirements);
			MaxKelvinSeen = 0.0f;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.NewBuilding, OnBuildingCompleted);
			Subscribe(DigNTiles.DigComplete, OnDigCompleted);
		}

		/// <summary>
		/// Triggered when a critter dies of non-natural (old age) causes.
		/// </summary>
		public void OnCritterKilled() {
			foreach (var requirement in killRequirements)
				requirement.AddKilledCritter();
		}

		/// <summary>
		/// Triggered when a neural vacillator completes.
		/// </summary>
		public void OnGeneShuffleComplete() {
			foreach (var requirement in geneRequirements)
				requirement.AddUse();
		}
	}
}
