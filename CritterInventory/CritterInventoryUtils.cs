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

using PeterHan.PLib.Detours;
using System;

using TrackerList = System.Collections.Generic.List<WorldTracker>;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Contains helper functions used by both CritterResourceEntry and CritterResourceHeader.
	/// </summary>
	internal static class CritterInventoryUtils {
		/// <summary>
		/// Creates tool tip text for the critter resource entries and headers.
		/// </summary>
		/// <param name="heading">The heading to display on the tool tip.</param>
		/// <param name="totals">The total quantity available and reserved for errands.</param>
		/// <param name="trend">The trend in the last 150 seconds.</param>
		/// <returns>The tool tip text formatted for those values.</returns>
		internal static string FormatTooltip(string heading, CritterTotals totals, float trend)
		{
			int total = totals.Total, reserved = totals.Reserved;
			var ret = new System.Text.StringBuilder(256);
			ret.Append(heading);
			ret.Append("\n");
			ret.AppendFormat(STRINGS.UI.RESOURCESCREEN.AVAILABLE_TOOLTIP,
				GameUtil.GetFormattedSimple(total - reserved), GameUtil.GetFormattedSimple(
				reserved), GameUtil.GetFormattedSimple(total));
			ret.Append("\n\n");
			if (trend == 0.0f)
				ret.Append(STRINGS.UI.RESOURCESCREEN.TREND_TOOLTIP_NO_CHANGE);
			else
				ret.AppendFormat(STRINGS.UI.RESOURCESCREEN.TREND_TOOLTIP, trend > 0.0f ?
					STRINGS.UI.RESOURCESCREEN.INCREASING_STR : STRINGS.UI.RESOURCESCREEN.
					DECREASING_STR, GameUtil.GetFormattedSimple(trend));
			return ret.ToString();
		}

		/// <summary>
		/// Gets the type of a critter.
		/// </summary>
		/// <param name="creature">The critter to query.</param>
		/// <returns>The critter type.</returns>
		public static CritterType GetCritterType(this CreatureBrain creature) {
			var go = creature.gameObject;
			var result = CritterType.Wild;
			if (creature != null) {
				if (go.GetDef<RobotBatteryMonitor.Def>() != null)
					result = CritterType.Artificial;
				else if (go.GetDef<WildnessMonitor.Def>() != null && !creature.HasTag(GameTags.
						Creatures.Wild))
					result = CritterType.Tame;
			}
			return result;
		}

		/// <summary>
		/// Gets the description for a critter type, localized to ONI's language.
		/// </summary>
		/// <param name="type">The critter type.</param>
		/// <returns>The localized name for display.</returns>
		internal static string GetProperName(this CritterType type) {
			string typeStr;
			switch (type) {
			case CritterType.Tame:
				typeStr = STRINGS.CREATURES.MODIFIERS.TAME.NAME;
				break;
			case CritterType.Wild:
				typeStr = STRINGS.CREATURES.MODIFIERS.WILD.NAME;
				break;
			case CritterType.Artificial:
				typeStr = CritterInventoryStrings.CREATURES.MODIFIERS.ARTIFICIAL.NAME;
				break;
			default:
				throw new InvalidOperationException("Unsupported critter type " + type);
			}
			return typeStr;
		}

		/// <summary>
		/// Gets the proper, localized heading for a critter resource entry.
		/// </summary>
		/// <param name="species">The critter species.</param>
		/// <param name="type">The critter domestication type.</param>
		/// <returns>The title for that entry.</returns>
		internal static string GetTitle(Tag species, CritterType type) {
			return string.Format("{0} ({1})", species.ProperNameStripLink(), type.
				GetProperName());
		}

		/// <summary>
		/// Checks to see if a title matches the search query. The original method in
		/// AllResourcesScreen is private and would be performance intensive to access...
		/// </summary>
		/// <param name="tag">The tag to query.</param>
		/// <param name="filterUp">The user filter text, in upper case.</param>
		/// <returns>true if it matches, or false otherwise.</returns>
		public static bool PassesSearchFilter(string tag, string filterUp) {
			return filterUp == "" || tag.ToUpper().Contains(filterUp);
		}

		/// <summary>
		/// The number of cycles to display on resource charts.
		/// </summary>
		public const float CYCLES_TO_CHART = 5.0f;

		/// <summary>
		/// The (appears to be hardcoded) interval for the trend indicator on tooltips.
		/// </summary>
		public const float TREND_INTERVAL = 150.0f;

		// Detours for private fields in TrackerTool
		private static readonly IDetouredField<TrackerTool, TrackerList> WORLD_TRACKERS =
			PDetours.DetourField<TrackerTool, TrackerList>("worldTrackers");

		/// <summary>
		/// Invokes an action for all critters that match the specified species.
		/// </summary>
		/// <param name="id">The world ID to search.</param>
		/// <param name="critters">The action to call for each critter.</param>
		/// <param name="species">The species to filter, or omitted (default) for all species.</param>
		internal static void GetCritters(int id, Action<CreatureBrain> critters,
				Tag species = default) {
			if (critters == null)
				throw new ArgumentNullException(nameof(critters));
			foreach (var brain in Components.Brains) {
				var creature = brain as CreatureBrain;
				if (creature != null && creature.isSpawned && !creature.HasTag(GameTags.
						Dead) && creature.GetMyWorldId() == id && (species == Tag.Invalid ||
						species == creature.PrefabID()))
					critters.Invoke(creature);
			}
		}

		/// <summary>
		/// Gets the resource tracker which tracks critter counts over time.
		/// </summary>
		/// <typeparam name="T">The type of critter tracker to find.</typeparam>
		/// <param name="id">The world ID to match.</param>
		/// <param name="type">The critter type to look up.</param>
		/// <returns>The tracker for that critter type and world ID.</returns>
		internal static T GetTracker<T>(int id, CritterType type, Func<T, bool> filter = null)
				where T : BaseCritterTracker {
			var inst = TrackerTool.Instance;
			TrackerList trackers;
			T result = null;
			if (inst != null && (trackers = WORLD_TRACKERS.Get(inst)) != null)
				foreach (var tracker in trackers)
					// Search for tracker that matches both world ID and wildness type
					if (tracker is T ct && ct.WorldID == id && ct.Type == type && (filter ==
							null || filter.Invoke(ct))) {
						result = ct;
						break;
					}
			return result;
		}
	}
}
