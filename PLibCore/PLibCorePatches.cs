/*
 * Copyright 2023 Peter Han
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
using System;
using System.Collections.Generic;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// A small component which applies core patches used by PLib.
	/// </summary>
	internal sealed class PLibCorePatches : PForwardedComponent {
		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// Localizes all mods to the current locale.
		/// </summary>
		private static void Initialize_Postfix() {
			var locale = Localization.GetLocale();
			if (locale != null) {
				int n = 0;
				string locCode = locale.Code;
				if (string.IsNullOrEmpty(locCode))
					locCode = Localization.GetCurrentLanguageCode();
				var libLocal = PRegistry.Instance.GetAllComponents(typeof(PLibCorePatches).
					FullName);
				if (libLocal != null)
					foreach (var component in libLocal)
						component.Process(0, locale);
				var modLocal = PRegistry.Instance.GetAllComponents(
					"PeterHan.PLib.Database.PLocalization");
				if (modLocal != null)
					foreach (var component in modLocal) {
						component.Process(0, locale);
						n++;
					}
				if (n > 0)
					PRegistry.LogPatchDebug("Localized {0:D} mod(s) to locale {1}".F(
						n, locCode));
			}
		}

		private static IEnumerable<CodeInstruction> LoadPreviewImage_Transpile(
				IEnumerable<CodeInstruction> body) {
			var target = typeof(Debug).GetMethodSafe(nameof(Debug.LogFormat), true,
				typeof(string), typeof(object[]));
			return (target == null) ? body : PPatchTools.RemoveMethodCall(body, target);
		}

		public override Version Version => VERSION;

		public override void Initialize(Harmony plibInstance) {
			UI.TextMeshProPatcher.Patch();
			var ugc = PPatchTools.GetTypeSafe("SteamUGCService", "Assembly-CSharp");
			if (ugc != null)
				try {
					plibInstance.PatchTranspile(ugc, "LoadPreviewImage", PatchMethod(nameof(
						LoadPreviewImage_Transpile)));
				} catch (Exception e) {
#if DEBUG
					PUtil.LogExcWarn(e);
#endif
				}
			plibInstance.Patch(typeof(Localization), nameof(Localization.Initialize),
				postfix: PatchMethod(nameof(Initialize_Postfix)));
		}

		public override void Process(uint operation, object _) {
			var locale = Localization.GetLocale();
			if (locale != null && operation == 0)
				PLibLocalization.LocalizeItself(locale);
		}

		/// <summary>
		/// Registers this instance of the PLib core patches.
		/// </summary>
		/// <param name="instance">The registry instance to use (since PRegistry.Instance
		/// is not yet fully initialized).</param>
		internal void Register(IPLibRegistry instance) {
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			instance.AddCandidateVersion(this);
		}
	}
}
