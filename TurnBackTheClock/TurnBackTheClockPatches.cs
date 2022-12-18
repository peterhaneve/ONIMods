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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// Patches which will be applied via annotations for Turn Back the Clock.
	/// </summary>
	public sealed class TurnBackTheClockPatches : KMod.UserMod2 {
		/// <summary>
		/// Applied to Techs to apply tech tree changes.
		/// </summary>
		[HarmonyPatch(typeof(Database.Techs), nameof(Database.Techs.PostProcess))]
		public static class Techs_PostProcess_Patch {
			internal static void Prefix() {
				if (TurnBackTheClockOptions.Instance.MD471618_TechTree)
					MD471618.TechTreeFix();
			}
		}

		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit(Harmony harmony) {
			MD471618.AfterDbInit();
			MD525812.AfterDbInit(harmony);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(TurnBackTheClockOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(TurnBackTheClockPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			MD471618.AllDiscovered = false;
		}
	}
}
