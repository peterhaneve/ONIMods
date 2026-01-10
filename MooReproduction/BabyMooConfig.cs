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

using UnityEngine;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// A baby Gassy Moo entity that uses a rescaled sprite from the regular Gassy Moo.
	/// </summary>
	[EntityConfigOrder(2)]
	public sealed class BabyMooConfig : IEntityConfig, IHasDlcRestrictions {
		// Baby Moos are only 1x1
		private const int HEIGHT = 1;

		public const string ID = "MooBaby";

		public static readonly Tag ID_TAG = ID.ToTag();

		private const int WIDTH = 1;

		internal static void SetupBabyMoo(GameObject prefab) {
			const float X = 0.5f * ((WIDTH + 1) % 2);
			// Resize to 1x1
			if (prefab.TryGetComponent(out KBoxCollider2D collider)) {
				collider.size = new Vector2f(WIDTH, HEIGHT);
				collider.offset = new Vector2f(X, HEIGHT * 0.5f);
			}
			if (prefab.TryGetComponent(out KBatchedAnimController kbac)) {
				kbac.Offset = new Vector3(X, 0f, 0f);
				kbac.animScale *= 0.5f;
			}
			var occupyArea = prefab.AddOrGet<OccupyArea>();
			if (occupyArea != null)
				occupyArea.SetCellOffsets(EntityTemplates.GenerateOffsets(WIDTH, HEIGHT));
			// Reduce to 1kg meat for baby
			if (prefab.TryGetComponent(out Butcherable butcherable))
				butcherable.SetDrops(new[] { MeatConfig.ID });
			MooReproductionPatches.UpdateMooChores(prefab, true);
			// Babies should not be ranchable or auto wrangled
			prefab.RemoveDef<RanchableMonitor.Def>();
			prefab.RemoveDef<FixedCapturableMonitor.Def>();
		}

		public GameObject CreatePrefab() {
			// Specifying is_baby = true does nothing but break the anims
			var prefab = MooConfig.CreateMoo(ID, MooReproductionStrings.CREATURES.
				SPECIES.MOO.BABY.NAME, MooReproductionStrings.CREATURES.SPECIES.MOO.BABY.DESC,
				"gassy_moo_kanim", MooTuning.BaseSongChances, false);
			EntityTemplates.ExtendEntityToBeingABaby(prefab, MooConfig.ID);
			SetupBabyMoo(prefab);
			return prefab;
		}

		public string[] GetDlcIds() {
			return null;
		}

		public string[] GetRequiredDlcIds() {
			return DlcManager.EXPANSION1;
		}

		public string[] GetForbiddenDlcIds() {
			return null;
		}

		public string[] GetAnyRequiredDlcIds() {
			return DlcManager.EXPANSION1;
		}

		public void OnPrefabInit(GameObject inst) {
		}

		public void OnSpawn(GameObject inst) {
			BaseMooConfig.OnSpawn(inst);
		}
	}
}
