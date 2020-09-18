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
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using ProcGen;
using System.Collections.Generic;

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
		/// The world name string key.
		/// </summary>
		private const string WORLD_NAME = "STRINGS.WORLDS.ONEHUNDREDK.NAME";

		/// <summary>
		/// The "to 11" cold temperature for frigid biomes.
		/// 
		/// Changed to 12 to avoid a clash with I Love Slicksters.
		/// </summary>
		private static Temperature to11 = null;

		/// <summary>
		/// Registers the strings used in this mod.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void InitStrings() {
			Strings.Add(WORLD_NAME, Challenge100KStrings.NAME);
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
			PLocalization.Register();
			POptions.RegisterOptions(typeof(Challenge100KOptions));
			PUtil.RegisterPatchClass(typeof(Challenge100K));
		}

		/// <summary>
		/// Applied to SettingsCache to create a custom 100K temperature range.
		/// </summary>
		[HarmonyPatch(typeof(SettingsCache), nameof(SettingsCache.LoadFiles))]
		public static class SettingsCache_LoadFiles_Patch {
			/// <summary>
			/// Applied after LoadFiles runs.
			/// </summary>
			internal static void Postfix() {
				var frigid = (Temperature.Range)12;
				var temps = SettingsCache.temperatures;
				if (!temps.ContainsKey(frigid))
					SettingsCache.temperatures.Add(frigid, to11);
			}
		}

		/// <summary>
		/// Applied to MutatedWorldData() to remove all geysers on hard mode on 100 K.
		/// </summary>
		[HarmonyPatch(typeof(MutatedWorldData), MethodType.Constructor, typeof(ProcGen.World),
			typeof(List<WorldTrait>))]
		public static class MutatedWorldData_Constructor_Patch {
			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(MutatedWorldData __instance) {
				var world = __instance.world;
				var subworlds = __instance.subworlds;
				if (world.name == WORLD_NAME) {
					var options = POptions.ReadSettingsForAssembly<Challenge100KOptions>();
					if (options != null && options.RemoveGeysers) {
#if DEBUG
						PUtil.LogDebug("Hard mode: removing geysers");
#endif
						world.globalFeatureTemplates?.Clear();
						// Remove the POI geysers too
						if (subworlds != null)
							foreach (var subworld in subworlds)
								subworld.Value.pointsOfInterest?.Clear();
					}
				}
			}
		}
	}
}
