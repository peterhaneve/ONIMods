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
using Klei.AI;
using PeterHan.PLib.Core;
using STRINGS;
using System.Text;
using UnityEngine;

using PERSONALITY = STRINGS.UI.DETAILTABS.PERSONALITY;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the Duplicant personality panel to avoid recalculating
	/// so much every time it is updated.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class MinionPersonalityPanelWrapper : KMonoBehaviour, ISim1000ms {
		private const string BULLETSPACE = "  " + Constants.BULLETSTRING;

		/// <summary>
		/// Avoids recreating new strings every update.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static MinionPersonalityPanelWrapper instance;

		/// <summary>
		/// Applies all minion stats panel patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			harmony.Patch(typeof(MinionPersonalityPanel), nameof(MinionPersonalityPanel.
				OnSpawn), prefix: new HarmonyMethod(typeof(MinionPersonalityPanelWrapper),
				nameof(OnSpawn_Prefix)));
			harmony.Patch(typeof(MinionPersonalityPanel).GetMethodSafe(nameof(
				MinionPersonalityPanel.Refresh), false), prefix: new HarmonyMethod(
				typeof(MinionPersonalityPanelWrapper), nameof(Refresh_Prefix)));
			harmony.Patch(typeof(MinionPersonalityPanel), nameof(MinionPersonalityPanel.
				ScheduleUpdate), prefix: new HarmonyMethod(typeof(MinionPersonalityPanelWrapper),
				nameof(ScheduleUpdate_Prefix)));
		}
		
		/// <summary>
		/// Gets a list of effects from an assigned slot in text form.
		/// </summary>
		/// <param name="slotInstance">The equipment slot to query.</param>
		/// <returns>The list of effects in text form for this slot.</returns>
		private static string GetSlotEffects(AssignableSlotInstance slotInstance) {
			var text = CACHED_BUILDER;
			var desc = ListPool<Descriptor, MinionPersonalityPanelWrapper>.Allocate();
			DescriptorAllocPatches.GetAllDescriptors(slotInstance.assignable.gameObject, false,
				desc);
			var effects = DescriptorAllocPatches.GetGameObjectEffects(desc, true);
			int n = effects.Count;
			if (n > 0) {
				text.Clear().AppendLine();
				for (int i = 0; i < n; i++)
					text.Append(BULLETSPACE).AppendLine(effects[i].IndentedText());
			}
			desc.Recycle();
			return text.ToString();
		}

		/// <summary>
		/// Applied before OnSpawn runs.
		/// </summary>
		private static void OnSpawn_Prefix(MinionPersonalityPanel __instance) {
			if (__instance != null)
				__instance.gameObject.AddOrGet<MinionPersonalityPanelWrapper>().panel =
					__instance;
		}

		/// <summary>
		/// Refreshes the Duplicant stats panel.
		/// </summary>
		/// <param name="msp">The Duplicant stats panel to update.</param>
		private static void Refresh(MinionPersonalityPanel msp) {
			var inst = instance;
			if (inst != null) {
				bool changed = inst.SetTarget(msp.selectedTarget);
				var id = inst.id;
				var resume = inst.resume;
				var amenitiesPanel = msp.amenitiesPanel;
				var attributesPanel = msp.attributesPanel;
				var bioPanel = msp.bioPanel;
				var equipmentPanel = msp.equipmentPanel;
				var resumePanel = msp.resumePanel;
				var traitsPanel = msp.traitsPanel;
				var target = inst.target;
				var traits = inst.traits;
				string name = null;
				var modifiers = inst.modifiers;
				if (changed) {
					resumePanel.SetActive(resume != null);
					attributesPanel.SetActive(resume != null);
					bioPanel.SetActive(id != null && resume != null);
					amenitiesPanel.SetActive(id != null);
					equipmentPanel.SetActive(id != null);
					traitsPanel.SetActive(traits != null);
				}
				if (target != null) {
					var text = CACHED_BUILDER;
					name = target.name;
					text.Clear().Append(PERSONALITY.GROUPNAME_RESUME).Replace("{0}",
						StringFormatter.ToUpper(name));
					resumePanel.SetTitle(text.ToString());
				}
				if (id != null && resume != null)
					RefreshBio(bioPanel, id, resume);
				if (traits != null)
					RefreshTraits(traitsPanel, traits);
				if (id != null) {
					RefreshAmenities(amenitiesPanel, id);
					RefreshEquipment(equipmentPanel, id);
				}
				if (resume != null)
					RefreshResume(resumePanel, resume, name);
				if (modifiers != null)
					RefreshAttributes(attributesPanel, modifiers);
			}
		}
		
		/// <summary>
		/// Applied before Refresh runs.
		/// </summary>
		internal static bool Refresh_Prefix(MinionPersonalityPanel __instance) {
			if (__instance.gameObject.activeSelf)
				Refresh(__instance);
			return false;
		}
		
		/// <summary>
		/// Refreshes the Duplicant's amenities.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="id">The currently selected Duplicant's identity.</param>
		private static void RefreshAmenities(CollapsibleDetailContentPanel panel,
			MinionIdentity id) {
			if (!RefreshAssignedItems(panel, id.GetSoleOwner(), id.name))
				panel.SetLabel("NothingAssigned", PERSONALITY.EQUIPMENT.NO_ASSIGNABLES,
					PERSONALITY.EQUIPMENT.NO_ASSIGNABLES_TOOLTIP.Format(id.name));
			panel.Commit();
		}
		
		/// <summary>
		/// A common helper method for refreshing assigned items.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="assigned">The items assigned to this Duplicant.</param>
		/// <param name="name">The name to show in the assignment tooltip.</param>
		/// <returns>true if anything is assigned, or false otherwise.</returns>
		private static bool RefreshAssignedItems(CollapsibleDetailContentPanel panel,
				Assignables assigned, string name) {
			bool hasAssignments = false;
			var text = CACHED_BUILDER;
			var slots = assigned.Slots;
			int n = slots.Count;
			for (int i = 0; i < n; i++) {
				var slotInstance = slots[i];
				if (slotInstance.slot.showInUI && slotInstance.IsAssigned()) {
					string itemName = "", slotName = slotInstance.slot.Name;
					if (slotInstance.assignable.TryGetComponent(out KSelectable item))
						itemName = item.GetName();
					string itemsList = GetSlotEffects(slotInstance);
					panel.SetLabel(slotName, text.Clear().Append(slotName).Append(": ").
						Append(itemName).ToString(), string.Format(PERSONALITY.EQUIPMENT.
						ASSIGNED_TOOLTIP, itemName, itemsList, name));
					hasAssignments = true;
				}
			}
			return hasAssignments;
		}

		/// <summary>
		/// Refreshes the Duplicant's attributes.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="modifiers">The currently selected Duplicant's attributes.</param>
		private static void RefreshAttributes(CollapsibleDetailContentPanel panel,
				Modifiers modifiers) {
			var currentAttr = modifiers.attributes.AttributeTable;
			int n = currentAttr.Count;
			for (int i = 0; i < n; i++) {
				var attrInstance = currentAttr[i];
				if (attrInstance.Attribute.ShowInUI == Attribute.Display.Skill)
					panel.SetLabel(attrInstance.Id, attrInstance.Name + ": " + attrInstance.
						GetFormattedValue(), attrInstance.GetAttributeValueTooltip());
			}
			panel.Commit();
		}
		
		/// <summary>
		/// Refreshes the Duplicant's biography.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="id">The currently selected Duplicant's identity.</param>
		/// <param name="resume">The currently selected Duplicant's skills.</param>
		private static void RefreshBio(CollapsibleDetailContentPanel panel,
				MinionIdentity id, MinionResume resume) {
			var apt = resume.AptitudeBySkillGroup;
			var text = CACHED_BUILDER;
			// Name
			panel.SetLabel("name", DUPLICANTS.NAMETITLE + id.name, "");
			// Age / arrival time
			text.Clear().Append(DUPLICANTS.ARRIVALTIME);
			FormatStringPatches.GetFormattedCycles(text, (GameClock.Instance.GetCycle() -
				id.arrivalTime) * Constants.SECONDS_PER_CYCLE, "F0", true);
			panel.SetLabel("age", text.ToString(), string.Format(DUPLICANTS.
				ARRIVALTIME_TOOLTIP, id.arrivalTime + 1.0f, id.name));
			// Gender
			text.Clear().Append("STRINGS.DUPLICANTS.GENDER.").Append(id.genderStringKey.
				ToUpperInvariant()).Append(".NAME");
			panel.SetLabel("gender", DUPLICANTS.GENDERTITLE + Strings.Get(text.ToString()).
				Format(id.gender), "");
			// Personality
			text.Clear().Append("STRINGS.DUPLICANTS.PERSONALITIES.").Append(id.nameStringKey.
				ToUpperInvariant()).Append(".DESC");
			panel.SetLabel("personality", Strings.Get(text.ToString()).Format(id.name),
				DUPLICANTS.DESC_TOOLTIP.Format(id.name));
			if (apt.Count > 0)
				panel.SetLabel("interestHeader", PERSONALITY.RESUME.APTITUDES.NAME + "\n",
					PERSONALITY.RESUME.APTITUDES.TOOLTIP.Format(id.name));
			foreach (var pair in apt)
				if (pair.Value != 0.0f) {
					var skillGroup = Db.Get().SkillGroups.TryGet(pair.Key);
					if (skillGroup != null)
						panel.SetLabel(skillGroup.Name, BULLETSPACE + skillGroup.Name,
							string.Format(DUPLICANTS.ROLES.GROUPS.APTITUDE_DESCRIPTION,
							skillGroup.Name, pair.Value));
				}
			panel.Commit();
		}

		/// <summary>
		/// Refreshes the Duplicant's equipment.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="id">The currently selected Duplicant's identity.</param>
		private static void RefreshEquipment(CollapsibleDetailContentPanel panel,
				MinionIdentity id) {
			if (!RefreshAssignedItems(panel, id.GetEquipment(), id.name))
				panel.SetLabel("NoSuitAssigned", PERSONALITY.EQUIPMENT.NOEQUIPMENT,
					PERSONALITY.EQUIPMENT.NOEQUIPMENT_TOOLTIP.Format(id.name));
			panel.Commit();
		}

		/// <summary>
		/// Refreshes the Duplicant's resume.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="resume">The currently selected Duplicant's resume.</param>
		/// <param name="name">The currently selected Duplicant's name</param>
		private static void RefreshResume(CollapsibleDetailContentPanel panel,
				MinionResume resume, string name) {
			var text = CACHED_BUILDER;
			int skills = 0;
			panel.SetLabel("mastered_skills_header", PERSONALITY.RESUME.MASTERED_SKILLS,
				PERSONALITY.RESUME.MASTERED_SKILLS_TOOLTIP);
			foreach (var pair in resume.MasteryBySkillID)
				if (pair.Value) {
					var skill = Db.Get().Skills.Get(pair.Key);
					var perks = skill.perks;
					int n = perks.Count;
					text.Clear().AppendLine(skill.description);
					for (int i = 0; i < n; i++)
						text.Append(BULLETSPACE).AppendLine(perks[i].Name);
					panel.SetLabel(skill.Id, BULLETSPACE + skill.Name, text.ToString());
					skills++;
				}
			if (skills == 0)
				panel.SetLabel("no_skills", BULLETSPACE + PERSONALITY.RESUME.
					NO_MASTERED_SKILLS.NAME, PERSONALITY.RESUME.NO_MASTERED_SKILLS.TOOLTIP.
					Format(name));
			panel.Commit();
		}
		
		/// <summary>
		/// Refreshes the Duplicant's traits.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="traits">The currently selected Duplicant's traits.</param>
		private static void RefreshTraits(CollapsibleDetailContentPanel panel, Traits traits) {
			var allTraits = traits.TraitList;
			int n = allTraits.Count;
			for (int i = 0; i < n; i++) {
				var trait = allTraits[i];
				if (!string.IsNullOrEmpty(trait.Name))
					panel.SetLabel(trait.Id, trait.Name, trait.GetTooltip());
			}
			panel.Commit();
		}

		/// <summary>
		/// Applied before ScheduleUpdate runs.
		/// </summary>
		internal static bool ScheduleUpdate_Prefix() {
			return false;
		}
		
		/// <summary>
		/// The Duplicant's identity.
		/// </summary>
		private MinionIdentity id;

		/// <summary>
		/// The Duplicant's attributes (from the modifiers).
		/// </summary>
		private Modifiers modifiers;

		/// <summary>
		/// The vanilla stats screen.
		/// </summary>
		internal MinionPersonalityPanel panel;
		
		/// <summary>
		/// The Duplicant's skill resume.
		/// </summary>
		private MinionResume resume;

		/// <summary>
		/// The currently selected target.
		/// </summary>
		private GameObject target;

		/// <summary>
		/// The Duplicant's traits.
		/// </summary>
		private Traits traits;

		internal MinionPersonalityPanelWrapper() {
		}

		public override void OnCleanUp() {
			id = null;
			modifiers = null;
			panel = null;
			resume = null;
			traits = null;
			target = null;
			instance = null;
			base.OnCleanUp();
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			instance = this;
			target = null;
		}

		/// <summary>
		/// Sets the target selected for the stats screen.
		/// </summary>
		/// <param name="newTarget">The new target for this screen.</param>
		internal bool SetTarget(GameObject newTarget) {
			bool changed = target == null || newTarget != target;
			if (changed) {
				target = newTarget;
				if (newTarget != null) {
					newTarget.TryGetComponent(out id);
					newTarget.TryGetComponent(out modifiers);
					newTarget.TryGetComponent(out resume);
					newTarget.TryGetComponent(out traits);
				} else {
					id = null;
					modifiers = null;
					resume = null;
					traits = null;
				}
			}
			return changed;
		}

		public void Sim1000ms(float dt) {
			if (panel != null && panel.gameObject.activeSelf)
				Refresh(panel);
		}
	}
}
