/*
 * Copyright 2024 Peter Han
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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using ProcGen;
using ProcGenGame;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.Challenge100K {
	/// <summary>
	/// Registers the required world gen information for the 100 K Challenge!
	/// </summary>
	public sealed class Challenge100K : KMod.UserMod2 {
		/// <summary>
		/// The enum value used for 100K subworlds.
		/// </summary>
		public const Temperature.Range ONE_HUNDRED_K = (Temperature.Range)12;

		/// <summary>
		/// The sprite to load for the asteroid selection.
		/// </summary>
		private const string SPRITE = "Asteroid_onehundredk";

		/// <summary>
		/// The world names used for all 100K moonlets.
		/// </summary>
		private static readonly ISet<string> WORLD_NAMES = new HashSet<string>() {
			"STRINGS.WORLDS.ONEHUNDREDK.NAME"
		};

		/// <summary>
		/// The "to 11" cold temperature for frigid biomes.
		/// 
		/// Changed to 12 to avoid a clash with I Love Slicksters.
		/// </summary>
		private static Temperature to11 = null;

		/// <summary>
		/// Retrieves the "minimum" temperature of an element on stock worlds. However, on
		/// 100 K, returns 1 K to disable the check.
		/// </summary>
		/// <param name="element">The element to look up.</param>
		/// <param name="worldGen">The currently generating world.</param>
		/// <returns>The minimum temperature to be used for world gen.</returns>
		private static float GetMinTemperature(Element element, WorldGen worldGen) {
			var world = worldGen?.Settings?.world;
			return (world != null && WORLD_NAMES.Contains(world.name)) ? 1.0f : element.lowTemp;
		}

		/// <summary>
		/// Registers the sprites used in this mod.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void InitSprites() {
			var sprite = PUIUtils.LoadSprite("PeterHan.Challenge100K." + SPRITE + ".png");
			if (sprite != null)
				Assets.Sprites.Add(SPRITE, sprite);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			to11 = new Temperature();
			typeof(Temperature).GetPropertySafe<float>(nameof(Temperature.min), false)?.
				SetValue(to11, 80.0f);
			typeof(Temperature).GetPropertySafe<float>(nameof(Temperature.max), false)?.
				SetValue(to11, 110.0f);
			LocString.CreateLocStringKeys(typeof(Challenge100KStrings.CLUSTER_NAMES));
			LocString.CreateLocStringKeys(typeof(Challenge100KStrings.WORLDS));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			new PLocalization().Register();
			new POptions().RegisterOptions(this, typeof(Challenge100KOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(Challenge100K));
		}

		/// <summary>
		/// Applied to SettingsCache to create a custom 100K temperature range.
		/// </summary>
		[HarmonyPatch(typeof(SettingsCache), nameof(SettingsCache.LoadFiles),
			typeof(List<Klei.YamlIO.Error>))]
		public static class SettingsCache_LoadFiles_Patch {
			/// <summary>
			/// Applied after LoadFiles runs.
			/// </summary>
			internal static void Postfix() {
				var temps = SettingsCache.temperatures;
				if (!temps.ContainsKey(ONE_HUNDRED_K))
					SettingsCache.temperatures.Add(ONE_HUNDRED_K, to11);
			}
		}

#if false
		/// <summary>
		/// Applied to MutatedWorldData() to remove all geysers on hard mode on 100 K.
		/// </summary>
		[HarmonyPatch(typeof(MutatedWorldData), MethodType.Constructor, typeof(ProcGen.World),
			typeof(List<WorldTrait>), typeof(List<WorldTrait>))]
		public static class MutatedWorldData_Constructor_Patch {
			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(MutatedWorldData __instance) {
				var world = __instance.world;
				var subworlds = __instance.subworlds;
				if (world.name == WORLD_NAME) {
					var options = POptions.ReadSettings<Challenge100KOptions>();
					if (options != null && options.RemoveGeysers) {
#if DEBUG
						PUtil.LogDebug("Hard mode: removing geysers");
#endif
						world.worldTemplateRules?.Clear();
						// Remove the POI geysers too
						if (subworlds != null)
							foreach (var subworld in subworlds)
								subworld.Value.subworldTemplateRules?.Clear();
					}
				}
			}
		}
#endif

		/// <summary>
		/// Applied to TerrainCell to prevent element temperature clamping to the minimum
		/// temperature of the current phase (which leads to unwanted things like hot magma
		/// channels).
		/// </summary>
		[HarmonyPatch(typeof(TerrainCell), "ApplyBackground")]
		public static class TerrainCell_ApplyBackground_Patch {
			/// <summary>
			/// Transpiles ApplyBackground to swap out Element.lowTemp with our method.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var target = typeof(Element).GetFieldSafe(nameof(Element.lowTemp), false);
				var replacement = typeof(Challenge100K).GetMethodSafe(nameof(
					GetMinTemperature), true, typeof(Element), typeof(WorldGen));
				foreach (var instruction in method) {
					if (instruction.opcode == OpCodes.Ldfld && target != null && target ==
							(FieldInfo)instruction.operand) {
						// With the Element on the stack, push the WorldGen (first arg)
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						// Replacement for "Element.lowTemp"
						instruction.opcode = OpCodes.Call;
						instruction.operand = replacement;
					}
					yield return instruction;
				}
			}
		}

		/// <summary>
		/// Applied to TerrainCell to "fix" the temperature range of Volcanoes, Magma Channels,
		/// Buried Oil, Subsurface Ocean, and Irregular Oil to 100 K.
		/// </summary>
		[HarmonyPatch(typeof(TerrainCell), "GetTemperatureRange", typeof(WorldGen))]
		public static class TerrainCell_GetTemperatureRange_Patch {
			/// <summary>
			/// Applied after GetTemperatureRange runs.
			/// </summary>
			internal static void Postfix(WorldGen worldGen, ref Temperature.Range __result) {
				var world = worldGen.Settings?.world;
				var temp = __result;
				if (world != null && WORLD_NAMES.Contains(world.name) && temp > Temperature.
						Range.VeryCold && temp <= Temperature.Range.ExtremelyHot) {
					// Override temp
#if DEBUG
					PUtil.LogDebug("Found subworld with temp {0}, overriding to 100K".F(temp));
#endif
					__result = ONE_HUNDRED_K;
				}
			}
		}

#if DEBUG
		/// <summary>
		/// Reports world generation errors in debug builds.
		/// </summary>
		[HarmonyPatch(typeof(WorldGen), "ReportWorldGenError")]
		public static class WorldGen_ReportWorldGenError_Patch {
			internal static void Postfix(System.Exception e) {
				PUtil.LogExcWarn(e);
			}
		}
#endif
	}
}
