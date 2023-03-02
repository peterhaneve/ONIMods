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

using HarmonyLib;
using Klei.AI;
using PeterHan.PLib.Core;
using System.Text;
using UnityEngine;

using PERSONALITY = STRINGS.UI.DETAILTABS.PERSONALITY;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the Duplicant statistics panel to avoid recalculating
	/// so much every time it is updated.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class MinionStatsPanelWrapper : KMonoBehaviour, ISim1000ms {
		/// <summary>
		/// Avoids recreating new strings every update.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		private static MinionStatsPanelWrapper instance;

		/// <summary>
		/// Applies all minion stats panel patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			harmony.Patch(typeof(MinionStatsPanel), nameof(MinionStatsPanel.OnSpawn),
				prefix: new HarmonyMethod(typeof(MinionStatsPanelWrapper),
				nameof(OnSpawn_Prefix)));
			harmony.Patch(typeof(MinionStatsPanel), nameof(MinionStatsPanel.Refresh),
				prefix: new HarmonyMethod(typeof(MinionStatsPanelWrapper),
				nameof(Refresh_Prefix)));
			harmony.Patch(typeof(MinionStatsPanel), nameof(MinionStatsPanel.ScheduleUpdate),
				prefix: new HarmonyMethod(typeof(MinionStatsPanelWrapper),
				nameof(ScheduleUpdate_Prefix)));
		}

		/// <summary>
		/// Applied before OnSpawn runs.
		/// </summary>
		private static void OnSpawn_Prefix(MinionStatsPanel __instance) {
			var go = __instance.gameObject;
			if (go != null)
				go.AddOrGet<MinionStatsPanelWrapper>().panel = __instance;
		}

		/// <summary>
		/// Refreshes the Duplicant stats panel.
		/// </summary>
		/// <param name="msp">The Duplicant stats panel to update.</param>
		private static void Refresh(MinionStatsPanel msp) {
			var inst = instance;
			if (inst != null) {
				bool changed = inst.SetTarget(msp.selectedTarget);
				var resume = inst.resume;
				var resumePanel = msp.resumePanel;
				var target = inst.target;
				string name = null;
				var modifiers = inst.modifiers;
				if (changed) {
					var attributesPanel = msp.attributesPanel;
					resumePanel.SetActive(resume != null);
					attributesPanel.SetActive(resume != null);
					attributesPanel.GetComponent<CollapsibleDetailContentPanel>().HeaderLabel.
						SetText(STRINGS.UI.DETAILTABS.STATS.GROUPNAME_ATTRIBUTES);
				}
				if (target != null) {
					var text = CACHED_BUILDER;
					name = target.name;
					text.Clear().Append(PERSONALITY.GROUPNAME_RESUME).Replace("{0}",
						StringFormatter.ToUpper(name));
					resumePanel.GetComponent<CollapsibleDetailContentPanel>().HeaderLabel.
						SetText(text);
				}
				if (resume != null)
					RefreshResume(msp, resume, name);
				if (modifiers != null)
					RefreshAttributes(msp, modifiers);
			}
		}
		
		/// <summary>
		/// Applied before Refresh runs.
		/// </summary>
		internal static bool Refresh_Prefix(MinionStatsPanel __instance) {
			if (__instance.gameObject.activeSelf)
				Refresh(__instance);
			return false;
		}

		/// <summary>
		/// Refreshes the Duplicant's attributes.
		/// </summary>
		/// <param name="msp">The Duplicant stats panel to update.</param>
		/// <param name="modifiers">The currently selected Duplicant's attributes.</param>
		private static void RefreshAttributes(MinionStatsPanel msp, Modifiers modifiers) {
			var drawer = msp.attributesDrawer;
			var currentAttr = modifiers.attributes.AttributeTable;
			int n = currentAttr.Count;
			drawer.BeginDrawing();
			for (int i = 0; i < n; i++) {
				var attrInstance = currentAttr[i];
				if (attrInstance.Attribute.ShowInUI == Attribute.Display.Skill)
					drawer.NewLabel(attrInstance.Name + ": " + attrInstance.
						GetFormattedValue()).Tooltip(attrInstance.GetAttributeValueTooltip());
			}
			drawer.EndDrawing();
		}

		/// <summary>
		/// Refreshes the Duplicant's resume.
		/// </summary>
		/// <param name="msp">The Duplicant stats panel to update.</param>
		/// <param name="resume">The currently selected Duplicant's resume.</param>
		/// <param name="name">The currently selected Duplicant's name</param>
		private static void RefreshResume(MinionStatsPanel msp, MinionResume resume,
				string name) {
			const string BULLETSPACE = " " + Constants.BULLETSTRING;
			var drawer = msp.resumeDrawer;
			var text = CACHED_BUILDER;
			int skills = 0;
			drawer.BeginDrawing();
			drawer.NewLabel(PERSONALITY.RESUME.MASTERED_SKILLS).Tooltip(PERSONALITY.RESUME.
				MASTERED_SKILLS_TOOLTIP);
			foreach (var pair in resume.MasteryBySkillID)
				if (pair.Value) {
					var skill = Db.Get().Skills.Get(pair.Key);
					var perks = skill.perks;
					int n = perks.Count;
					text.Clear();
					text.AppendLine(skill.description);
					for (int i = 0; i < n; i++)
						text.Append(BULLETSPACE).AppendLine(perks[i].Name);
					drawer.NewLabel(BULLETSPACE + skill.Name).Tooltip(text.ToString());
					skills++;
				}
			if (skills == 0)
				drawer.NewLabel(BULLETSPACE + PERSONALITY.RESUME.NO_MASTERED_SKILLS.NAME).
					Tooltip(PERSONALITY.RESUME.NO_MASTERED_SKILLS.TOOLTIP.Format(name));
			drawer.EndDrawing();
		}
		
		/// <summary>
		/// Applied before ScheduleUpdate runs.
		/// </summary>
		internal static bool ScheduleUpdate_Prefix() {
			return false;
		}

		/// <summary>
		/// The Duplicant's attributes (from the modifiers).
		/// </summary>
		private Modifiers modifiers;

		/// <summary>
		/// The vanilla stats screen.
		/// </summary>
		internal MinionStatsPanel panel;

		/// <summary>
		/// The Duplicant's skill resume.
		/// </summary>
		private MinionResume resume;

		/// <summary>
		/// The currently selected target.
		/// </summary>
		private GameObject target;

		internal MinionStatsPanelWrapper() {
		}

		public override void OnCleanUp() {
			modifiers = null;
			panel = null;
			resume = null;
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
					newTarget.TryGetComponent(out modifiers);
					newTarget.TryGetComponent(out resume);
				} else {
					modifiers = null;
					resume = null;
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
