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

using Database;
using FMODUnity;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;

namespace PeterHan.PLib.Database {
	/// <summary>
	/// A wrapper class used to create ColonyAchievement instances.
	/// </summary>
	public sealed class PColonyAchievement {
		/// <summary>
		/// Prototypes the new ColonyAchievment constructor. This one is a monster with a
		/// zillion parallel parameters used only for victory animations (Klei please!) and
		/// gets changed often enough to warrant a detour.
		/// </summary>
		private delegate ColonyAchievement NewColonyAchievement(string Id,
			string platformAchievementId, string Name, string description,
			bool isVictoryCondition, List<ColonyAchievementRequirement> requirementChecklist,
			string messageTitle, string messageBody, string videoDataName,
			string victoryLoopVideo, Action<KMonoBehaviour> VictorySequence);

		/// <summary>
		/// Creates a new colony achievement.
		/// </summary>
		private static readonly NewColonyAchievement NEW_COLONY_ACHIEVEMENT =
			typeof(ColonyAchievement).DetourConstructor<NewColonyAchievement>();

		/// <summary>
		/// The achievement description (string, not a string key!)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// The icon to use for the achievement.
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// The achievement ID.
		/// </summary>
		public string ID { get; }

		/// <summary>
		/// Whether this colony achievement is considered a victory achievement.
		/// 
		/// Victory achievements are displayed at the top, and can play a movie when they
		/// are satisfied.
		/// </summary>
		public bool IsVictory { get; set; }

		/// <summary>
		/// The achievement display name (string, not a string key!)
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The callback triggered if this achievement is a victory achievement when it is
		/// completed.
		/// </summary>
		public Action<KMonoBehaviour> OnVictory { get; set; }

		/// <summary>
		/// The requirements for this achievement.
		/// </summary>
		public List<ColonyAchievementRequirement> Requirements { get; set; }

		/// <summary>
		/// This member is obsolete since the Sweet Dreams update. Use VictoryAudioSnapshoRef
		/// instead.
		/// </summary>
		[Obsolete("Set victory audio snapshot directly due to Klei changes in the Sweet Dreams update")]
		public string VictoryAudioSnapshot { get; set; }

		/// <summary>
		/// The message body to display when this achievement triggers.
		/// 
		/// The game does not use this field by default, but it is available for victory
		/// callbacks.
		/// </summary>
		public string VictoryMessage { get; set; }

		/// <summary>
		/// The message title to display when this achievement triggers.
		/// 
		/// The game does not use this field by default, but it is available for victory
		/// callbacks.
		/// </summary>
		public string VictoryTitle { get; set; }

		/// <summary>
		/// The video data file to play when this achievement triggers.
		/// 
		/// The game does not use this field by default, but it is available for victory
		/// callbacks.
		/// </summary>
		public string VictoryVideoData { get; set; }

		/// <summary>
		/// The video data file to loop behind the message when this achievement triggers.
		/// 
		/// The game does not use this field by default, but it is available for victory
		/// callbacks.
		/// </summary>
		public string VictoryVideoLoop { get; set; }

		/// <summary>
		/// Creates a new colony achievement wrapper.
		/// </summary>
		/// <param name="id">The achievement ID.</param>
		public PColonyAchievement(string id) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			Description = "";
			Icon = "";
			ID = id;
			IsVictory = false;
			Name = "";
			OnVictory = null;
			Requirements = null;
			VictoryMessage = "";
			VictoryTitle = "";
			VictoryVideoData = "";
			VictoryVideoLoop = "";
		}

		/// <summary>
		/// Creates and adds the achievement to the database. As platform achievements cannot
		/// be added using mods, the platform achievement ID will always be empty.
		/// </summary>
		public void AddAchievement() {
			if (Requirements == null)
				throw new ArgumentNullException("No colony achievement requirements specified");
			var achieve = NEW_COLONY_ACHIEVEMENT.Invoke(ID, "", Name, Description, IsVictory,
				Requirements, VictoryTitle, VictoryMessage, VictoryVideoData, VictoryVideoLoop,
				OnVictory);
			achieve.icon = Icon;
			PDatabaseUtils.AddColonyAchievement(achieve);
		}

		public override bool Equals(object obj) {
			return obj is PColonyAchievement other && ID == other.ID;
		}

		public override int GetHashCode() {
			return ID.GetHashCode();
		}

		public override string ToString() {
			return "PColonyAchievement[ID={0},Name={1}]".F(ID, Name);
		}
	}
}
