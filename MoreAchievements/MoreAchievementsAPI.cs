/*
 * Copyright 2021 Peter Han
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
using System;
using System.Collections.Generic;

using AchievementDict = System.Collections.Generic.IDictionary<string, object>;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// The API class for this mod, which allows custom achievements to be hidden or
	/// (future work) categorized.
	/// </summary>
	public static class MoreAchievementsAPI {
		/// <summary>
		/// The achievement API information.
		/// </summary>
		private const string ACHIEVEMENTS_API_INFO = "PeterHan.MoreAchievements.AchievementInfo";

		/// <summary>
		/// The lock to prevent concurrent modification of the achievement information.
		/// </summary>
		private const string ACHIEVEMENTS_API_LOCK = "PeterHan.MoreAchievements.Lock";

		/// <summary>
		/// Gets the information for the specified achievement. The achievement lock must be
		/// held for this method to work properly.
		/// </summary>
		/// <param name="id">The achievement ID to look up.</param>
		/// <returns>The achievement information.</returns>
		private static Traverse GetAchievement(string id) {
			var data = PSharedData.GetData<AchievementDict>(ACHIEVEMENTS_API_INFO);
			if (data == null)
				PSharedData.PutData(ACHIEVEMENTS_API_INFO, data =
					new Dictionary<string, object>(32));
			if (!data.TryGetValue(id, out object info))
				data.Add(id, info = new AchievementInfo(id));
			return Traverse.Create(info);
		}

		/// <summary>
		/// Gets the lock object for the achievement list.
		/// </summary>
		/// <returns>An object used to synchronize mods accessing the achievement list.</returns>
		private static object GetAchievementLock() {
			var obj = PSharedData.GetData<object>(ACHIEVEMENTS_API_LOCK);
			if (obj == null)
				PSharedData.PutData(ACHIEVEMENTS_API_LOCK, obj = new object());
			return obj;
		}

		/// <summary>
		/// Sets the category of an achievement.
		/// 
		/// This method currently is not yet implemented.
		/// 
		/// This method uses PLib and thus PUtil.InitLibrary must be called before using it.
		/// </summary>
		/// <param name="id">The achievement ID.</param>
		/// <param name="category">The category to assign.</param>
		public static void SetAchievementCategory(string id, string category) {
			lock (GetAchievementLock()) {
				var trInfo = GetAchievement(id);
				trInfo.SetProperty(nameof(AchievementInfo.Category), category);
			}
		}

		/// <summary>
		/// Sets an achievement to be hidden until it is achieved.
		/// 
		/// This method uses PLib and thus PUtil.InitLibrary must be called before using it.
		/// </summary>
		/// <param name="id">The achievement ID to hide.</param>
		public static void SetAchievementHidden(string id) {
			lock (GetAchievementLock()) {
				var trInfo = GetAchievement(id);
				trInfo.SetProperty(nameof(AchievementInfo.Hidden), true);
			}
		}

		/// <summary>
		/// Retrieves the shared information about the specified achievement, translated into
		/// the local mod's version of AchievementInfo.
		/// </summary>
		/// <param name="id">The achievement ID to look up.</param>
		/// <returns>The extra information about that achievement.</returns>
		internal static AchievementInfo TranslateAchievement(string id) {
			var newInfo = new AchievementInfo(id);
			lock (GetAchievementLock()) {
				var trInfo = GetAchievement(id);
				try {
					newInfo.Category = trInfo.GetProperty<string>(nameof(AchievementInfo.
						Category));
					newInfo.Hidden = trInfo.GetProperty<bool>(nameof(AchievementInfo.Hidden));
				} catch (Exception e) {
					// Unable to parse, but this is warning only
					PUtil.LogExcWarn(e);
				}
			}
			return newInfo;
		}

		/// <summary>
		/// Extra information about a colony achievement.
		/// </summary>
		internal sealed class AchievementInfo {
			/// <summary>
			/// The achievement category. Not currently used.
			/// </summary>
			public string Category { get; set; }

			/// <summary>
			/// Whether the achievement is hidden until achieved.
			/// </summary>
			public bool Hidden { get; set; }

			/// <summary>
			/// The achievement ID.
			/// </summary>
			public string ID { get; }

			public AchievementInfo(string id) {
				ID = id;
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
