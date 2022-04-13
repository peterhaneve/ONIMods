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

using HarmonyLib;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A much, much faster radbolt updater.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class FastProtonCollider : KMonoBehaviour {
		/// <summary>
		/// The tag bits with which to collide.
		/// </summary>
		private static TagBits COLLIDE_WITH;

		/// <summary>
		/// The tag bits that will prevent collision.
		/// </summary>
		private static TagBits COLLIDE_WITHOUT;

		/// <summary>
		/// Could not find a const for this in game...
		/// </summary>
		public const float DAMAGE_ON_HIT = 20.0f;

		/// <summary>
		/// Or this one...
		/// </summary>
		public const int DISEASE_PER_CELL = 5;

		/// <summary>
		/// The scene partitioner layer name to use.
		/// </summary>
		internal const string RADBOLTS = nameof(HighEnergyParticle);

		/// <summary>
		/// The layer used for colliding ~~beams~~ radbolts.
		/// </summary>
		internal static ScenePartitionerLayer hepLayer;

		/// <summary>
		/// Initializes the tag masks used for collision.
		/// </summary>
		internal static void Init() {
			COLLIDE_WITH = new TagBits();
			COLLIDE_WITH.SetTag(GameTags.Creature);
			COLLIDE_WITH.SetTag(GameTags.Minion);
			COLLIDE_WITHOUT = new TagBits();
			COLLIDE_WITHOUT.SetTag(GameTags.Dead);
			COLLIDE_WITHOUT.SetTag(GameTags.Dying);
		}

#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private HighEnergyParticle hep;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// The last cell that this radbolt occupied when collision was checked.
		/// </summary>
		private int cachedCell;

		/// <summary>
		/// The disease to leave behind on travel and to spawn on hit.
		/// </summary>
		private byte diseaseIndex;

		/// <summary>
		/// The partitioner entry into the radbolt collision layer.
		/// </summary>
		private HandleVector<int>.Handle partitionerEntry;

		/// <summary>
		/// Tracks the radbolt's travel distance.
		/// </summary>
		private ColonyAchievementTracker tracker;

		internal FastProtonCollider() {
			cachedCell = Grid.InvalidCell;
			partitionerEntry = HandleVector<int>.InvalidHandle;
		}

		/// <summary>
		/// Checks for HEP collision with a building.
		/// </summary>
		/// <param name="cell">The current cell that this radbolt occupies.</param>
		/// <returns>Whether this radbolt was captured by a building with a radbolt port.</returns>
		private bool CheckBuildingCollision(int cell) {
			// Check for collision with a building
			var port = Grid.Objects[cell, (int)ObjectLayer.Building].
				GetComponentSafe<HighEnergyParticlePort>();
			bool captured = false;
			if (port != null && port.GetHighEnergyParticleInputPortPosition() == cell) {
				// Not exactly on center but good enough and WAY cheaper
				if (port.InputActive() && port.AllowCapture(hep)) {
					hep.Capture(port);
					captured = true;
				} else
					hep.Collide(HighEnergyParticle.CollisionType.PassThrough);
			}
			return captured;
		}

		/// <summary>
		/// Checks for HEP collision with other objects.
		/// </summary>
		/// <param name="cell">The current cell that this radbolt occupies.</param>
		/// <param name="pos">The exact position of this radbolt.</param>
		private void CheckCollision(int cell, Vector3 pos) {
			// If the cell is the same, do not check building or tile collision again
			if ((cell == cachedCell || (!CheckBuildingCollision(cell) &&
					!CheckSolidTileCollision(cell))) && !CheckRadboltCollision(cell, pos))
				CheckLivingCollision(cell);
			cachedCell = cell;
		}

		/// <summary>
		/// Checks for radbolt collision with a creature or Duplicant.
		/// </summary>
		/// <param name="cell">The current cell that this radbolt occupies.</param>
		private void CheckLivingCollision(int cell) {
			var entries = Grid.Objects[cell, (int)ObjectLayer.Pickupables].
				GetComponentSafe<Pickupable>()?.objectLayerListItem;
			Health hp;
			while (entries != null) {
				var item = entries.gameObject;
				var prefabID = item.GetComponentSafe<KPrefabID>();
				// Is it a creature/Duplicant and not already dead?
				if (prefabID != null && prefabID.HasAnyTags(ref COLLIDE_WITH) && !prefabID.
						HasAnyTags_AssumeLaundered(ref COLLIDE_WITHOUT) && (hp = item.
						GetComponent<Health>()) != null && !hp.IsDefeated()) {
					hp.Damage(DAMAGE_ON_HIT);
					if (prefabID.HasTag(GameTags.Minion)) {
						var smi = item.GetSMI<WoundMonitor.Instance>();
						// If the hit was not a KO hit
						if (smi != null && !hp.IsDefeated())
							smi.PlayKnockedOverImpactAnimation();
						// Add germs to them
						item.GetComponent<PrimaryElement>().AddDisease(diseaseIndex, Mathf.
							FloorToInt(hep.payload * 50.0f), "HEPImpact");
						hep.Collide(HighEnergyParticle.CollisionType.Minion);
					} else
						hep.Collide(HighEnergyParticle.CollisionType.Creature);
					break;
				}
				entries = entries.nextItem;
			}
		}

		/// <summary>
		/// Checks for radbolt collision with another one.
		/// </summary>
		/// <param name="cell">The current cell that this radbolt occupies.</param>
		/// <param name="pos">The exact position of this radbolt.</param>
		/// <returns>Whether the radbolt collided with another radbolt.</returns>
		private bool CheckRadboltCollision(int cell, Vector3 pos) {
			var hits = ListPool<ScenePartitionerEntry, FastProtonCollider>.Allocate();
			Grid.CellToXY(cell, out int x, out int y);
			GameScenePartitioner.Instance.GatherEntries(x - 1, y - 1, 3, 3, hepLayer, hits);
			int n = hits.Count;
			bool collided = false;
			for (int i = 0; i < n && !collided; i++)
				if (hits[i].obj is HighEnergyParticle otherHEP && otherHEP != null &&
						otherHEP != hep && otherHEP.isCollideable && CollidesWith(pos,
						otherHEP.transform.position)) {
					hep.payload += otherHEP.payload;
					otherHEP.DestroyNow();
					hep.Collide(HighEnergyParticle.CollisionType.HighEnergyParticle);
					collided = true;
				}
			hits.Recycle();
			return collided;
		}

		/// <summary>
		/// Checks for radbolt collision with a solid tile.
		/// </summary>
		/// <param name="cell">The current cell that this radbolt occupies.</param>
		/// <returns>Whether this radbolt collided with the tile.</returns>
		private bool CheckSolidTileCollision(int cell) {
			bool collided = false;
			if (Grid.IsSolidCell(cell)) {
				var tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
				var capturer = hep.capturedBy;
				collided = tile == null || !tile.HasTag(GameTags.HEPPassThrough) ||
					capturer == null || capturer.gameObject != tile;
				if (collided)
					hep.Collide(HighEnergyParticle.CollisionType.Solid);
			}
			return collided;
		}

		/// <summary>
		/// Checks to see if this radbolt is colliding with another object.
		/// </summary>
		/// <param name="pos">The position of this radbolt.</param>
		/// <param name="other">The location of the other object.</param>
		/// <returns>true if they are colliding, or false otherwise.</returns>
		private bool CollidesWith(Vector3 pos, Vector3 other) {
			Vector2 difference = pos - other;
			return difference.sqrMagnitude <= HighEnergyParticleConfig.
				PARTICLE_COLLISION_SIZE * HighEnergyParticleConfig.PARTICLE_COLLISION_SIZE;
		}

		/// <summary>
		/// Creates the scene partitioner entry for this collider.
		/// </summary>
		/// <param name="cell">The current cell of this radbolt.</param>
		private void CreatePartitioner(int cell) {
			var gsp = GameScenePartitioner.Instance;
			if (gsp != null && hepLayer != null)
				partitionerEntry = gsp.Add(nameof(HighEnergyParticle), hep, cell, hepLayer,
					null);
		}

		/// <summary>
		/// Destroys the scene partitioner entry for this collider.
		/// </summary>
		private void DestroyPartitioner() {
			if (partitionerEntry.IsValid())
				GameScenePartitioner.Instance.Free(ref partitionerEntry);
		}

		/// <summary>
		/// Updates the radbolt position and checks collision.
		/// </summary>
		/// <param name="dt">The time elapsed in seconds since the last update.</param>
		public void MovingUpdate(float dt) {
			var tt = transform;
			if (hep.collision == HighEnergyParticle.CollisionType.None && dt > 0.0f) {
				Vector3 pos = tt.position, newPos = pos + EightDirectionUtil.GetNormal(
					hep.direction) * hep.speed * dt;
				int cell = Grid.PosToCell(pos), newCell = Grid.PosToCell(newPos);
				bool destroy = false;
				if (tracker != null)
					tracker.radBoltTravelDistance += hep.speed * dt;
				if (!FastTrackOptions.Instance.DisableSound)
					hep.loopingSounds.UpdateVelocity(hep.flyingSound, newPos - pos);
				if (!Grid.IsValidCell(newCell)) {
					PUtil.LogWarning("High energy particle moved into invalid cell {0:D}".F(
						newCell));
					destroy = true;
				} else {
					float payload = hep.payload;
					if (cell != newCell) {
						SimMessages.ModifyDiseaseOnCell(newCell, diseaseIndex,
							DISEASE_PER_CELL);
						payload -= HighEnergyParticleConfig.PER_CELL_FALLOFF;
						if (partitionerEntry.IsValid())
							// GSP had to be valid for the entry to exist in the first place
							GameScenePartitioner.Instance.UpdatePosition(partitionerEntry,
								newCell);
						else
							CreatePartitioner(newCell);
					}
					if (payload <= 0.0f)
						destroy = true;
					else {
						hep.payload = payload;
						// Use the Klei override to trigger kanim cell change update
						tt.SetPosition(newPos);
						CheckCollision(cell, pos);
					}
				}
				if (destroy)
					hep.smi.sm.destroySimpleSignal.Trigger(hep.smi);
			}
		}

		public override void OnCleanUp() {
			base.OnCleanUp();
			DestroyPartitioner();
		}

		public override void OnSpawn() {
			base.OnSpawn();
			var diseases = Db.Get().Diseases;
			diseaseIndex = diseases.GetIndex(diseases.RadiationPoisoning.Id);
			if (FastTrackOptions.Instance.DisableAchievements == FastTrackOptions.
					AchievementDisable.Always || AchievementDisablePatches.TrackAchievements())
				tracker = SaveGame.Instance.GetComponent<ColonyAchievementTracker>();
			else
				tracker = null;
			CreatePartitioner(Grid.PosToCell(this));
		}
	}

	/// <summary>
	/// Applied to HighEnergyParticle to turn off its collision checking for our own.
	/// </summary>
	[HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.CheckCollision))]
	public static class HighEnergyParticle_CheckCollision_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied before CheckCollision runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to HighEnergyParticle to replace its move method with our own.
	/// </summary>
	[HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.MovingUpdate))]
	public static class HighEnergyParticle_MovingUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied before MovingUpdate runs.
		/// </summary>
		internal static bool Prefix(HighEnergyParticle __instance, float dt) {
			var fpc = __instance.GetComponent<FastProtonCollider>();
			bool cont = fpc == null;
			if (!cont)
				fpc.MovingUpdate(dt);
			else
				// Should never happen!
				__instance.CheckCollision();
			return cont;
		}
	}

	/// <summary>
	/// Applied to HighEnergyParticleConfig to add this component to it on creation.
	/// 
	/// Unfortunately the stock circle collider needs to be left on so it is selectable by
	/// the hover cards tool.
	/// </summary>
	[HarmonyPatch(typeof(HighEnergyParticleConfig), nameof(HighEnergyParticleConfig.
		CreatePrefab))]
	public static class HighEnergyParticleConfig_CreatePrefab_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RadiationOpts;

		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void Postfix(ref GameObject __result) {
			__result.AddOrGet<FastProtonCollider>();
		}
	}
}
