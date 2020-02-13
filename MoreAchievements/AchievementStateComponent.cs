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

using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib;
using System;
using System.Collections.Generic;

using IOldList = System.Collections.IList;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Tracks the state of custom achievements as a component on Game.
	/// </summary>
	public sealed class AchievementStateComponent : KMonoBehaviour {
		/// <summary>
		/// Triggers the colony achievement requirement with the specified ID.
		/// </summary>
		/// <param name="achievement">The requirement ID to trigger.</param>
		public static void Trigger(string achievement) {
			if (!string.IsNullOrEmpty(achievement)) {
				var asc = Game.Instance?.GetComponent<AchievementStateComponent>();
				if (asc != null && asc.events.TryGetValue(achievement, out TriggerEvent evt))
					evt.Trigger();
			}
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
		/// Cached acquire N artifacts requirements.
		/// </summary>
		private readonly List<CollectNArtifacts> artifactRequirements;

		/// <summary>
		/// Cached build N buildings requirements.
		/// </summary>
		private readonly List<BuildNBuildings> buildRequirements;

		/// <summary>
		/// Cached dig N tiles requirements.
		/// </summary>
		private readonly List<DigNTiles> digRequirements;

		/// <summary>
		/// Cached events which can be triggered.
		/// </summary>
		private readonly IDictionary<string, TriggerEvent> events;

		/// <summary>
		/// Cached use neural vacillator N times requirements.
		/// </summary>
		private readonly List<UseGeneShufflerNTimes> geneRequirements;

		/// <summary>
		/// Cached kill N critters requirements.
		/// </summary>
		private readonly List<KillNCritters> killRequirements;

		internal AchievementStateComponent() {
			artifactRequirements = new List<CollectNArtifacts>(4);
			buildRequirements = new List<BuildNBuildings>(8);
			digRequirements = new List<DigNTiles>(8);
			events = new Dictionary<string, TriggerEvent>(32);
			geneRequirements = new List<UseGeneShufflerNTimes>(4);
			killRequirements = new List<KillNCritters>(8);
		}

		/// <summary>
		/// Checks to see if all artifacts have been collected.
		/// </summary>
		public void CheckArtifacts() {
			int have = 0;
			// Count artifacts discovered
			foreach (string name in ArtifactConfig.artifactItems)
				if (WorldInventory.Instance.IsDiscovered(Assets.GetPrefab(name).PrefabID()))
					have++;
			foreach (var requirement in artifactRequirements)
				requirement.Obtained = have;
		}

		/// <summary>
		/// Collects and caches colony achievement requirements of the parameter type.
		/// 
		/// Achievements cannot change during a game so cache the requirements that need to be
		/// updated.
		/// </summary>
		/// <param name="requirements">The location to save those requirements.</param>
		private void CollectRequirements(IDictionary<Type, IOldList> requirements) {
			var achievements = Db.Get().ColonyAchievements?.resources;
			if (requirements == null)
				throw new ArgumentNullException("requirements");
			if (achievements == null)
				PUtil.LogError("Achievement list is not initialized!");
			else
				// Works with any number of achievements that use this type
				foreach (var achievement in achievements)
					foreach (var requirement in achievement.requirementChecklist)
						if (requirement is TriggerEvent evt) {
							string id = evt.ID;
							// Add event trigger
							if (events.ContainsKey(id))
								PUtil.LogWarning("Duplicant trigger event ID: " + id);
							events[id] = evt;
						} else if (requirements.TryGetValue(requirement.GetType(), out
								IOldList ofThisType))
							ofThisType.Add(requirement);
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
			CollectRequirements(new Dictionary<Type, IOldList>(4) {
				{ typeof(BuildNBuildings), buildRequirements },
				{ typeof(CollectNArtifacts), artifactRequirements },
				{ typeof(DigNTiles), digRequirements },
				{ typeof(KillNCritters), killRequirements },
				{ typeof(UseGeneShufflerNTimes), geneRequirements }
			});
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
