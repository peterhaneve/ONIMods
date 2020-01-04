/*
 * Copyright 2020 Peter Han
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
using System.Reflection;

namespace PeterHan.ResearchQueue {
	/// <summary>
	/// Patches which will be applied via annotations for Research Queue.
	/// </summary>
	public static class ResearchQueuePatches {
		/// <summary>
		/// Caches a reference to the mActiveModifiers field of KInputController.
		/// </summary>
		private static FieldInfo activeModifiers = null;

		public static void OnLoad() {
			PUtil.InitLibrary();
			// Cache a ref to the active modifiers field
			try {
				activeModifiers = typeof(KInputController).GetField("mActiveModifiers",
					BindingFlags.NonPublic | BindingFlags.Instance);
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Adds techs to the queue... in the proper order!
		/// </summary>
		/// <param name="research">The currnet research status.</param>
		/// <param name="queuedTech">The research queue</param>
		/// <param name="tech">The tech to queue.</param>
		private static void AddTechToQueue(IList<TechInstance> queuedTech,
				IDictionary<string, TechInstance> techsToAdd, Tech tech) {
			var tInst = Research.Instance.GetOrAdd(tech);
			string id = tech.Id;
			// Only add techs that have not already been added
			if (!techsToAdd.ContainsKey(id) && !tInst.IsComplete()) {
				bool contains = false;
				// Only add techs not already in the queue
				foreach (var queuedInst in queuedTech)
					if (queuedInst.tech.Id == id) {
						contains = true;
						break;
					}
				if (!contains) {
					// Add dependencies first
					tInst.tech.requiredTech.ForEach((newTech) => AddTechToQueue(queuedTech,
						techsToAdd, newTech));
					techsToAdd.Add(id, tInst);
				}
			}
		}

		/// <summary>
		/// When a research is cancelled, removes from the queue if SHIFT clicked.
		/// </summary>
		/// <param name="entry">The entry that was clicked.</param>
		/// <param name="targetTech">The technology to queue.</param>
		/// <returns>true to remove the technology normally, or false if this method already
		/// removed the technology.</returns>
		private static bool OnResearchCanceled(ResearchEntry entry, Tech targetTech) {
			var controller = Global.Instance.GetInputManager()?.GetDefaultController();
			var screen = ManagementMenu.Instance?.researchScreen as ResearchScreen;
			var research = Research.Instance;
			// If SHIFT is down (have to use reflection!)
			bool shiftDown = activeModifiers != null && (activeModifiers.GetValue(controller)
				is Modifier mods) && mods == Modifier.Shift, cont = true;
			if (research != null && screen != null && !targetTech.IsComplete()) {
#if DEBUG
				PUtil.LogDebug("Dequeue tech: " + targetTech.Name);
#endif
				// Remove from queue
				screen.CancelResearch();
				research.CancelResearch(targetTech);
				var queue = research.GetResearchQueue();
				// Restack research
				int n = queue.Count;
				research.SetActiveResearch((n > 0) ? queue[n - 1].tech : null, false);
				// The original method would immediately deselect this item, avoid that
				cont = false;
			}
			return cont;
		}

		/// <summary>
		/// When a research is clicked, adds to the queue if SHIFT clicked.
		/// </summary>
		/// <param name="entry">The entry that was clicked.</param>
		/// <param name="targetTech">The technology to queue.</param>
		/// <returns>true to add the technology normally, or false if this method already
		/// added the technology.</returns>
		private static bool OnResearchClicked(ResearchEntry entry, Tech targetTech) {
			var controller = Global.Instance.GetInputManager()?.GetDefaultController();
			var screen = ManagementMenu.Instance?.researchScreen as ResearchScreen;
			var research = Research.Instance;
			string id = targetTech.Id;
			// If SHIFT is down (have to use reflection!)
			bool cont = true, shiftDown = activeModifiers != null && (activeModifiers.
				GetValue(controller) is Modifier mods) && mods == Modifier.Shift;
			if (controller != null && research != null && !DebugHandler.InstantBuildMode) {
#if DEBUG
				PUtil.LogDebug("Queue tech: " + targetTech.Name);
#endif
				var queue = research.GetResearchQueue();
				int index = -1, n = queue.Count;
				for (int i = 0; i < n && index < 0; i++)
					// If the user shift clicks a tech already in the queue, remove it
					if (queue[i].tech.Id == id)
						index = i;
				screen.CancelResearch();
				if (index >= 0) {
					// Remove from queue and dynamically restack
					research.CancelResearch(targetTech);
					queue = research.GetResearchQueue();
					n = queue.Count;
					research.SetActiveResearch((n > 0) ? queue[n - 1].tech : null, false);
					cont = false;
				} else if (screen != null) {
					if (shiftDown)
						// If not in the queue already and shift clicked, queue it on the end
						// Reflection would be required no matter which way
						Traverse.Create(research).CallMethod("AddTechToQueue", targetTech);
					research.SetActiveResearch(targetTech, !shiftDown);
					cont = false;
				}
			}
			return cont;
		}

		/// <summary>
		/// Goes through the research screen and updates titles for techs in the queue.
		/// </summary>
		/// <param name="queuedTech">The current research queue.</param>
		private static void UpdateResearchOrder(IList<TechInstance> queuedTech) {
			var screen = ManagementMenu.Instance?.researchScreen as ResearchScreen;
			if (queuedTech == null)
				throw new ArgumentNullException("queuedTech");
			int n = queuedTech.Count;
			if (screen != null) {
				// O(N^2) sucks
				var techIndex = DictionaryPool<string, int, ResearchScreen>.Allocate();
				for (int i = 0; i < n; i++)
					techIndex.Add(queuedTech[i].tech.Id, i + 1);
				foreach (var tech in Db.Get().Techs.resources) {
					var entry = screen.GetEntry(tech);
					// Update all techs with the order count
					if (entry != null) {
						var lt = Traverse.Create(entry).GetField<LocText>("researchName");
						if (techIndex.TryGetValue(tech.Id, out int order))
							lt?.SetText(string.Format(ResearchQueueStrings.QueueFormat, tech.
								Name, order));
						else
							lt?.SetText(tech.Name);
					}
				}
				techIndex.Recycle();
			}
		}

		/// <summary>
		/// Applied to Research to update queue order on techs just added to the queue.
		/// </summary>
		[HarmonyPatch(typeof(Research), "AddTechToQueue")]
		public static class Research_AddTechToQueue_Patch {
			/// <summary>
			/// Applied before AddTechToQueue runs.
			/// </summary>
			internal static bool Prefix(List<TechInstance> ___queuedTech,
					Tech tech) {
				var dict = DictionaryPool<string, TechInstance, Research>.Allocate();
				var techList = ListPool<TechInstance, Research>.Allocate();
				AddTechToQueue(___queuedTech, dict, tech);
				// Sort by tech level and add at end
				techList.AddRange(dict.Values);
				techList.Sort(TechTierSorter.Instance);
				___queuedTech.AddRange(techList);
				// Update display
				UpdateResearchOrder(___queuedTech);
				dict.Recycle();
				techList.Recycle();
				return false;
			}
		}

		/// <summary>
		/// Applied to Research to update queue order on techs when research is cancelled.
		/// </summary>
		[HarmonyPatch(typeof(Research), "CancelResearch")]
		public static class Research_CancelResearch_Patch {
			/// <summary>
			/// Applied after CancelResearch runs.
			/// </summary>
			internal static void Postfix(List<TechInstance> ___queuedTech) {
				UpdateResearchOrder(___queuedTech);
			}
		}

		/// <summary>
		/// Applied to Research to update queue order on techs when research is changed.
		/// </summary>
		[HarmonyPatch(typeof(Research), "SetActiveResearch")]
		public static class Research_SetActiveResearch_Patch {
			/// <summary>
			/// Applied after SetActiveResearch runs. Transpiling this hit problems with
			/// multiple returns/branches.
			/// </summary>
			internal static void Postfix(List<TechInstance> ___queuedTech) {
				UpdateResearchOrder(___queuedTech);
			}

			/// <summary>
			/// Transpiles SetActiveResearch to rip out a Sort call in the middle.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(List<TechInstance>).
					GetMethodSafe("Sort", false, new Type[] {
						typeof(Comparison<TechInstance>)
					}));
			}
		}

		/// <summary>
		/// Applied to ResearchEntry to allow dequeuing upon shift click.
		/// </summary>
		[HarmonyPatch(typeof(ResearchEntry), "OnResearchCanceled")]
		public static class ResearchEntry_OnResearchCanceled_Patch {
			/// <summary>
			/// Applied before OnResearchCanceled runs.
			/// </summary>
			internal static bool Prefix(ResearchEntry __instance, Tech ___targetTech) {
				return OnResearchCanceled(__instance, ___targetTech);
			}
		}

		/// <summary>
		/// Applied to ResearchEntry to allow queuing upon shift click.
		/// </summary>
		[HarmonyPatch(typeof(ResearchEntry), "OnResearchClicked")]
		public static class ResearchEntry_OnResearchClicked_Patch {
			/// <summary>
			/// Applied before OnResearchClicked runs.
			/// </summary>
			internal static bool Prefix(ResearchEntry __instance, Tech ___targetTech) {
				return OnResearchClicked(__instance, ___targetTech);
			}
		}

		/// <summary>
		/// Applied to ResearchEntry to display the help text to add to queue.
		/// </summary>
		[HarmonyPatch(typeof(ResearchEntry), "SetTech")]
		public static class ResearchEntry_SetTech_Patch {
			/// <summary>
			/// Applied after SetTech runs.
			/// </summary>
			internal static void Postfix(LocText ___researchName, Tech newTech) {
				if (newTech != null && ___researchName != null) {
					var tooltip = ___researchName.GetComponent<ToolTip>();
					if (tooltip != null) {
						// Append to existing tooltip
						string text = tooltip.GetMultiString(0);
						string keyCode = GameUtil.GetKeycodeLocalized(KKeyCode.LeftShift) ??
							"SHIFT";
						tooltip.SetSimpleTooltip(text + "\r\n\r\n" + ResearchQueueStrings.
							QueueTooltip.text.F(keyCode));
					}
				}
			}
		}

		/// <summary>
		/// Applied to ResearchScreen to update the tech tree on load.
		/// </summary>
		[HarmonyPatch(typeof(ResearchScreen), "OnSpawn")]
		public static class ResearchScreen_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix() {
				var inst = Research.Instance;
				if (inst != null) {
					// Force a refresh
					var queue = inst.GetResearchQueue();
					int n = queue.Count;
					if (n > 0)
						// If there are techs in the queue, do this no-op to update labels
						inst.SetActiveResearch(queue[n - 1]?.tech, false);
				}
			}
		}

		/// <summary>
		/// Applied to SaveGame to add a component for saving the research queue.
		/// </summary>
		[HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
		public static class SaveGame_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(SaveGame __instance) {
				// Add a component for saving the research queue
				__instance.gameObject?.AddOrGet<SavedResearchQueue>();
			}
		}
	}
}
