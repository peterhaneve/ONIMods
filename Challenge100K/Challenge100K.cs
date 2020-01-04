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
		/// The "to 11" cold temperature for frigid biomes.
		/// </summary>
		private static Temperature to11 = null;

		public static void OnLoad() {
			PUtil.InitLibrary();
			to11 = new Temperature();
			var tr11 = Traverse.Create(to11);
			tr11.SetProperty("min", 80.0f);
			tr11.SetProperty("max", 110.0f);
		}

		/// <summary>
		/// Applied to Db to load the strings for this world.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			public static LocString NAME = "100K Challenge";
			public static LocString DESCRIPTION = "One of the coldest worlds ever surveyed, this harsh and unforgiving asteroid features an average temperature of only 100 K (-173 C).\r\n\r\nSURVIVAL IN THIS WORLD WILL BE NEARLY IMPOSSIBLE, BUT A GLIMMER OF HOPE REMAINS. CAN YOU USE ALL THAT YOU HAVE LEARNED TO SURVIVE FOR 100 CYCLES?\r\n";

			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				Strings.Add("STRINGS.WORLDS.ONEHUNDREDK.NAME", NAME);
				Strings.Add("STRINGS.WORLDS.ONEHUNDREDK.DESCRIPTION", DESCRIPTION);
			}
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
