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
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// The API class for this mod, which allows custom achievements to be hidden or
	/// (future work) categorized.
	/// </summary>
	public sealed class MoreAchievementsAPI : PForwardedComponent {
		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static MoreAchievementsAPI Instance { get; private set; }

		/// <summary>
		/// The version of this component.
		/// </summary>
		internal static readonly Version VERSION = new Version(1, 0, 0, 0);

		private static void UpdateAchievementData_Postfix(string[] newlyAchieved,
				Dictionary<string, GameObject> ___achievementEntries) {
			var newly = HashSetPool<string, AchievementStateComponent>.Allocate();
			// Achievements just obtained should always be shown
			if (newlyAchieved != null)
				foreach (string achieved in newlyAchieved)
					newly.Add(achieved);
			foreach (var pair in ___achievementEntries) {
				var obj = pair.Value;
				string id = pair.Key;
				var info = Instance?.GetAchievement(id);
				if (obj != null && obj.TryGetComponent(out MultiToggle toggle) &&
						info != null && info.Hidden)
					// Hide achievements that have never been achieved
					obj.SetActive(toggle.CurrentState != 2 || newly.Contains(id));
			}
			newly.Recycle();
		}

		public override Version Version => VERSION;

		/// <summary>
		/// The achievements registered for this mod.
		/// </summary>
		private readonly ICollection<AchievementInfo> achievements;

		/// <summary>
		/// The achievements registered for all mods. Only used in the instantiated copy
		/// of MoreAchievementsAPI.
		/// </summary>
		private readonly IDictionary<string, AchievementInfo> allAchievements;

		public MoreAchievementsAPI() {
			achievements = new List<AchievementInfo>();
			allAchievements = new Dictionary<string, AchievementInfo>(64);
			InstanceData = achievements;
		}

		/// <summary>
		/// Sets the category of an achievement and optionally hides it. Currently the
		/// category is not yet implemented.
		/// 
		/// This method must be used in OnLoad.
		/// </summary>
		/// <param name="id">The achievement ID.</param>
		/// <param name="category">The category to assign.</param>
		/// <param name="hidden">true to make the achievement hidden until achieved, or
		/// false otherwise.</param>
		public void AddAchievementInformation(string id, string category,
				bool hidden = false) {
			RegisterForForwarding();
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			achievements.Add(new AchievementInfo() {
				ID = id, Category = category ?? "", Hidden = hidden
			});
		}

		/// <summary>
		/// Retrieves the shared information about the specified achievement, translated into
		/// the local mod's version of AchievementInfo. Only works on the instantiated
		/// copy of MoreAchievementsAPI!
		/// </summary>
		/// <param name="id">The achievement ID to look up.</param>
		/// <returns>The extra information about that achievement.</returns>
		internal AchievementInfo GetAchievement(string id) {
			if (!allAchievements.TryGetValue(id, out AchievementInfo info))
				info = null;
			return info;
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;
			foreach (var achievementProvider in PRegistry.Instance.GetAllComponents(ID))
				if (achievementProvider != null) {
					var toAdd = achievementProvider.GetInstanceDataSerialized<ICollection<
						AchievementInfo>>();
					if (toAdd != null)
						foreach (var achievement in toAdd) {
							allAchievements[achievement.ID] = achievement;
#if DEBUG
							PUtil.LogDebug("Added data for achievement " + achievement.ID);
#endif
						}
				}
			plibInstance.Patch(typeof(RetiredColonyInfoScreen), "UpdateAchievementData",
				postfix: PatchMethod(nameof(UpdateAchievementData_Postfix)));
		}

		/// <summary>
		/// Extra information about a colony achievement.
		/// </summary>
		[JsonObject(MemberSerialization.OptIn)]
		internal sealed class AchievementInfo {
			/// <summary>
			/// The achievement category. Not currently used.
			/// </summary>
			[JsonProperty]
			public string Category { get; set; }

			/// <summary>
			/// Whether the achievement is hidden until achieved.
			/// </summary>
			[JsonProperty]
			public bool Hidden { get; set; }

			/// <summary>
			/// The achievement ID.
			/// </summary>
			[JsonProperty]
			public string ID { get; set; }

			public AchievementInfo() {
				ID = "";
				Category = "";
				Hidden = false;
			}

			public override bool Equals(object obj) {
				return obj is AchievementInfo other && other.ID == ID;
			}

			public override int GetHashCode() {
				return ID.GetHashCode();
			}

			public override string ToString() {
				return "Achievement " + ID;
			}
		}
	}
}
