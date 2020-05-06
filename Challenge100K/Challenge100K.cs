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

using Harmony;
using PeterHan.PLib;
using ProcGen;

namespace PeterHan.Challenge100K {
	/// <summary>
	/// Registers the required world gen information for the 100 K Challenge!
	/// </summary>
	public static class Challenge100K {
		/// <summary>
		/// The sprite to load for the asteroid selection.
		/// </summary>
		private const string SPRITE = "Asteroid_onehundredk";

		/// <summary>
		/// The "to 11" cold temperature for frigid biomes.
		/// </summary>
		private static Temperature to11 = null;

		/// <summary>
		/// Registers the strings used in this mod.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void InitStrings() {
			Strings.Add("STRINGS.WORLDS.ONEHUNDREDK.NAME", Challenge100KStrings.NAME);
			Strings.Add("STRINGS.WORLDS.ONEHUNDREDK.DESCRIPTION", Challenge100KStrings.
				DESCRIPTION);
			var sprite = PUtil.LoadSprite("PeterHan.Challenge100K." + SPRITE + ".png");
			if (sprite != null)
				Assets.Sprites.Add(SPRITE, sprite);
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			to11 = new Temperature();
			var tr11 = Traverse.Create(to11);
			tr11.SetProperty("min", 80.0f);
			tr11.SetProperty("max", 110.0f);
			PUtil.RegisterPatchClass(typeof(Challenge100K));
		}

		/// <summary>
		/// Applied to SettingsCache to create a custom 100K temperature range.
		/// </summary>
		[HarmonyPatch(typeof(SettingsCache), "LoadFiles")]
		public static class SettingsCache_LoadFiles_Patch {
			internal static void Postfix() {
				var frigid = (Temperature.Range)11;
				var temps = SettingsCache.temperatures;
				if (!temps.ContainsKey(frigid))
					SettingsCache.temperatures.Add(frigid, to11);
			}
		}
	}
}
