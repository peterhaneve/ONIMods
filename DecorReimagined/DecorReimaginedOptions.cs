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

using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// The options class used for Decor Reimagined.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	[ModInfo("https://github.com/peterhaneve/ONIMods", "preview.png")]
	[RestartRequired]
	public sealed class DecorReimaginedOptions {
		// String names for the art stages.
		private const string ART_CRUDE = "Bad";
		private const string ART_DEFAULT = "Default";
		private const string ART_MASTERPIECE = "Good";
		private const string ART_QUAINT = "Average";

		/// <summary>
		/// Whether all critters unconditionally get 0 decor. Improves performance but makes
		/// the game harder.
		/// </summary>
		[Option("STRINGS.UI.FRONTEND.DECORREIMAGINED.NOCRITTERDECOR", "STRINGS.UI.TOOLTIPS.DECORREIMAGINED.NOCRITTERDECOR")]
		[JsonProperty]
		public bool AllCrittersZeroDecor { get; set; }

		/// <summary>
		/// The decor of the Atmo Suit and Jet Suit items.
		/// </summary>
		[JsonProperty]
		public int AtmoSuitDecor { get; set; }

		/// <summary>
		/// The decor of broken buildings.
		/// </summary>
		[JsonProperty]
		public float BrokenBuildingDecor { get; set; }

		/// <summary>
		/// The decor bonus for level 1 art "crude".
		/// </summary>
		[JsonProperty]
		public int CrudeArtDecor { get; set; }

		/// <summary>
		/// The decor of each different debris pile.
		/// </summary>
		[JsonProperty]
		public float DebrisDecor { get; set; }

		/// <summary>
		/// The radius of decor from debris.
		/// </summary>
		[JsonProperty]
		public int DebrisRadius { get; set; }

		/// <summary>
		/// The decor bonus for art that has not been started.
		/// </summary>
		[JsonProperty]
		public int DefaultArtDecor { get; set; }

		/// <summary>
		/// Whether hard mode is enabled.
		/// </summary>
		[Option("STRINGS.UI.FRONTEND.DECORREIMAGINED.HARDMODE", "STRINGS.UI.TOOLTIPS.DECORREIMAGINED.HARDMODE")]
		[JsonProperty]
		public bool HardMode { get; set; }

		/// <summary>
		/// The decor bonus for level 3 art "masterpiece".
		/// </summary>
		[JsonProperty]
		public int MasterpieceArtDecor { get; set; }

		/// <summary>
		/// If true, tile decor will not be changed.
		/// </summary>
		[Option("STRINGS.UI.FRONTEND.DECORREIMAGINED.KEEPTILEDECOR", "STRINGS.UI.TOOLTIPS.DECORREIMAGINED.KEEPTILEDECOR")]
		[JsonProperty]
		public bool PreserveTileDecor { get; set; }

		/// <summary>
		/// The decor bonus for level 2 art "quaint".
		/// </summary>
		[JsonProperty]
		public int QuaintArtDecor { get; set; }

		/// <summary>
		/// The decor of the Snazzy Suit item.
		/// </summary>
		[JsonProperty]
		public int SnazzySuitDecor { get; set; }

		/// <summary>
		/// The decor of an ugly crier.
		/// </summary>
		[JsonProperty]
		public int UglyCrierDecor { get; set; }

		/// <summary>
		/// The decor of the Warm Vest and Cool Vest items.
		/// </summary>
		[JsonProperty]
		public int VestDecor { get; set; }

		public DecorReimaginedOptions() {
			AllCrittersZeroDecor = false;
			AtmoSuitDecor = -10;
			BrokenBuildingDecor = -60.0f;
			CrudeArtDecor = 0;
			DebrisDecor = -10.0f;
			DebrisRadius = 2;
			DefaultArtDecor = -5;
			HardMode = false;
			MasterpieceArtDecor = 10;
			PreserveTileDecor = false;
			QuaintArtDecor = 5;
			SnazzySuitDecor = 15;
			// -30 is a slap on the wrist
			UglyCrierDecor = -150;
			VestDecor = 0;
		}

		/// <summary>
		/// Applies these settings to a completed sculpture. The settings applied are the
		/// decor bonuses for each art level.
		/// </summary>
		/// <param name="obj">The sculpture to modify.</param>
		public void ApplyToSculpture(GameObject obj) {
			if (obj.TryGetComponent(out Artable _)) {
				var statuses = Db.Get().ArtableStatuses;
				foreach (var stage in Db.GetArtableStages().GetPrefabStages(obj.PrefabID())) {
					var artLevel = stage.statusItem;
					if (artLevel == statuses.Ugly)
						stage.decor = CrudeArtDecor;
					else if (artLevel == statuses.Okay)
						stage.decor = QuaintArtDecor;
					else if (artLevel == statuses.Great)
						// Good1, Good2, Good3
						stage.decor = MasterpieceArtDecor;
					else if (artLevel == statuses.Ready)
						stage.decor = DefaultArtDecor;
				}
			}
		}

		public override string ToString() {
			return "DecorReimaginedOptions[hard={0}]".F(HardMode);
		}
	}
}
