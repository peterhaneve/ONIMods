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
			var gc = GameClock.Instance;
			if (cause != null) {
				Trigger(DeathFromCause.PREFIX + cause.Id);
				if (gc != null)
					asc.LastDeath = gc.GetCycle();
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
			Trigger(OverloadWire.PREFIX + rating);
		}

		/// <summary>
		/// Triggered when a rocket visits, or returns from, a space destination.
		/// </summary>
		/// <param name="destination">The destination of the mission.</param>
		public static void OnVisit(int destination) {
			var asc = Instance;
			if (asc != null)
				asc.PlanetsVisited?.Add(destination);
		}

		/// <summary>
		/// Triggers the colony achievement requirement with the specified ID.
		/// </summary>
		/// <param name="achievement">The requirement ID to trigger.</param>
		public static void Trigger(string achievement) {
			var asc = Instance;
			if (!string.IsNullOrEmpty(achievement) && asc != null) {
#if DEBUG
				PUtil.LogDebug("Achievement requirement triggered: " + achievement);
#endif
				var te = asc.TriggerEvents;
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

		/// <summary>
		/// The list of prefab IDs that are considered foods or primary food ingredients.
		/// </summary>
		private readonly ISet<string> foods;

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
		[Serialize]
		internal int CrittersKilled;
		#endregion

		#region NoDeathsForNCycles
		/// <summary>
		/// The cycle number of the last death.
		/// </summary>
		[Serialize]
		internal int LastDeath;
		#endregion

		#region NoFarmables
		/// <summary>
		/// Set to true if a food plant is planted.
		/// </summary>
		[Serialize]
		internal bool LocavoreFailed;
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
		internal Dictionary<string, float> BestAttributeValue;
		#endregion

		#region TriggerEvent
		/// <summary>
		/// Logs the status of events which can be triggered.
		/// </summary>
		[Serialize]
		internal Dictionary<string, bool> TriggerEvents;
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
		internal HashSet<int> PlanetsVisited;

		/// <summary>
		/// The number of planets which must be visited. Not serialized.
		/// </summary>
		internal int PlanetsRequired;
		#endregion

		public AchievementStateComponent() {
			ArtifactsObtained = 0;
			BestVarietyValue = 0.0f;
			MaxKelvinSeen = 0.0f;
			PlanetsRequired = int.MaxValue;
			foods = new HashSet<string>();
		}

		/// <summary>
		/// Checks a crop's products, and fails Locavore if it produces food.
		/// </summary>
		/// <param name="crop">The crop to check.</param>
		/// <returns>true if the achievement was failed, or false otherwise.</returns>
		internal bool CheckLocavore(Crop crop) {
			var products = crop.cropVal.products;
			int n = products.Count;
			bool result = false;
			for (int i = 0; i < n && !result; i++)
				if (IsFoodCrop(products[i].cropId)) {
					FailLocavore();
					result = true;
				}
			return result;
		}

		/// <summary>
		/// Counts the number of artifacts that have been obtained.
		/// </summary>
		private void CountArtifacts() {
			int have = 0, n;
			// Count artifacts discovered
			foreach (var pair in ArtifactConfig.artifactItems) {
				var artifacts = pair.Value;
				n = artifacts.Count;
				for (int i = 0; i < n; i++)
					if (DiscoveredResources.Instance.IsDiscovered(Assets.GetPrefab(
							artifacts[i]).PrefabID()))
						have++;
			}
			ArtifactsObtained = have;
		}

		/// <summary>
		/// Marks the Locavore achievement as failed.
		/// </summary>
		internal void FailLocavore() {
			LocavoreFailed = true;
		}

		/// <summary>
		/// Finds the best candidate for the Jack of All Trades achievement.
		/// </summary>
		private void FindJester() {
			var dupes = Components.LiveMinionIdentities.Items;
			int n = dupes.Count, va = VarietyAttributes.Length;
			for (int i = 0; i < n; i++) {
				var duplicant = dupes[i];
				if (duplicant != null) {
					float minValue = float.MaxValue;
					// Find the worst attribute on this Duplicant for JoaT
					for (int j = 0; j < va; j++) {
						float attrValue = VarietyAttributes[j].Lookup(duplicant)?.
							GetTotalValue() ?? 0.0f;
						if (attrValue < minValue)
							minValue = attrValue;
					}
					// If this Duplicant is better than previous jester, update it
					if (minValue >= BestVarietyValue)
						BestVarietyValue = minValue;
				}
			}
		}

		/// <summary>
		/// Checks the colony summary to guess the date of the last possible death.
		/// </summary>
		private void InitGrimReaper() {
			// Look for the last dip in Duplicant count
			float lastValue = -1.0f;
			try {
				var data = RetireColonyUtility.GetCurrentColonyRetiredColonyData();
				RetiredColonyData.RetiredColonyStatistic[] stats;
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
				var gc = GameClock.Instance;
				PUtil.LogWarning("Unable to determine the last date of death:");
				PUtil.LogExcWarn(e);
				if (gc != null)
					LastDeath = gc.GetCycle();
			}
		}

		/// <summary>
		/// Checks to see if the item is a food item, or is a primary ingredient in a food
		/// recipe.
		/// </summary>
		/// <param name="cropId">The crop ID to check.</param>
		/// <returns>true if it is a food crop, or false otherwise.</returns>
		internal bool IsFoodCrop(string cropId) {
			bool food = !string.IsNullOrEmpty(cropId);
			if (food)
				food = foods.Contains(cropId);
			return food;
		}

		/// <summary>
		/// Called when a building is completed.
		/// </summary>
		private void OnBuildingCompleted(object _) {
			BuildingsBuilt++;
		}

		protected override void OnCleanUp() {
			var inst = Game.Instance;
			if (inst != null) {
				inst.Unsubscribe((int)GameHashes.NewBuilding);
				inst.Unsubscribe(DigNTiles.DigComplete);
			}
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
			var inst = Game.Instance;
			base.OnSpawn();
			if (BuildingsBuilt == 0)
				// Not yet initialized, fill with number of completed buildings
				BuildingsBuilt = Components.BuildingCompletes.Count;
			PlanetsVisited ??= new HashSet<int>();
			TriggerEvents ??= new Dictionary<string, bool>(64);
			BestAttributeValue ??= new Dictionary<string, float>(64);
			if (LastDeath <= 0)
				InitGrimReaper();
			var dbAttr = Db.Get().Attributes;
			VarietyAttributes = new[] { dbAttr.Art, dbAttr.Athletics,
				dbAttr.Botanist, dbAttr.Caring, dbAttr.Construction, dbAttr.Cooking,
				dbAttr.Digging, dbAttr.Learning, dbAttr.Machinery, dbAttr.Ranching,
				dbAttr.Strength };
			// Neutronium discovered?
			var neutronium = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
			if (neutronium != null && DiscoveredResources.Instance.IsDiscovered(neutronium.tag))
				Trigger(AchievementStrings.ISEEWHATYOUDIDTHERE.ID);
			if (DlcManager.IsExpansion1Active())
				// DLC STARMAP
				PlanetsRequired = ClusterManager.Instance.worldCount;
			else {
				// VANILLA STARMAP
				var si = SpacecraftManager.instance;
				if (si != null && si.destinations != null) {
					int count = 0;
					// Exclude unreachable destinations (earth) but include temporal tear
					foreach (var destination in si.destinations)
						if (destination.GetDestinationType()?.visitable == true)
							count++;
					if (count > 0)
						PlanetsRequired = count;
				}
			}
			if (inst != null) {
				inst.Subscribe((int)GameHashes.NewBuilding, OnBuildingCompleted);
				inst.Subscribe(DigNTiles.DigComplete, OnDigCompleted);
			}
			ScanLocavore();
		}

		/// <summary>
		/// Scans the map for invalid candidates for Locavore. Only runs once when the map is
		/// loaded.
		/// </summary>
		private void ScanLocavore() {
			var edibleList = EdiblesManager.GetAllLoadedFoodTypes();
			var rawEdibles = new HashSet<string>();
			// Go through recipes, Food Supply Tooltips style
			foods.Clear();
			foreach (var food in edibleList)
				rawEdibles.Add(food.Id);
			foods.UnionWith(rawEdibles);
			foreach (var recipe in RecipeManager.Get().recipes) {
				var ingred = recipe.Ingredients;
				if (ingred.Count == 1 && rawEdibles.Contains(recipe.Result.Name))
					foods.Add(ingred[0].tag.Name);
			}
			foreach (var recipe in ComplexRecipeManager.Get().recipes) {
				var ingred = recipe.ingredients;
				var results = recipe.results;
				int n = results.Length;
				for (int i = 0; i < n; i++)
					if (rawEdibles.Contains(results[i].material.Name)) {
						// Avoid problematic recipes like the smoker requiring wood, which
						// disallows all of the tree variants as "edibles"
						if (ingred.Length == 1)
							foods.Add(ingred[0].material.Name);
						break;
					}
			}
#if DEBUG
			PUtil.LogDebug("Detected foods: " + foods.Join(", "));
#endif
			rawEdibles.Clear();
			var plots = Components.PlantablePlots;
			foreach (var world in plots.GetWorldsIds()) {
				// This skips planets with no plots
				var inWorld = plots.GetItems(world);
				int n = inWorld.Count;
				for (int i = 0; i < n; i++) {
					var plant = inWorld[i].Occupant;
					if (plant != null && plant.TryGetComponent(out Crop crop) &&
							CheckLocavore(crop))
						break;
				}
			}
		}

		public void Sim1000ms(float dt) {
			CountArtifacts();
			FindJester();
			UpdateAttributes();
			// Mark visited worlds for DLC
			if (DlcManager.IsExpansion1Active()) {
				var wc = ClusterManager.Instance.WorldContainers;
				int n = wc.Count;
				for (int i = 0; i < n; i++) {
					var world = wc[i];
					if (world.IsDupeVisited)
						PlanetsVisited.Add(world.id);
				}
			}
		}

		/// <summary>
		/// Updates the best Duplicant in each attribute.
		/// </summary>
		private void UpdateAttributes() {
			var dupes = Components.LiveMinionIdentities.Items;
			// For each value requested, update the value if needed
			var keys = ListPool<string, AchievementStateComponent>.Allocate();
			keys.Clear();
			keys.AddRange(BestAttributeValue.Keys);
			int n = keys.Count, nd = dupes.Count;
			for (int i = 0; i < n; i++) {
				// Check each duplicant for the best value
				float best = 0.0f;
				var attribute = keys[i];
				var attr = Db.Get().Attributes.Get(attribute);
				for (int j = 0; j < nd; j++) {
					var duplicant = dupes[j];
					if (duplicant != null)
						best = Math.Max(best, attr.Lookup(duplicant).GetTotalValue());
				}
				BestAttributeValue[attribute] = best;
			}
			keys.Recycle();
		}
	}
}
