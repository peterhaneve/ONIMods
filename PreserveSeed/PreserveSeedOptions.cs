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

using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace PeterHan.PreserveSeed {
	/// <summary>
	/// The options class used for Preserve Random Seed.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class PreserveSeedOptions {
		/// <summary>
		/// The only instance of the singleton options.
		/// </summary>
		public static PreserveSeedOptions Instance { get; private set; }
		
		/// <summary>
		/// Loads the settings.
		/// </summary>
		internal static void InitInstance() {
			Instance = POptions.ReadSettings<PreserveSeedOptions>() ??
				new PreserveSeedOptions();
		}

		[Option("STRINGS.UI.FRONTEND.PRESERVESEED.PRESERVEPODSEED", "STRINGS.UI.TOOLTIPS.PRESERVESEED.PRESERVEPODSEED", "STRINGS.UI.FRONTEND.PRESERVESEED.CATEGORY_POD")]
		[JsonProperty]
		public bool PreservePodSeed { get; set; }

		[Option("STRINGS.UI.FRONTEND.PRESERVESEED.RECHARGENORMAL", "STRINGS.UI.TOOLTIPS.PRESERVESEED.RECHARGENORMAL", "STRINGS.UI.FRONTEND.PRESERVESEED.CATEGORY_POD")]
		[Limit(0, 10)]
		[JsonProperty]
		public float RechargeNormal { get; set; }

		[Option("STRINGS.UI.FRONTEND.PRESERVESEED.RECHARGEREJECT", "STRINGS.UI.TOOLTIPS.PRESERVESEED.RECHARGEREJECT", "STRINGS.UI.FRONTEND.PRESERVESEED.CATEGORY_POD")]
		[Limit(0, 10)]
		[JsonProperty]
		public float RechargeReject { get; set; }

		public PreserveSeedOptions() {
			PreservePodSeed = true;
			RechargeNormal = 3.0f;
			RechargeReject = 3.0f;
		}
	}
}
