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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Updates the starmap sections of the default "simple" info screen.
	/// </summary>
	internal sealed partial class SimpleInfoScreenWrapper {
		public const string NO_GEYSERS = "NoGeysers";

		private static readonly Tag UNKNOWN_GEYSERS = new Tag(GeyserGenericConfig.ID);

		/// <summary>
		/// Displays the biomes present on the given planetoid.
		/// </summary>
		/// <param name="biomes">The biomes found on this planetoid.</param>
		/// <param name="biomeRows">The cached list of all biomes seen so far.</param>
		internal void AddBiomes(IList<string> biomes,
				IDictionary<Tag, GameObject> biomeRows) {
			var parent = sis.worldBiomesPanel.Content.gameObject;
			int n = biomes.Count;
			var toDisable = HashSetPool<Tag, SimpleInfoScreen>.Allocate();
			foreach (var pair in biomeRows)
				if (pair.Value.activeSelf)
					toDisable.Add(pair.Key);
			for (int i = 0; i < n; i++) {
				string id = biomes[i];
				var idTag = new Tag(id);
				if (biomeRows.TryGetValue(idTag, out var row))
					toDisable.Remove(idTag);
				else {
					row = Util.KInstantiateUI(sis.bigIconLabelRow, parent, true);
					if (row.TryGetComponent(out HierarchyReferences hr)) {
						string upperID = StringFormatter.ToUpper(id);
						hr.GetReference<Image>("Icon").sprite = GameUtil.GetBiomeSprite(id);
						// These are forgivable as they only run once
						hr.GetReference<LocText>("NameLabel").SetText(STRINGS.UI.
							FormatAsLink(Strings.Get("STRINGS.SUBWORLDS." + upperID +
							".NAME"), "BIOME" + upperID));
						hr.GetReference<LocText>("DescriptionLabel").SetText(Strings.Get(
							"STRINGS.SUBWORLDS." + upperID + ".DESC"));
					}
					biomeRows[id] = row;
				}
				row.SetActive(true);
			}
			// Only turn off that which needs to be turned off
			foreach (var id in toDisable)
				if (biomeRows.TryGetValue(id, out var row))
					row.SetActive(false);
			toDisable.Recycle();
		}

		/// <summary>
		/// Displays a geyser present on the given planetoid.
		/// </summary>
		/// <param name="geyserTag">The geyser tag to add.</param>
		/// <param name="parent">The parent of new geyser objects.</param>
		/// <param name="geyserName">The text to display for the geyser name.</param>
		/// <param name="geyserRows">The cached list of all geysers seen so far.</param>
		/// <returns>The label that can display that geyser.</returns>
		internal GameObject AddGeyser(Tag geyserTag, GameObject parent, string geyserName,
				IDictionary<Tag, GameObject> geyserRows) {
			HierarchyReferences hr;
			if (!geyserRows.TryGetValue(geyserTag, out var row)) {
				row = Util.KInstantiateUI(sis.iconLabelRow, parent, true);
				geyserRows.Add(geyserTag, row);
				if (row.TryGetComponent(out hr)) {
					string tagStr = geyserTag.name, text = geyserName;
					var icon = hr.GetReference<Image>("Icon");
					hr.GetReference<LocText>("ValueLabel").gameObject.SetActive(false);
					if (tagStr == NO_GEYSERS)
						icon.sprite = Assets.GetSprite("icon_action_cancel");
					else {
						var uiSprite = Def.GetUISprite(geyserTag);
						icon.sprite = uiSprite.first;
						icon.color = uiSprite.second;
					}
					if (string.IsNullOrEmpty(geyserName))
						text = Assets.GetPrefab(geyserTag).GetProperName();
					hr.GetReference<LocText>("NameLabel").SetText(text);
				}
			} else if (!string.IsNullOrEmpty(geyserName) && row.TryGetComponent(out hr))
				hr.GetReference<LocText>("NameLabel").SetText(geyserName);
			row.SetActive(true);
			return row;
		}

		/// <summary>
		/// Displays all geysers present on the given planetoid.
		/// </summary>
		/// <param name="id">The world ID to filter.</param>
		private void AddGeysers(int id) {
			var geyserRows = sis.geyserRows;
			var parent = sis.worldGeysersPanel.Content.gameObject;
			var spawnables = SaveGame.Instance.worldGenSpawner.spawnables;
			byte worldIndex;
			var knownGeysers = ListPool<Tag, SimpleInfoScreen>.Allocate();
			// Add all spawned geysers
			int n = allGeysers.Length, unknownGeysers = 0;
			for (int i = 0; i < n; i++) {
				var geyser = allGeysers[i];
				var go = geyser.gameObject;
				if (go.GetMyWorldId() == id)
					knownGeysers.Add(go.PrefabID());
			}
			// All unknown geysers and oil wells
			n = spawnables.Count;
			for (int i = 0; i < n; i++) {
				var candidate = spawnables[i];
				int cell = candidate.cell;
				if (Grid.IsValidCell(cell) && !candidate.isSpawned && (worldIndex = Grid.
						WorldIdx[cell]) != ClusterManager.INVALID_WORLD_IDX &&
						worldIndex == id) {
					string prefabID = candidate.spawnInfo.id;
					Tag prefabTag = new Tag(prefabID);
					if (prefabID == OilWellConfig.ID)
						knownGeysers.Add(prefabTag);
					else if (prefabID == GeyserGenericConfig.ID)
						unknownGeysers++;
					else {
						var prefab = Assets.GetPrefab(prefabTag);
						if (prefab != null && prefab.TryGetComponent(out Geyser _))
							knownGeysers.Add(prefabTag);
					}
				}
			}
			int totalGeysers = knownGeysers.Count;
			foreach (var pair in geyserRows)
				pair.Value.SetActive(false);
			for (int i = 0; i < totalGeysers; i++)
				AddGeyser(knownGeysers[i], parent, null, geyserRows);
			if (unknownGeysers > 0)
				AddGeyser(UNKNOWN_GEYSERS, parent, STRINGS.UI.DETAILTABS.SIMPLEINFO.
					UNKNOWN_GEYSERS.Replace("{num}", unknownGeysers.ToString()), geyserRows);
			if (totalGeysers == 0)
				AddGeyser(new Tag(NO_GEYSERS), parent, STRINGS.UI.DETAILTABS.SIMPLEINFO.
					NO_GEYSERS, geyserRows);
			knownGeysers.Recycle();
		}

		/// <summary>
		/// Displays the given planetoid's surface sunlight and radiation.
		/// </summary>
		/// <param name="world">The world to be displayed.</param>
		internal void AddSurfaceConditions(WorldContainer world) {
			var surfaceConditionRows = sis.surfaceConditionRows;
			var parent = sis.worldTraitsPanel.Content.gameObject;
			int n = surfaceConditionRows.Count;
			bool isNew = n < 2;
			for (int i = n; i < 2; i++)
				surfaceConditionRows.Add(Util.KInstantiateUI(sis.iconLabelRow, parent,
					true));
			var lights = surfaceConditionRows[0];
			var rads = surfaceConditionRows[1];
			if (lights.TryGetComponent(out HierarchyReferences hr)) {
				var valueLabel = hr.GetReference<LocText>("ValueLabel");
				if (isNew) {
					hr.GetReference<Image>("Icon").sprite = Assets.GetSprite("overlay_lights");
					hr.GetReference<LocText>("NameLabel").SetText(STRINGS.UI.CLUSTERMAP.
						ASTEROIDS.SURFACE_CONDITIONS.LIGHT);
					valueLabel.alignment = TMPro.TextAlignmentOptions.MidlineRight;
				}
				valueLabel.SetText(GameUtil.GetFormattedLux(world.SunlightFixedTraits[world.
					sunlightFixedTrait]));
			}
			lights.transform.SetAsLastSibling();
			if (rads.TryGetComponent(out hr)) {
				var valueLabel = hr.GetReference<LocText>("ValueLabel");
				if (isNew) {
					hr.GetReference<Image>("Icon").sprite = Assets.GetSprite(
						"overlay_radiation");
					hr.GetReference<LocText>("NameLabel").SetText(STRINGS.UI.CLUSTERMAP.
						ASTEROIDS.SURFACE_CONDITIONS.RADIATION);
					valueLabel.alignment = TMPro.TextAlignmentOptions.MidlineRight;
				}
				valueLabel.SetText(GameUtil.GetFormattedRads(world.CosmicRadiationFixedTraits[
					world.cosmicRadiationFixedTrait]));
			}
			rads.transform.SetAsLastSibling();
		}

		/// <summary>
		/// Displays the given planetoid's world traits.
		/// </summary>
		/// <param name="traitIDs">The trait IDs present on the selected planetoid.</param>
		internal void AddWorldTraits(IList<string> traitIDs) {
			var worldTraitRows = sis.worldTraitRows;
			int n = traitIDs.Count, existing = worldTraitRows.Count;
			for (int i = 0; i < n; i++) {
				string id = traitIDs[i];
				bool isNew = i >= existing;
				var cachedTrait = ProcGen.SettingsCache.GetCachedTrait(id, false);
				if (isNew)
					sis.CreateWorldTraitRow();
				var traitRow = worldTraitRows[i];
				if (traitRow.TryGetComponent(out HierarchyReferences hr)) {
					var refIcon = hr.GetReference<Image>("Icon");
					Sprite sprite;
					string traitName, tooltip;
					if (cachedTrait != null) {
						string path = cachedTrait.filePath;
						sprite = Assets.GetSprite(path.Substring(path.LastIndexOf('/') + 1));
						if (sprite == null)
							sprite = Assets.GetSprite("unknown");
						refIcon.color = Util.ColorFromHex(cachedTrait.colorHex);
						traitName = Strings.Get(cachedTrait.name);
						tooltip = Strings.Get(cachedTrait.description);
					} else {
						sprite = Assets.GetSprite("NoTraits");
						refIcon.color = Color.white;
						traitName = STRINGS.WORLD_TRAITS.MISSING_TRAIT;
						tooltip = "";
					}
					refIcon.gameObject.SetActive(true);
					traitRow.AddOrGet<ToolTip>().SetSimpleTooltip(tooltip);
					hr.GetReference<LocText>("NameLabel").SetText(traitName);
					refIcon.sprite = sprite;
					traitRow.SetActive(true);
				}
			}
			existing = worldTraitRows.Count;
			for (int i = n; i < existing; i++)
				worldTraitRows[i].SetActive(false);
			if (n == 0) {
				// No Traits row
				bool isNew = existing < 1;
				if (isNew)
					sis.CreateWorldTraitRow();
				var noTraitRow = worldTraitRows[0];
				if (isNew && noTraitRow.TryGetComponent(out HierarchyReferences hr)) {
					var refIcon = hr.GetReference<Image>("Icon");
					refIcon.gameObject.SetActive(true);
					refIcon.sprite = Assets.GetSprite("NoTraits");
					refIcon.color = Color.black;
					hr.GetReference<LocText>("NameLabel").SetText(STRINGS.WORLD_TRAITS.
						NO_TRAITS.NAME_SHORTHAND);
					noTraitRow.AddOrGet<ToolTip>().SetSimpleTooltip(STRINGS.WORLD_TRAITS.
						NO_TRAITS.DESCRIPTION);
				}
				noTraitRow.SetActive(true);
			}
		}

		/// <summary>
		/// Refreshes the asteroid description side screen.
		/// </summary>
		private void RefreshWorld() {
			var world = lastSelection.world;
			bool isPlanetoid = world != null && lastSelection.isAsteroid;
			sis.worldTraitsPanel.gameObject.SetActive(isPlanetoid);
			sis.worldGeysersPanel.gameObject.SetActive(isPlanetoid);
			if (isPlanetoid) {
				var biomes = world.Biomes;
				var biomeRows = sis.biomeRows;
				var traitIDs = world.WorldTraitIds;
				if (biomes == null)
					foreach (var pair in biomeRows)
						pair.Value.SetActive(false);
				else
					AddBiomes(biomes, biomeRows);
				sis.worldBiomesPanel.gameObject.SetActive(biomes != null);
				if (allGeysers != null)
					AddGeysers(world.id);
				if (traitIDs != null)
					AddWorldTraits(traitIDs);
				AddSurfaceConditions(world);
			} else
				sis.worldBiomesPanel.gameObject.SetActive(false);
		}
	}
}
