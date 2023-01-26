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
using Klei.AI;
using PeterHan.PLib.Actions;
using System.Collections.Generic;
using System.Text;
using PeterHan.PLib.Core;

using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to improve format strings for the UI.
	/// </summary>
	public static partial class FormatStringPatches {
		/// <summary>
		/// Applied to Database.CreatureStatusItems to replace some delegates with versions
		/// that allocate less memory.
		/// </summary>
		[HarmonyPatch(typeof(Database.CreatureStatusItems), nameof(Database.
			CreatureStatusItems.CreateStatusItems))]
		internal static class CreatureStatusItems_CreateStatusItems_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied after CreateStatusItems runs.
			/// </summary>
			internal static void Postfix(Database.CreatureStatusItems __instance) {
				__instance.Fresh.resolveStringCallback = ResolveRotTitle;
				__instance.Stale.resolveStringCallback = ResolveRotTitle;
				__instance.Refrigerated.resolveStringCallback = ResolveRefrigerationTitle;
				__instance.RefrigeratedFrozen.resolveStringCallback =
					ResolveRefrigerationTitle;
				__instance.Unrefrigerated.resolveStringCallback = ResolveRefrigerationTitle;
			}

			/// <summary>
			/// Gets the title for the refrigeration tooltip.
			/// </summary>
			/// <param name="baseStr">The localized template string.</param>
			/// <param name="data">The food item to display.</param>
			/// <returns>The title for the status item.</returns>
			private static string ResolveRefrigerationTitle(string baseStr, object data) {
				var text = CACHED_BUILDER;
				string result = baseStr;
				text.Clear();
				if (data is IRottable rottable) {
					GetFormattedTemperature(text, rottable.RotTemperature);
					string highTemp = text.ToString();
					text.Clear();
					GetFormattedTemperature(text, rottable.PreserveTemperature);
					string lowTemp = text.ToString();
					result = text.Clear().Append(baseStr).Replace("{RotTemperature}",
						highTemp).Replace("{PreserveTemperature}", lowTemp).ToString();
				}
				return result;
			}

			/// <summary>
			/// Gets the title for the rot percentage tooltip.
			/// </summary>
			/// <param name="baseStr">The localized template string.</param>
			/// <param name="data">The food item to display.</param>
			/// <returns>The title for the status item.</returns>
			private static string ResolveRotTitle(string baseStr, object data) {
				var text = CACHED_BUILDER;
				string result = baseStr;
				text.Clear();
				if (data is Rottable.Instance smi) {
					text.Append("(");
					(smi.RotConstitutionPercentage * 100.0f).ToRyuHardString(text, 0);
					text.Append(PCT).Append(")");
					string rotPercent = text.ToString();
					result = text.Clear().Append(baseStr).Replace("{RotPercentage}",
						rotPercent).ToString();
				}
				return result;
			}
		}

		/// <summary>
		/// Applied to Element to thoroughly reduce the memory consumption of the generated
		/// description.
		/// </summary>
		[HarmonyPatch(typeof(Element), nameof(Element.FullDescription))]
		internal static class FullDescription_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every call.
			/// </summary>
			private static readonly StringBuilder OUTER_BUILDER = new StringBuilder(256);

			private static readonly StringBuilder PART_BUILDER = new StringBuilder(64);

			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Adds the phase change thresholds of this element.
			/// </summary>
			/// <param name="text">The text to be displayed.</param>
			/// <param name="element">The element being displayed.</param>
			/// <param name="addHardnessColor">true to show the hardness with its matching
			/// color, or false otherwise.</param>
			private static void AddPhaseChange(StringBuilder text, Element element,
					bool addHardnessColor) {
				string ht, lt;
				var part = PART_BUILDER;
				text.AppendLine().AppendLine();
				part.Clear();
				if (element.IsSolid) {
					GetFormattedTemperature(part, element.highTemp);
					ht = part.ToString();
					part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCSOLID).Replace("{1}",
						ht).Replace("{2}", GetHardnessString(element, addHardnessColor));
				} else if (element.IsLiquid) {
					GetFormattedTemperature(part, element.highTemp);
					ht = part.ToString();
					part.Clear();
					GetFormattedTemperature(part, element.lowTemp);
					lt = part.ToString();
					part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCLIQUID).Replace("{1}",
						lt).Replace("{2}", ht);
				} else {
					GetFormattedTemperature(part, element.lowTemp);
					lt = part.ToString();
					part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCGAS).Replace("{1}",
						lt);
				}
				part.Replace("{0}", element.GetMaterialCategoryTag().ProperName());
			}

			/// <summary>
			/// Adds the radiation information about this element.
			/// </summary>
			/// <param name="text">The text to be displayed.</param>
			/// <param name="element">The element being displayed.</param>
			private static void AddRads(StringBuilder text, Element element) {
				var part = PART_BUILDER;
				part.Clear();
				element.radiationAbsorptionFactor.ToRyuSoftString(part, 4);
				string radAbsorb = part.ToString();
				part.Clear();
				(element.radiationPer1000Mass * 1.1f).ToRyuSoftString(part, 3);
				string radEmit = part.Append(STRINGS.UI.UNITSUFFIXES.RADIATION.RADS).
					Append(PER_CYCLE).ToString();
				// Could not find this constant in Klei source
				part.Clear().Append(STRINGS.ELEMENTS.RADIATIONPROPERTIES).Replace("{0}",
					radAbsorb).Replace("{1}", radEmit);
				text.AppendLine().Append(part);
			}

			/// <summary>
			/// Adds the tags and material properties for the element.
			/// </summary>
			/// <param name="text">The text to be displayed.</param>
			/// <param name="element">The element being displayed.</param>
			private static void AddTags(StringBuilder text, Element element) {
				var modifiers = element.attributeModifiers;
				var tags = element.oreTags;
				int n = tags.Length;
				if (n > 0 && !element.IsVacuum) {
					text.AppendLine().AppendLine().Append(EP);
					for (int i = 0; i < n; i++) {
						var tag = tags[i];
						text.Append(tag.ProperName());
						if (i < n - 1)
							text.Append(", ");
					}
				}
				n = modifiers.Count;
				for (int i = 0; i < n; i++) {
					var modifier = modifiers[i];
					string name = Db.Get().BuildingAttributes.Get(modifier.AttributeId).Name;
					text.AppendLine().Append(STRINGS.UI.PRE_KEYWORD).Append(name).Append(
						STRINGS.UI.PST_KEYWORD).Append(": ").Append(modifier.
						GetFormattedString());
				}
			}

			/// <summary>
			/// Adds the thermal properties for the element.
			/// </summary>
			/// <param name="text">The text to be displayed.</param>
			/// <param name="element">The element being displayed.</param>
			private static void AddThermal(StringBuilder text, Element element) {
				var part = PART_BUILDER;
				string tempUnits = GameUtil.GetTemperatureUnitSuffix();
				float shc = GameUtil.GetDisplaySHC(element.specificHeatCapacity),
					tc = GameUtil.GetDisplayThermalConductivity(element.
					thermalConductivity);
				part.Clear();
				shc.ToRyuHardString(part, 3);
				string shcText = part.Append(" (DTU/g)/").Append(tempUnits).ToString();
				part.Clear();
				tc.ToRyuHardString(part, 3);
				string tcText = part.Append(" (DTU/(m*s))/").Append(tempUnits).ToString();
				part.Clear().Append(STRINGS.ELEMENTS.THERMALPROPERTIES).
					Replace("{SPECIFIC_HEAT_CAPACITY}", shcText).
					Replace("{THERMAL_CONDUCTIVITY}", tcText);
				text.AppendLine().Append(part);
			}

			/// <summary>
			/// Applied before FullDescription runs.
			/// </summary>
			internal static bool Prefix(Element __instance, bool addHardnessColor,
					ref string __result) {
				var text = OUTER_BUILDER;
				text.Clear();
				text.Append(__instance.Description());
				if (!__instance.IsVacuum)
					AddPhaseChange(text, __instance, addHardnessColor);
				AddThermal(text, __instance);
				if (DlcManager.FeatureRadiationEnabled())
					AddRads(text, __instance);
				AddTags(text, __instance);
				__result = text.ToString();
				return false;
			}
		}
	}

	/// <summary>
	/// Groups patches to optimize amount object display.
	/// </summary>
	public static class AmountDisplayPatches {
		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(128);

		/// <summary>
		/// Gets the value to be displayed by the standard amount displayer.
		/// </summary>
		/// <param name="text">The location where the result will be stored.</param>
		/// <param name="formatter">Formats the value to user-friendly units.</param>
		/// <param name="instance">The value to be displayed.</param>
		/// <param name="master">The amount to be displayed.</param>
		private static void GetValueString(StringBuilder text, IAttributeFormatter formatter,
				AmountInstance instance, Amount master) {
			string formatted = formatter.GetFormattedValue(instance.value, TimeSlice.None);
			text.Append(formatted);
			if (master.showMax)
				text.Append(" / ").Append(formatter.GetFormattedValue(instance.GetMax(),
					TimeSlice.None));
		}

		/// <summary>
		/// Applied to AsPercentAmountDisplayer to reduce memory allocations when displaying
		/// its description.
		/// </summary>
		[HarmonyPatch(typeof(AsPercentAmountDisplayer), nameof(StandardAmountDisplayer.
			GetDescription))]
		internal static class AsPercent_GetDescription_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetDescription runs.
			/// </summary>
			internal static bool Prefix(AsPercentAmountDisplayer __instance, Amount master,
					AmountInstance instance, ref string __result) {
				// The string builder was a push, but this sure beats string.Format!
				__result = master.Name + ": " + __instance.Formatter.GetFormattedValue(
					__instance.ToPercent(instance.value, instance), TimeSlice.None);
				return false;
			}
		}

		/// <summary>
		/// Applied to AsPercentAmountDisplayer to reduce memory allocations when displaying
		/// its tooltip.
		/// </summary>
		[HarmonyPatch(typeof(AsPercentAmountDisplayer), nameof(StandardAmountDisplayer.
			GetTooltip))]
		internal static class AsPercent_GetTooltip_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetTooltip runs.
			/// </summary>
			internal static bool Prefix(AsPercentAmountDisplayer __instance, Amount master,
					AmountInstance instance, ref string __result) {
				var text = CACHED_BUILDER;
				var formatter = __instance.Formatter;
				var timeSlice = formatter.DeltaTimeSlice;
				var modifiers = instance.deltaAttribute.Modifiers;
				int n = modifiers.Count;
				var delta = instance.deltaAttribute;
				text.Clear().Append(master.description).Replace("{0}", formatter.
					GetFormattedValue(instance.value, TimeSlice.None)).AppendLine().
					AppendLine();
				if (timeSlice == TimeSlice.PerCycle)
					text.Append(STRINGS.UI.CHANGEPERCYCLE).Replace("{0}", formatter.
						GetFormattedValue(__instance.ToPercent(delta.GetTotalDisplayValue(),
						instance), TimeSlice.PerCycle));
				else if (timeSlice == TimeSlice.PerSecond)
					text.Append(STRINGS.UI.CHANGEPERSECOND).Replace("{0}", formatter.
						GetFormattedValue(__instance.ToPercent(delta.GetTotalDisplayValue(),
						instance), TimeSlice.PerCycle));
				for (int i = 0; i < n; i++) {
					var modifier = modifiers[i];
					text.AppendLine().Append(Constants.TABBULLETSTRING).Append(modifier.
						GetDescription()).Append(": ").Append(formatter.GetFormattedValue(
						__instance.ToPercent(delta.GetModifierContribution(modifier),
						instance), timeSlice));
				}
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to StandardAmountDisplayer to reduce memory allocations when displaying
		/// its description.
		/// </summary>
		[HarmonyPatch(typeof(StandardAmountDisplayer), nameof(StandardAmountDisplayer.
			GetDescription))]
		internal static class Standard_GetDescription_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetDescription runs.
			/// </summary>
			internal static bool Prefix(StandardAmountDisplayer __instance, Amount master,
					AmountInstance instance, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear().Append(master.Name).Append(": ");
				GetValueString(text, __instance.Formatter, instance, master);
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to StandardAmountDisplayer to reduce memory allocations when displaying
		/// its tooltip.
		/// </summary>
		[HarmonyPatch(typeof(StandardAmountDisplayer), nameof(StandardAmountDisplayer.
			GetTooltip))]
		internal static class Standard_GetTooltip_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetTooltip runs.
			/// </summary>
			internal static bool Prefix(StandardAmountDisplayer __instance, Amount master,
					AmountInstance instance, ref string __result) {
				var text = CACHED_BUILDER;
				var formatter = __instance.Formatter;
				var timeSlice = formatter.DeltaTimeSlice;
				string desc = master.description;
				var modifiers = instance.deltaAttribute.Modifiers;
				int n = modifiers.Count;
				// Yes the base game uses None even if the time slice is not none
				text.Clear().Append(desc).Replace("{0}", formatter.GetFormattedValue(instance.
					value, TimeSlice.None));
				if (desc.Contains("{1}"))
					// GetIdentityDescriptor calls GetComponent so only call if needed
					text.Replace("{1}", GameUtil.GetIdentityDescriptor(instance.gameObject));
				text.AppendLine().AppendLine();
				if (timeSlice == TimeSlice.PerCycle)
					text.Append(STRINGS.UI.CHANGEPERCYCLE).Replace("{0}", formatter.
						GetFormattedValue(instance.deltaAttribute.GetTotalDisplayValue(),
						TimeSlice.PerCycle));
				else if (timeSlice == TimeSlice.PerSecond)
					text.Append(STRINGS.UI.CHANGEPERSECOND).Replace("{0}", formatter.
						GetFormattedValue(instance.deltaAttribute.GetTotalDisplayValue(),
						TimeSlice.PerSecond));
				for (int i = 0; i < n; i++) {
					var modifier = modifiers[i];
					text.AppendLine().Append(Constants.TABBULLETSTRING).Append(modifier.
						GetDescription()).Append(": ").Append(formatter.GetFormattedModifier(
						modifier));
				}
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to StandardAmountDisplayer to reduce memory allocations when displaying
		/// its value.
		/// </summary>
		[HarmonyPatch(typeof(StandardAmountDisplayer), nameof(StandardAmountDisplayer.
			GetValueString))]
		internal static class Standard_GetValueString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetValueString runs.
			/// </summary>
			internal static bool Prefix(StandardAmountDisplayer __instance, Amount master,
					AmountInstance instance, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				GetValueString(text, __instance.Formatter, instance, master);
				__result = text.ToString();
				return false;
			}
		}
	}

	/// <summary>
	/// Groups patches to reduce LocText memory allocations.
	/// </summary>
	public static class LocTextAllocPatches {
		/// <summary>
		/// Buffers the result of GetActionString.
		/// </summary>
		private static readonly StringBuilder ACTION_BUFFER = new StringBuilder(32);

		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(512);
		
		/// <summary>
		/// The prefix used for all mouse click substitutions.
		/// </summary>
		private const string CLICK_PREFIX = "ClickType";

		/// <summary>
		/// Buffers the hotkey text.
		/// </summary>
		private static readonly StringBuilder HOTKEY_BUFFER = new StringBuilder(32);

		/// <summary>
		/// The prefix used for all hotkey substitutions.
		/// </summary>
		private const string HOTKEY_PREFIX = "Hotkey";

		/// <summary>
		/// Caches hotkey substitutions to hotkey strings. There are 273 when this method was
		/// written.
		/// </summary>
		private static readonly IDictionary<string, string> HOTKEY_LOOKUP =
			new Dictionary<string, string>(384);

		/// <summary>
		/// Stores the cached text for modifier keys.
		/// </summary>
		private static readonly string[] MODIFERS = new string[5];

		/// <summary>
		/// Gets the action's command as a user-friendly string. Only works if the Steam
		/// Controller is not in use.
		/// </summary>
		/// <param name="inputBinding">The input binding for the command.</param>
		/// <param name="text">The location where the result will be stored</param>
		private static void GetActionString(ref BindingEntry inputBinding, StringBuilder text)
		{
			var keyCode = inputBinding.mKeyCode;
			var modifier = inputBinding.mModifier;
			var modText = MODIFERS;
			string raw = StringFormatter.ToUpper(GameUtil.GetKeycodeLocalized(keyCode));
			if (modifier == Modifier.None)
				text.Append(raw);
			else {
				bool first = true;
				if ((modifier & Modifier.Alt) != 0) {
					text.Append(modText[0]);
					first = false;
				}
				if ((modifier & Modifier.Ctrl) != 0) {
					if (!first) text.Append(" + ");
					text.Append(modText[1]);
					first = false;
				}
				if ((modifier & Modifier.Shift) != 0) {
					if (!first) text.Append(" + ");
					text.Append(modText[2]);
					first = false;
				}
				switch (modifier) {
				case Modifier.CapsLock:
					if (!first) text.Append(" + ");
					text.Append(modText[3]);
					break;
				case Modifier.Backtick:
					if (!first) text.Append(" + ");
					text.Append(modText[4]);
					break;
				}
				text.Append(" + ").Append(raw);
			}
		}

		/// <summary>
		/// Populates the hotkey lookup table.
		/// </summary>
		private static void Init() {
			var actionBuffer = ACTION_BUFFER;
			var kb = GameInputMapping.KeyBindings;
			var lookup = HOTKEY_LOOKUP;
			var steamGamepad = KInputManager.steamInputInterpreter;
			if (kb != null) {
				int n = kb.Length;
				bool isGamepad = KInputManager.currentControllerIsGamepad;
				// Precompute the modifier strings
				var modText = MODIFERS;
				modText[0] = GameUtil.GetKeycodeLocalized(KKeyCode.LeftAlt).ToUpper();
				modText[1] = GameUtil.GetKeycodeLocalized(KKeyCode.LeftControl).ToUpper();
				modText[2] = GameUtil.GetKeycodeLocalized(KKeyCode.LeftShift).ToUpper();
				modText[3] = GameUtil.GetKeycodeLocalized(KKeyCode.CapsLock).ToUpper();
				modText[4] = GameUtil.GetKeycodeLocalized(KKeyCode.BackQuote).ToUpper();
				for (int i = 0; i < n; i++) {
					ref var binding = ref kb[i];
					var action = binding.mAction;
					// Only perform on entries with a matching key binding
					actionBuffer.Clear().Append("<b><color=#F44A4A>");
					if (isGamepad)
						actionBuffer.Append(steamGamepad.GetActionGlyph(action));
					else {
						actionBuffer.Append('[');
						GetActionString(ref binding, actionBuffer);
						actionBuffer.Append(']');
					}
					lookup[action.ToString()] = actionBuffer.Append("</b></color>").
						ToString();
				}
				// Copy the click strings from LocText
				foreach (var pair in LocText.ClickLookup) {
					var text = pair.Value;
					lookup[pair.Key] = isGamepad ? text.first : text.second;
				}
			}
		}

		/// <summary>
		/// Checks to see if the buffered hotkey string matches the prefix.
		/// </summary>
		/// <param name="prefix">The prefix to compare.</param>
		/// <returns>true if it is a hotkey, or false otherwise.</returns>
		private static bool IsHotkey(string prefix) {
			var hkb = HOTKEY_BUFFER;
			int n = hkb.Length, compareN = prefix.Length;
			bool equals = n == compareN;
			for (int i = 0; i < n && equals; i++)
				equals = hkb[i] == prefix[i];
			return equals;
		}

		/// <summary>
		/// Parses hotkey sequences from the text.
		/// </summary>
		/// <param name="text">The location where the output will be stored.</param>
		/// <param name="input">The input text.</param>
		private static void ParseHotkeys(StringBuilder text, string input) {
			int n = input.Length;
			bool hotkey = false;
			char substitute = '0';
			var hkb = HOTKEY_BUFFER;
			var lookup = HOTKEY_LOOKUP;
			text.Clear();
			// Populate table only if necessary
			lock (lookup) {
				if (lookup.Count < 1)
					Init();
			}
			for (int i = 0; i < n; i++) {
				char c = input[i];
				if (c == '{' || c == '(') {
					hotkey = false;
					substitute = c;
				} else if (substitute != '0')
					switch (c) {
					case '/':
						// Hotkey/...
						if (IsHotkey(HOTKEY_PREFIX) || IsHotkey(CLICK_PREFIX)) {
							hotkey = true;
							hkb.Clear();
						} else
							hkb.Append(c);
						break;
					case '}':
					case ')':
						// What was requested?
						if (hotkey) {
							if (lookup.TryGetValue(hkb.ToString(), out string formatted))
								text.Append(formatted);
						} else
							// Strings with other {} should get by unaltered
							text.Append(substitute).Append(hkb).Append(c);
						hkb.Clear();
						substitute = '0';
						break;
					default:
						hkb.Append(c);
						break;
					}
				else
					text.Append(c);
			}
			if (substitute != '0') {
				// Unterminated string, fill it in verbatim
				text.Append(substitute).Append(hkb);
				hkb.Clear();
			}
		}

		/// <summary>
		/// Checks for the Steam input interpreter (not present on wegame or EGS) and disables
		/// these particular patches if not found.
		/// </summary>
		/// <returns>true if the strings can be optimized, or false if the option is disabled
		/// or the input interpreter is missing.</returns>
		private static bool CheckPatch() => FastTrackOptions.Instance.AllocOpts &&
			PPatchTools.GetTypeSafe(nameof(SteamInputInterpreter)) != null;

		/// <summary>
		/// Applied to GameUtil to make calculating action strings much faster.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetActionString))]
		internal static class GetActionString_Patch {
			internal static bool Prepare() => CheckPatch();

			/// <summary>
			/// Applied before GetActionString runs.
			/// </summary>
			internal static bool Prefix(Action action, ref string __result) {
				string result;
				// Allow actions over the max for mods
				if (action > 0 && action != PAction.MaxAction) {
					var bindingEntry = GameUtil.ActionToBinding(action);
					if (KInputManager.currentControllerIsGamepad)
						result = KInputManager.steamInputInterpreter.GetActionGlyph(action);
					else {
						var text = ACTION_BUFFER;
						text.Clear();
						GetActionString(ref bindingEntry, text);
						result = text.ToString();
					}
				} else
					result = "";
				__result = result;
				return false;
			}
		}

		/// <summary>
		/// Applied to LocText to make parsing the text for links and hotkeys more efficient.
		/// </summary>
		[HarmonyPatch(typeof(LocText), nameof(LocText.FilterInput))]
		internal static class FilterInput_Patch {
			internal static bool Prepare() => CheckPatch();

			/// <summary>
			/// Applied before FilterInput runs.
			/// </summary>
			internal static bool Prefix(LocText __instance, string input, ref string __result)
			{
				var text = CACHED_BUILDER;
				if (input != null) {
					ParseHotkeys(text, input);
					// Link handling
					if (__instance.AllowLinks && !input.Contains(LocText.linkColorPrefix))
						text.Replace(LocText.linkPrefix_open, LocText.combinedPrefix).
							Replace(LocText.linkSuffix, LocText.combinedSuffix);
					__result = text.ToString();
				} else
					__result = null;
				return false;
			}
		}

		/// <summary>
		/// Applied to LocText to make parsing the text for links and hotkeys more efficient.
		/// </summary>
		[HarmonyPatch(typeof(LocText), nameof(LocText.ParseText))]
		internal static class ParseText_Patch {
			internal static bool Prepare() => CheckPatch();

			/// <summary>
			/// Applied before ParseText runs.
			/// </summary>
			internal static bool Prefix(string input, ref string __result)
			{
				var text = CACHED_BUILDER;
				if (input != null) {
					ParseHotkeys(text, input);
					__result = text.ToString();
				} else
					__result = null;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameInputManager to dump the cache whenever key binds are changed.
		/// </summary>
		[HarmonyPatch(typeof(GameInputManager), nameof(GameInputManager.RebindControls))]
		internal static class RebindControls_Patch {
			internal static bool Prepare() => CheckPatch();

			/// <summary>
			/// Applied after RebindControls runs.
			/// </summary>
			internal static void Postfix() {
				var lookup = HOTKEY_LOOKUP;
				lock (lookup) {
					lookup.Clear();
				}
			}
		}
	}

	/// <summary>
	/// Applied to StringFormatter to reduce wasted memory on 3-way sound event combines
	/// where the second parameter is always a "_".
	/// </summary>
	[HarmonyPatch(typeof(StringFormatter), nameof(StringFormatter.Combine), typeof(string),
		typeof(string), typeof(string))]
	public static class StringFormatter_Combine3_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

		/// <summary>
		/// Applied before Combine runs.
		/// </summary>
		internal static bool Prefix(string a, string b, string c, ref string __result) {
			bool cont = b != "_";
			if (!cont) {
				var cached = StringFormatter.cachedCombines;
				if (!cached.TryGetValue(a, out var dictionary))
					cached[a] = dictionary = new Dictionary<string, string>(8);
				if (!dictionary.TryGetValue(c, out string text))
					dictionary[c] = text = a + "_" + c;
				__result = text;
			}
			return cont;
		}
	}

	/// <summary>
	/// Applied to StringFormatter to reduce wasted memory on 4-way sound event combines
	/// where the third parameter is always a "_" and first is always "DupVoc_".
	/// </summary>
	[HarmonyPatch(typeof(StringFormatter), nameof(StringFormatter.Combine), typeof(string),
		typeof(string), typeof(string), typeof(string))]
	public static class StringFormatter_Combine4_Patch {
		private const string PREFIX = "DupVoc_";

		internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

		/// <summary>
		/// Applied before Combine runs.
		/// </summary>
		internal static bool Prefix(string a, string b, string c, string d,
				ref string __result) {
			bool cont = c != "_" || a != PREFIX;
			if (!cont) {
				var cached = StringFormatter.cachedCombines;
				if (!cached.TryGetValue(b, out var dictionary))
					cached[b] = dictionary = new Dictionary<string, string>(64);
				if (!dictionary.TryGetValue(d, out string text))
					dictionary[d] = text = PREFIX + b + "_" + d;
				__result = text;
			}
			return cont;
		}
	}
}
