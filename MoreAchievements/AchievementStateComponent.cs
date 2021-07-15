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

using KSerialization;
using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Tracks the state of custom achievements as a component on Game.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class AchievementStateComponent : KMonoBehaviour, ISim1000ms {
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
				asc.CrittersKilled++;
		}

		/// <summary>
		/// Triggered when a Duplicant dies.
		/// </summary>
		/// <param name="cause">The cause of death.</param>
		public static void OnDeath(Death cause) {
			var asc = Instance;
			var instance = GameClock.Instance;
			if (cause != null) {
				Trigger(DeathFromCause.PREFIX + cause.Id);
				if (instance != null)
					asc.LastDeath = instance.GetCycle();
			}
		}

		/// <summary>
		/// Triggered when a neural vacillator completes.
		/// </summary>
		public static void OnGeneShuffleComplete() {
			var asc = Instance;
			if (asc != null)
				asc.GeneShufflerUses++;
		}

		/// <summary>
		/// Triggered when a wire overloads.
		/// </summary>
		/// <param name="rating">The rating of the overloaded wire.</param>
		public static void OnOverload(Wire.WattageRating rating) {
			Trigger(OverloadWire.PREFIX + rating.ToString());
		}

		/// <summary>
		/// Triggered when a rocket visits, or returns from, a space destination.
		/// </summary>
		/// <param name="destination">The destination of the mission.</param>
		public static void OnVisit(int destination) {
			Instance?.PlanetsVisited?.Add(destination);
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
				var te = Instance?.TriggerEvents;
				if (te != null)
					te[achievement] = true;
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

		#region BuildNBuildings
		/// <summary>
		/// The number of buildings built.
		/// </summary>
		[Serialize]
		internal int BuildingsBuilt;
		#endregion

		#region CollectNArtifacts
		/// <summary>
		/// The number of artifact types obtained. Not serialized!
		/// </summary>
		internal int ArtifactsObtained;
		#endregion

		#region DigNTiles
		/// <summary>
		/// The number of tiles dug up.
		/// </summary>
		[Serialize]
		internal int TilesDug;
		#endregion

		#region HeatBuildingToXKelvin
		/// <summary>
		/// The maximum temperature seen on a building.
		/// </summary>
		internal float MaxKelvinSeen;
		#endregion

		#region KillNCritters
		/// <summary>
		/// The number of critters killed.
		/// </summary>
		internal int CrittersKilled;
		#endregion

		#region NoDeathsForNCycles
		/// <summary>
		/// The cycle number of the last death.
		/// </summary>
		[Serialize]
		internal int LastDeath;
		#endregion

		#region ReachXAllAttributes
		/// <summary>
		/// The attributes which will be checked for "Jack of All Trades".
		/// </summary>
		private Klei.AI.Attribute[] VarietyAttributes;

		/// <summary>
		/// The highest value achieved by a Duplicant across all attributes checked. This
		/// works differently than BestAttributeValue because it is scored across one Duplicant
		/// at a time - Machinery 20 Athletics 18 scores as 18, but two different Duplicants
		/// with Machinery 20 and Athletics 18 may score lower.
		/// </summary>
		[Serialize]
		internal float BestVarietyValue;
		#endregion

		#region ReachXAttributeValue
		/// <summary>
		/// The highest value achieved by a Duplicant for the attributes listed in the
		/// collection.
		/// </summary>
		[Serialize]
		internal IDictionary<string, float> BestAttributeValue;
		#endregion

		#region TriggerEvent
		/// <summary>
		/// Logs the status of events which can be triggered.
		/// </summary>
		[Serialize]
		internal IDictionary<string, bool> TriggerEvents;
		#endregion

		#region UseGeneShufflerNTimes
		/// <summary>
		/// The number of times that the Neural Vacillator has been used.
		/// </summary>
		[Serialize]
		internal int GeneShufflerUses;
		#endregion

		#region VisitAllPlanets
		/// <summary>
		/// The destination IDs already visited.
		/// </summary>
		[Serialize]
		internal ICollection<int> PlanetsVisited;

		/// <summary>
		/// The number of planets which must be visited. Not serialized.
		/// </summary>
		internal int PlanetsRequired;
		#endregion

		public AchievementStateComponent() {
			ArtifactsObtained = 0;
			BestVarietyValue = 0.0f;
			BuildingsBuilt = 0;
			CrittersKilled = 0;
			GeneShufflerUses = 0;
			MaxKelvinSeen = 0.0f;
			LastDeath = -1;
			PlanetsRequired = int.MaxValue;
			TilesDug = 0;
		}

		/// <summary>
		/// Checks the colony summary to guess the date of the last possible death.
		/// </summary>
		private void InitGrimReaper() {
			// Look for the last dip in Duplicant count
			float lastValue = -1.0f;
			RetiredColonyData.RetiredColonyStatistic[] stats;
			try {
				var data = RetireColonyUtility.GetCurrentColonyRetiredColonyData();
				if ((stats = data?.Stats) != null && data.cycleCount > 0) {
					var liveDupes = new SortedList<int, float>(stats.Length);
					// Copy and sort the values
					foreach (var cycleData in stats)
						if (cycleData.id == RetiredColonyData.DataIDs.LiveDuplicants) {
							foreach (var entry in cycleData.value)
								liveDupes[Mathf.RoundToInt(entry.first)] = entry.second;
							break;
						}
					LastDeath = 0;
					// Sorted by cycle now
					foreach (var pair in liveDupes) {
						float dupes = pair.Value;
						if (dupes < lastValue)
							LastDeath = pair.Key;
						lastValue = dupes;
					}
					liveDupes.Clear();
				}
			} catch (Exception e) {
				PUtil.LogWarning("Unable to determine the last date of death:");
				PUtil.LogExcWarn(e);
				LastDeath = GameClock.Instance?.GetCycle() ?? 0;
			}
		}

		/// <summary>
		/// Called when a building is completed.
		/// </summary>
		private void OnBuildingCompleted(object _) {
			BuildingsBuilt++;
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
			TilesDug++;
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Instance = this;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			if (BuildingsBuilt == 0)
				// Not yet initialized, fill with number of completed buildings
				BuildingsBuilt = Components.BuildingCompletes.Count;
			if (PlanetsVisited == null)
				PlanetsVisited = new HashSet<int>();
			if (TriggerEvents == null)
				TriggerEvents = new Dictionary<string, bool>(64);
			if (BestAttributeValue == null)
				BestAttributeValue = new Dictionary<string, float>(64);
			if (LastDeath < 0)
				InitGrimReaper();
			var dbAttr = Db.Get().Attributes;
			VarietyAttributes = new Klei.AI.Attribute[] { dbAttr.Art, dbAttr.Athletics,
				dbAttr.Botanist, dbAttr.Caring, dbAttr.Construction, dbAttr.Cooking,
				dbAttr.Digging, dbAttr.Learning, dbAttr.Machinery, dbAttr.Ranching,
				dbAttr.Strength };
			// Neutronium discovered?
			var neutronium = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
			if (neutronium != null && DiscoveredResources.Instance.IsDiscovered(neutronium.tag))
				Trigger(AchievementStrings.ISEEWHATYOUDIDTHERE.ID);
			PUtil.LogDebug("World count: " + ClusterManager.Instance.worldCount);
			if (DlcManager.IsExpansion1Active())
				// DLC STARMAP
				PlanetsRequired = ClusterManager.Instance.worldCount;
			else {
				// VANILLA STARMAP
				var dest = SpacecraftManager.instance?.destinations;
				if (dest != null) {
					int count = 0;
					// Exclude unreachable destinations (earth) but include temporal tear
					foreach (var destination in dest)
						if (destination.GetDestinationType()?.visitable == true)
							count++;
					if (count > 0)
						PlanetsRequired = count;
				}
			}
			Subscribe((int)GameHashes.NewBuilding, OnBuildingCompleted);
			Subscribe(DigNTiles.DigComplete, OnDigCompleted);
		}

		public void Sim1000ms(float dt) {
			int have = 0;
			// Count artifacts discovered
			foreach (string name in ArtifactConfig.artifactItems)
				if (DiscoveredResources.Instance.IsDiscovered(Assets.GetPrefab(name).
						PrefabID()))
					have++;
			ArtifactsObtained = have;
			foreach (var duplicant in Components.LiveMinionIdentities.Items)
				if (duplicant != null) {
					float minValue = float.MaxValue;
					// Find the worst attribute on this Duplicant for JoaT
					foreach (var attribute in VarietyAttributes) {
						float attrValue = attribute.Lookup(duplicant)?.GetTotalValue() ?? 0.0f;
						if (attrValue < minValue)
							minValue = attrValue;
					}
					// If this Duplicant is better than previous jester, update it
					if (minValue >= BestVarietyValue)
						BestVarietyValue = minValue;
				}
			// For each value requested, update the value if needed
			var keys = ListPool<string, AchievementStateComponent>.Allocate();
			keys.Clear();
			keys.AddRange(BestAttributeValue.Keys);
			foreach (var attribute in keys) {
				// Check each duplicant for the best value
				float best = 0.0f;
				var attr = Db.Get().Attributes.Get(attribute);
				foreach (var duplicant in Components.LiveMinionIdentities.Items)
					if (duplicant != null)
						best = Math.Max(best, attr.Lookup(duplicant).GetTotalValue());
				BestAttributeValue[attribute] = best;
			}
			keys.Recycle();
			// Mark visited worlds for DLC
			if (DlcManager.IsExpansion1Active())
				foreach (var world in ClusterManager.Instance.WorldContainers)
					if (world.IsDupeVisited)
						PlanetsVisited.Add(world.id);
		}
	}
}
