/*
 * Copyright 2019 Peter Han
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
using PeterHan.PLib.Options;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Patches which will be applied via annotations for Claustrophobia.
	/// </summary>
	public static class ClaustrophobiaPatches {
		public static void OnLoad() {
			PUtil.LogModInit();
			POptions.RegisterOptions(typeof(ClaustrophobiaOptions));
		}

		/// <summary>
		/// Handles localization by registering for translation.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			/// <summary>
			/// Applied before Initialize runs.
			/// </summary>
			internal static void Prefix() {
#if DEBUG
				ModUtil.RegisterForTranslation(typeof(ClaustrophobiaStrings));
#else
				Localization.RegisterForTranslation(typeof(ClaustrophobiaStrings));
#endif
			}
		}

		/// <summary>
		/// Applied to Game to register a component managing confined/trapped notifications.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			/// <param name="__instance">The current game.</param>
			internal static void Postfix(Game __instance) {
				__instance.gameObject.AddComponent<ClaustrophobiaChecker>();
			}
		}
	}
}
