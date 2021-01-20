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
		/// Retrieves the singleton instance of this component, which is created when a game
		/// is loaded or started.
		/// </summary>
		internal static AchievementStateComponent Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance of this component.
		/// </summary>
		internal static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// Triggered when a critter dies of non-natural (old age) causes.
		/// </summary>
		public static void OnCritterKilled() {
			var asc = Instance;
			if (asc != null)
				foreach (var requirement in asc.killRequirements)
					requirement.AddKilledCritter();
		}

		/// <summary>
		/// Triggered when a Duplicant dies.
		/// </summary>
		/// <param name="cause">The cause of death.</param>
		public static void OnDeath(Death cause) {
			var asc = Instance;
			if (cause != null && asc != null) {
				foreach (var requirement in asc.deathRequirements)
					requirement.OnDeath(cause);
				foreach (var requirement in asc.noDeathRequirements)
					requirement.OnDeath(cause);
			}
		}

		/// <summary>
		/// Triggered when a neural vacillator completes.
		/// </summary>
		public static void OnGeneShuffleComplete() {
			var asc = Instance;
			if (asc != null)
				foreach (var requirement in asc.geneRequirements)
					requirement.AddUse();
		}

		/// <summary>
		/// Triggered when a wire overloads.
		/// </summary>
		/// <param name="rating">The rating of the overloaded wire.</param>
		public static void OnOverload(Wire.WattageRating rating) {
			var asc = Instance;
			if (asc != null)
				foreach (var requirement in asc.overloadRequirements)
					requirement.CheckOverload(rating);
		}

		/// <summary>
		/// Triggered when a rocket visits, or returns from, a space destination.
		/// </summary>
		/// <param name="destination">The destination of the mission.</param>
		public static void OnVisit(int destination) {
			var asc = Instance;
			if (asc != null)
				foreach (var requirement in asc.visitPlanetRequirements)
					requirement.OnVisit(destination);
		}

		/// <summary>
		/// Triggers the colony achievement requirement with the specified ID.
		/// </summary>
		/// <param name="achievement">The requirement ID to trigger.</param>
		public static void Trigger(string achievement) {
			if (!string.IsNullOrEmpty(achievement)) {
#if DEBUG
				PUtil.LogDebug("Achievement requirement triggered: " + achievement);
#endif
				var asc = Instance;
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
				var asc = Instance;
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
		private readonly List<BuildNBuildings> buildRequirements;

		/// <summary>
		/// Cached death from cause requirements.
		/// </summary>
		private readonly List<DeathFromCause> deathRequirements;

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

		/// <summary>
		/// Cached no deaths for N cycles requirements.
		/// </summary>
		private readonly List<NoDeathsForNCycles> noDeathRequirements;

		/// <summary>
		/// Cached overload X wire requirements.
		/// </summary>
		private readonly List<OverloadWire> overloadRequirements;

		/// <summary>
		/// Cached visit all planets requirements.
		/// </summary>
		private readonly List<VisitAllPlanets> visitPlanetRequirements;

		internal AchievementStateComponent() {
			buildRequirements = new List<BuildNBuildings>(8);
			deathRequirements = new List<DeathFromCause>(8);
			digRequirements = new List<DigNTiles>(8);
			events = new Dictionary<string, TriggerEvent>(32);
			geneRequirements = new List<UseGeneShufflerNTimes>(4);
			killRequirements = new List<KillNCritters>(8);
			noDeathRequirements = new List<NoDeathsForNCycles>(4);
			overloadRequirements = new List<OverloadWire>(8);
			visitPlanetRequirements = new List<VisitAllPlanets>(4);
		}

		/// <summary>
		/// Collects and caches colony achievement requirements of the parameter type.
		/// 
		/// Achievements cannot change during a game so cache the requirements that need to be
		/// updated.
		/// </summary>
		/// <param name="requirements">The location to save those requirements.</param>
		private void CollectRequirements(IDictionary<Type, IOldList> requirements) {
			var achievements = SaveGame.Instance.GetComponent<ColonyAchievementTracker>();
			if (requirements == null)
				throw new ArgumentNullException("requirements");
			if (achievements == null)
				PUtil.LogError("Achievement list is not initialized!");
			else {
				foreach (var list in requirements.Values)
					list.Clear();
				// Works with any number of achievements that use this type
				foreach (var achievement in achievements.achievements.Values)
					foreach (var requirement in achievement.Requirements)
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
		}

		/// <summary>
		/// Collects and caches the required colony achievement requirements.
		/// </summary>
		internal void CollectRequirements() {
			CollectRequirements(new Dictionary<Type, IOldList>(16) {
				{ typeof(BuildNBuildings), buildRequirements },
				{ typeof(DeathFromCause), deathRequirements },
				{ typeof(DigNTiles), digRequirements },
				{ typeof(KillNCritters), killRequirements },
				{ typeof(NoDeathsForNCycles), noDeathRequirements },
				{ typeof(OverloadWire), overloadRequirements },
				{ typeof(UseGeneShufflerNTimes), geneRequirements },
				{ typeof(VisitAllPlanets), visitPlanetRequirements }
			});
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
			Instance = this;
			MaxKelvinSeen = 0.0f;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.NewBuilding, OnBuildingCompleted);
			Subscribe(DigNTiles.DigComplete, OnDigCompleted);
			// Neutronium discovered in the past?
			var neutronium = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
			if (neutronium != null && WorldInventory.Instance.IsDiscovered(neutronium.tag))
				Trigger(AchievementStrings.ISEEWHATYOUDIDTHERE.ID);
		}
	}
}
