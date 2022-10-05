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

using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// A baby Gassy Moo entity that uses a rescaled sprite from the regular Gassy Moo.
	/// </summary>
	public sealed class BabyMooConfig : IEntityConfig {
		// Baby Moos are only 1x1
		private const int HEIGHT = 1;

		public const string ID = "MooBaby";

		public static readonly Tag ID_TAG = ID.ToTag();

		private const int WIDTH = 1;

		public GameObject CreatePrefab() {
			const float X = 0.5f * ((WIDTH + 1) % 2);
			var prefab = MooConfig.CreateMoo(ID, MooReproductionStrings.CREATURES.
				SPECIES.MOO.BABY.NAME, MooReproductionStrings.CREATURES.SPECIES.MOO.BABY.DESC,
				"gassy_moo_kanim", true);
			// Resize to 1x1
			var collider = prefab.GetComponent<KBoxCollider2D>();
			collider.size = new Vector2f(WIDTH, HEIGHT);
			collider.offset = new Vector2f(X, HEIGHT * 0.5f);
			var kbac = prefab.GetComponent<KBatchedAnimController>();
			kbac.Offset = new Vector3(X, 0f, 0f);
			kbac.animScale *= 0.5f;
			var occupyArea = prefab.AddOrGet<OccupyArea>();
			occupyArea.OccupiedCellsOffsets = EntityTemplates.GenerateOffsets(WIDTH, HEIGHT);
			EntityTemplates.ExtendEntityToBeingABaby(prefab, MooConfig.ID);
			// Reduce to 1kg meat for baby
			var butcherable = prefab.GetComponent<Butcherable>();
			if (butcherable != null)
				butcherable.SetDrops(new[] { MeatConfig.ID });
			MooReproductionPatches.UpdateMooChores(prefab, true);
			// Babies should not be ranchable or auto wrangled, but there is no RemoveDef
			// function...
			var smc = prefab.GetComponent<StateMachineController>();
			if (smc != null && smc.defHandle.IsValid()) {
				var defs = smc.cmpdef?.defs;
				if (defs != null) {
					defs.Remove(smc.GetDef<RanchableMonitor.Def>());
					defs.Remove(smc.GetDef<FixedCapturableMonitor.Def>());
				}
			}
			return prefab;
		}

		public string[] GetDlcIds() {
			return DlcManager.AVAILABLE_EXPANSION1_ONLY;
		}

		public void OnPrefabInit(GameObject inst) {
		}

		public void OnSpawn(GameObject inst) {
			BaseMooConfig.OnSpawn(inst);
		}
	}
}
