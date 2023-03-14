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

using DAMAGESOURCES = STRINGS.BUILDINGS.DAMAGESOURCES;

namespace PeterHan.ToastControl {
	/// <summary>
	/// Strings used in Popup Control.
	/// </summary>
	public static class ToastControlStrings {
		public static string ACTION_KEY = "TOASTCONTROL.ACTION.SETTINGS";
		public static LocString ACTION_TITLE = string.Format("{0} Settings", UI.FRONTEND.
			TOASTCONTROL.NAME.text);
		
		public static class UI {
			public static class FRONTEND {
				public static class TOASTCONTROL {
					public static LocString OPTIONS_CAPTION = "<b>Selected</b> check boxes will show the popup.\n<b>Deselected</b> check boxes will hide the popup.\n ";

					internal const string BUILDING = "building";
					public static LocString BUILDINGS = "Buildings";
					public static LocString DUPLICANTS = "Duplicants and Critters";
					private const string DISEASE = "disease";
					private const string ITEM = "Item";
					private const string DUPLICANT = "(Duplicant)";
					private const string MACHINE = "(Machine)";
					public static LocString MATERIALS = "Materials";
					internal const string OTHER = "other sources";
					public static LocString TOOLS = "Tools";

					public static LocString CONTROL_ALL = "<b>All</b> Popups";
					public static LocString CONTROL_ALL_TOOLTIP = "If <b>deselected</b>, hides all popup notifications\nregardless of the other settings";
					public static LocString DISABLE_MOVING = "Disable Motion";
					public static LocString DISABLE_MOVING_TOOLTIP = "If selected, popup text will no longer move upwards after creation";

					public static LocString BUILD_COMPLETE = "Construction completed";
					public static LocString CRITTER_DROPS = "Critter drops";
					public static LocString DELIVERED_DUPLICANT = string.Format(STRINGS.UI.DELIVERED,
						ITEM, DUPLICANT);
					public static LocString DELIVERED_MACHINE = string.Format(STRINGS.UI.DELIVERED,
						ITEM, MACHINE);
					public static LocString DISEASE_CURED = string.Format(STRINGS.
						DUPLICANTS.DISEASES.CURED_POPUP, DISEASE);
					public static LocString DISEASE_INFECTED = string.Format(STRINGS.
						DUPLICANTS.DISEASES.INFECTED_POPUP, DISEASE);
					public static LocString EFFECT_ADDED = "Effect applied";
					public static LocString ELEMENT_DUG = "Materials from digging";
					public static LocString ELEMENT_GAINED = "Materials produced";
					public static LocString ELEMENT_MOPPED = "Liquid mopped";
					public static LocString ELEMENT_REMOVED = "Materials consumed";
					public static LocString GERMS_ADDED = "Added germs";
					public static LocString HARVEST_TOGGLED = "Autoharvest toggled";
					public static LocString INVALID_CONNECTION = "Invalid utility connection";
					public static LocString ITEM_GAINED = "Item obtained";
					public static LocString OVERJOYED = "Overjoyed reactions";
					public static LocString PICKEDUP_DUPLICANT = string.Format(STRINGS.UI.PICKEDUP,
						ITEM, DUPLICANT);
					public static LocString PICKEDUP_MACHINE = string.Format(STRINGS.UI.PICKEDUP,
						ITEM, MACHINE);
					public static LocString RADS_REMOVED = "Radiation removed";
					public static LocString RESEARCH_GAINED = "Research point gained";

					private const string DAMAGE_BASE = "Damage: {0}";
					public static LocString DAMAGE_INPUT = string.Format(DAMAGE_BASE,
						"Wrong input element");
					public static LocString DAMAGE_METEOR = string.Format(DAMAGE_BASE,
						"Meteors");
					public static LocString DAMAGE_OTHER = string.Format(DAMAGE_BASE,
						OTHER);
					public static LocString DAMAGE_PIPE = string.Format(DAMAGE_BASE,
						"Pipe state change");
					public static LocString DAMAGE_PRESSURE = string.Format(DAMAGE_BASE,
						"Overpressure");
					public static LocString DAMAGE_ROCKET = string.Format(DAMAGE_BASE,
						"Rocket");

					public static LocString NAME = "Popup Control";
				}
			}

			public static class TOOLTIPS {
				public static class TOASTCONTROL {
					public static LocString BUILD_COMPLETE = "Building construction completed";
					public static LocString CAPTURE_FAILED = "Critter cannot be wrangled manually";
					public static LocString CRITTER_DROPS = "Materials dropped from critters after death or through excretions";
					public static LocString DAMAGE_INPUT = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, DAMAGESOURCES.
						BAD_INPUT_ELEMENT);
					public static LocString DAMAGE_METEOR = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, DAMAGESOURCES.
						COMET);
					public static LocString DAMAGE_OTHER = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, FRONTEND.
						TOASTCONTROL.OTHER);
					public static LocString DAMAGE_PIPE = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, DAMAGESOURCES.
						CONDUIT_CONTENTS_BOILED + " or " + DAMAGESOURCES.
						CONDUIT_CONTENTS_FROZE);
					public static LocString DAMAGE_PRESSURE = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, DAMAGESOURCES.
						LIQUID_PRESSURE);
					public static LocString DAMAGE_ROCKET = string.Format(DAMAGESOURCES.
						NOTIFICATION_TOOLTIP, FRONTEND.TOASTCONTROL.BUILDING, DAMAGESOURCES.
						ROCKET);
					public static LocString DEBUG_LOCATION_INVALID = "Location selected for spawn is not valid";
					public static LocString DELIVERED_DUPLICANT = "Item was delivered to storage or building by a duplicant";
					public static LocString DELIVERED_MACHINE = "Item was delivered to storage or building by a machine";
					public static LocString DISEASE_CURED = "Successfully cured of a disease";
					public static LocString DISEASE_INFECTED = "Became infected by a disease";
					public static LocString EFFECT_ADDED = "An effect was applied to a Duplicant or critter";
					public static LocString ELEMENT_DUG = "Materials were mined from the world";
					public static LocString ELEMENT_GAINED = "Materials were produced from a building and dropped on the ground";
					public static LocString ELEMENT_MOPPED = STRINGS.UI.FormatAsLink("Liquid",
						"LIQUID") + " was mopped into a bottle";
					public static LocString ELEMENT_REMOVED = "Materials were consumed by a building";
					public static LocString FLOOR_CLEARED = "Dropped items were cleared";
					public static LocString FOODROT = STRINGS.UI.FormatAsLink("Food", "FOOD") +
						" items have rotted and are no longer edible";
					public static LocString FORGIVENESS = "Creature is no longer hostile";
					public static LocString HARVEST_TOGGLED = "Autoharvest was enabled or disabled for a plant";
					public static LocString GERMS_ADDED = STRINGS.UI.FormatAsLink("Germs",
						"GERMS") + " were spawned by a building or Duplicant action";
					public static LocString INVALID_CONNECTION = "Utility connection is not valid:\n - Transit Tube cannot make a U-Turn";
					public static LocString ITEM_GAINED = "A new item was obtained";
					public static LocString MOP_NOT_FLOOR = STRINGS.UI.FormatAsLink("Liquid",
						"LIQUID") + " is not on floor";
					public static LocString MOP_TOO_MUCH = "Too much " + STRINGS.UI.
						FormatAsLink("Liquid", "LIQUID") + " to mop";
					public static LocString OVERJOYED = "Duplicants' Overjoyed reactions:\n - Super Productive";
					public static LocString PICKEDUP_DUPLICANT = "Item was removed from storage or building by a duplicant";
					public static LocString PICKEDUP_MACHINE = "Item was removed from storage or building by a machine";
					public static LocString RADS_REMOVED = STRINGS.UI.FormatAsLink("Radiation",
						"RADIATION") + " was removed by a building or Duplicant action";
					public static LocString RESEARCH_GAINED = "Progress made towards a research objective";
					public static LocString SETTINGS_APPLIED = "Settings were copied to another building";
				}
			}
		}
	}
}
