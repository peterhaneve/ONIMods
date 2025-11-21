/*
 * Copyright 2025 Peter Han
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
	/// A baby Husky Moo entity that uses a rescaled sprite from the regular Diesel Moo.
	/// </summary>
	[EntityConfigOrder(2)]
	public sealed class BabyDieselMooConfig : IEntityConfig, IHasDlcRestrictions {
		public const string ID = "DieselMooBaby";

		public static readonly Tag ID_TAG = ID.ToTag();

		public GameObject CreatePrefab() {
			var prefab = DieselMooConfig.CreateMoo(ID, MooReproductionStrings.CREATURES.
				SPECIES.DIESELMOO.BABY.NAME, MooReproductionStrings.CREATURES.SPECIES.
				DIESELMOO.BABY.DESC, "gassy_moo_kanim", MooTuning.DieselSongChances, false);
			EntityTemplates.ExtendEntityToBeingABaby(prefab, DieselMooConfig.ID);
			BabyMooConfig.SetupBabyMoo(prefab);
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
