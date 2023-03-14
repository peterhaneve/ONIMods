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

using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;

using DAMAGEPOPS = STRINGS.UI.GAMEOBJECTEFFECTS.DAMAGE_POPS;

namespace PeterHan.ToastControl {
	/// <summary>
	/// Controls when individual popups are shown or hidden.
	/// </summary>
	internal sealed class ToastControlPopups {
		/// <summary>
		/// The current options for the mod.
		/// </summary>
		internal static ToastControlOptions Options { get; private set; } =
			new ToastControlOptions();

		/// <summary>
		/// Used on functions that determine whether a notification is shown.
		/// </summary>
		internal delegate bool ShowFunc(object parent, string message);

		/// <summary>
		/// Maps type full names (not AQN, just type name) to the function that controls their
		/// visibility.
		/// </summary>
		private static readonly IDictionary<string, ShowFunc> SHOW_FUNCS = new
				Dictionary<string, ShowFunc>() {
			{ "Klei.AI.AttributeLevel", (c, t) => Options.AttributeIncrease },
			{ nameof(BaseUtilityBuildTool), ShowInsufficient },
			{ "Beefinery+States", ShowElementRemoved },
			{ nameof(BuildingHP), ShowBuildingDamage },
			{ nameof(BuildTool), ShowInsufficient },
			{ nameof(CaptureTool), (c, t) => Options.CannotCapture },
			{ nameof(Constructable), (c, t) => Options.BuildingComplete },
			{ nameof(CopyBuildingSettings), (c, t) => Options.CopySettings },
			{ "CreatureCalorieMonitor+Stomach", ShowCritterPoop },
			{ nameof(DebugHandler), ShowInvalidLocation },
			{ "Klei.AI.EffectInstance", (c, t) => Options.EffectAdded },
			{ "ElementDropperMonitor+Instance", ShowCritterPoop },
			{ nameof(ElementEmitter), ShowElementDropped }, // no uses?
			{ nameof(FleeStates), (c, t) => Options.Fleeing },
			{ nameof(FlushToilet), ShowGermsAdded },
			{ nameof(HarvestDesignatable), (c, t) => Options.HarvestToggle },
			{ nameof(MinionResume), (c, t) => Options.SkillPointEarned },
			{ nameof(Moppable), (c, t) => Options.ElementMopped },
			{ nameof(MopTool), ShowMopError },
			{ nameof(NuclearResearchCenterWorkable), ShowResearchGained },
			{ "PeeChore+States", ShowRadiationRemoved },
			{ nameof(ReorderableBuilding), ShowInvalidLocation },
			{ nameof(ResearchCenter), ShowResearchGained },
			{ nameof(ResearchPointObject), ShowResearchGained },
			{ nameof(RotPile), ShowFoodRotted },
			{ nameof(Rottable), ShowFoodRotted },
			{ nameof(SandboxClearFloorTool), (c, t) => Options.FloorCleared },
			{ nameof(SandboxSampleTool), ShowInvalidLocation },
			{ nameof(SeedProducer), ShowItemGained },
			{ nameof(SetLocker), ShowItemGained },
			{ "Klei.AI.SlimeSickness+SlimeLungComponent+StatesInstance", ShowCritterPoop },
			{ "Klei.AI.SicknessInstance+StatesInstance", ShowCureOrInfect },
			{ "SolidConsumerMonitor+Instance", ShowElementRemoved },
			{ nameof(Storage), ShowItemStored },
			{ nameof(SuperProductive), ShowOverjoyed },
			{ nameof(Toilet), ShowGermsAdded },
			{ nameof(ToiletWorkableUse), ShowRadiationRemoved },
			{ nameof(UtilityBuildTool), (c, t) => Options.InvalidConnection },
			{ "VomitChore+States", ShowRadiationRemoved },
			{ nameof(WorldDamage), (c, t) => Options.ElementDug }
		};

		/// <summary>
		/// Reloads the mod options.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		public static void ReloadOptions() {
			var newOptions = POptions.ReadSettings<ToastControlOptions>();
			if (newOptions != null) {
				PUtil.LogDebug("Reloaded options for Popup Control");
				Options = newOptions;
			}
		}

		private static bool ShowBuildingDamage(object _, string text) {
			bool show = Options.DamageOther;
			if (text.Contains(DAMAGEPOPS.WRONG_ELEMENT))
				show = Options.DamageInput;
			else if (text.Contains(DAMAGEPOPS.OVERHEAT))
				show = Options.DamageOverheat;
			else if (text.Contains(DAMAGEPOPS.CIRCUIT_OVERLOADED))
				show = Options.DamageOverload;
			else if (text.Contains(DAMAGEPOPS.COMET) || text.Contains(DAMAGEPOPS.
					MICROMETEORITE))
				show = Options.DamageMeteor;
			else if (text.Contains(DAMAGEPOPS.CONDUIT_CONTENTS_BOILED) || text.Contains(
					DAMAGEPOPS.CONDUIT_CONTENTS_FROZE))
				show = Options.DamagePipe;
			else if (text.Contains(DAMAGEPOPS.LIQUID_PRESSURE))
				show = Options.DamagePressure;
			else if (text.Contains(DAMAGEPOPS.LOGIC_CIRCUIT_OVERLOADED))
				show = Options.DamageLogic;
			else if (text.Contains(DAMAGEPOPS.MINION_DESTRUCTION))
				show = Options.DamageAnger;
			else if (text.Contains(DAMAGEPOPS.ROCKET))
				show = Options.DamageRocket;
			return show;
		}

		private static bool ShowCritterPoop(object _, string text) => Options.CritterDrops;

		private static bool ShowCureOrInfect(object _, string text) {
			return text.Contains(string.Format(STRINGS.DUPLICANTS.DISEASES.CURED_POPUP, "")) ?
				Options.DiseaseCure : Options.DiseaseInfect;
		}

		private static bool ShowElementDropped(object _, string text) => Options.ElementDropped;

		private static bool ShowElementRemoved(object _, string text) => Options.ElementRemoved;

		private static bool ShowFoodRotted(object _, string text) => Options.FoodDecayed;

		private static bool ShowGermsAdded(object _, string text) => Options.GermsAdded;

		private static bool ShowInsufficient(object _, string text) => Options.
			InsufficientMaterials;

		private static bool ShowInvalidLocation(object _, string text) => Options.
			InvalidLocation;

		private static bool ShowItemGained(object _, string text) => Options.ItemGained;

		private static bool ShowItemStored(object sender, string text) {
			var storage = sender as Storage;
			return (storage == null || storage.fxPrefix == Storage.FXPrefix.Delivered) ?
				Options.Delivered : Options.PickedUp;
		}

		private static bool ShowMopError(object _, string text) {
			return (text == STRINGS.UI.TOOLS.MOP.NOT_ON_FLOOR) ? Options.MopNotFloor : Options.
				MopTooMuch;
		}

		private static bool ShowOverjoyed(object _, string text) => Options.Overjoyed;

		private static bool ShowRadiationRemoved(object _, string text) => Options.
			RadiationRemoved;

		private static bool ShowResearchGained(object _, string text) => Options.ResearchGained;

		/// <summary>
		/// Determines whether a popup should be shown.
		/// </summary>
		/// <param name="parent">The source of the popup.</param>
		/// <param name="message">The original popup message.</param>
		/// <returns>true to show the popup, or false to hide it.</returns>
		public static bool ShowPopup(object parent, string message) {
			bool show = Options.GlobalEnable;
			Type parentType;
#pragma warning disable IDE0031 // Use null propagation
			if (parent is UnityEngine.Object unityParent)
				// GetType() NREs on disposed objects?
				parentType = (unityParent == null) ? null : unityParent.GetType();
			else if (parent is Type type)
				parentType = type;
			else
				parentType = parent?.GetType();
#pragma warning restore IDE0031
			if (parentType != null) {
				string type = parentType.FullName;
				// Skip internal delegate classes
				int index = type.IndexOf('<');
				if (index > 0) {
					// Back up to the previous dot before then, if present
					int lastDot = type.LastIndexOf('.', index);
					if (lastDot > 0) index = lastDot;
					type = type.Substring(0, index);
				}
				if (show) {
					if (SHOW_FUNCS.TryGetValue(type, out ShowFunc method))
						show = method.Invoke(parent, message);
					else {
						// Debug: Flag popup with no handler
#if DEBUG
						PUtil.LogWarning("Popup from {0} => \"{1}\" has no handler defined".F(
							parent == null ? "null" : parent.GetType().FullName, message));
#endif
					}
				}
			}
			return show;
		}
	}
}
