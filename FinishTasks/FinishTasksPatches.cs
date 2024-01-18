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

using Database;
using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using System.Collections.Generic;
using UnityEngine;

using FINISHTASK = PeterHan.FinishTasks.FinishTasksStrings.UI.SCHEDULEGROUPS.FINISHTASK;

namespace PeterHan.FinishTasks {
	/// <summary>
	/// Patches which will be applied via annotations for Rest for the Weary.
	/// </summary>
	public sealed class FinishTasksPatches : KMod.UserMod2 {
		/// <summary>
		/// A dummy schedule block type, since the game compares whether schedule types are
		/// different by checking to see if they permit different types of jobs. Even if no
		/// chore actually has this type, it is enough to differentiate FinishTask from Work.
		/// </summary>
		public static ScheduleBlockType FinishBlock { get; private set; }

		/// <summary>
		/// The colors used for displaying the finish task on the schedule.
		/// </summary>
		private static ColorStyleSetting FinishColor;

		/// <summary>
		/// The schedule group to use for finishing tasks.
		/// </summary>
		public static ScheduleGroup FinishTask { get; private set; }

		/// <summary>
		/// The precondition evaluated to see if a Duplicant can start a new Work type task.
		/// </summary>
		private static Chore.Precondition CAN_START_NEW = new Chore.Precondition() {
			id = "PeterHan.FinishTasks.CanStartNewTask",
			description = FinishTasksStrings.DUPLICANTS.CHORES.PRECONDITIONS.
				CAN_START_NEW_TASK,
			fn = CheckStartNew
		};

		/// <summary>
		/// The ID for the IsScheduledTime precondition.
		/// </summary>
		private static string IsScheduledTimeID;

		/// <summary>
		/// Cached reference to Db.Get().ScheduleBlockTypes.Work.
		/// </summary>
		private static ScheduleBlockType Work;

		/// <summary>
		/// Checks to see if a Duplicant can start a new task.
		/// </summary>
		/// <param name="context">The context related to the chore in question.</param>
		/// <param name="targetChore">The current work chore.</param>
		/// <returns>true if new chores can be started, or false otherwise.</returns>
		private static bool CheckStartNew(ref Chore.Precondition.Context context,
				object targetChore) {
			var state = context.consumerState;
			var driver = state.choreDriver;
			var scheduleBlock = state.scheduleBlock;
			var alertStatus = ClusterManager.Instance.GetWorld(driver.GetMyWorldId());
			bool start = true, normal = !alertStatus.IsYellowAlert() && !alertStatus.
				IsRedAlert();
			// Bypass on red/yellow alert, only evaluate condition during Finish Tasks blocks,
			// allow the current chore to continue, or new work chores to be evaluated if the
			// current chore is compulsory like emotes
			if (normal && scheduleBlock != null && scheduleBlock.GroupId == FinishTask.Id) {
				var currentChore = driver.GetCurrentChore();
				// Allow the task that the Duplicant initially was doing to continue even if
				// temporarily interrupted
				var savedChore = driver.TryGetComponent(out FinishChoreDetector detector) ?
					null : (detector.IsAcquiringChore ? currentChore : detector.TaskToFinish);
				start = currentChore != null && (currentChore == context.chore || currentChore.
					masterPriority.priority_class == PriorityScreen.PriorityClass.compulsory ||
					savedChore == context.chore);
			}
			return start;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			FinishBlock = null;
			FinishColor = ScriptableObject.CreateInstance<ColorStyleSetting>();
			FinishColor.activeColor = new Color(0.8f, 0.6f, 1.0f, 1.0f);
			FinishColor.inactiveColor = new Color(0.5f, 0.286f, 1.0f, 1.0f);
			FinishColor.disabledColor = new Color(0.4f, 0.4f, 0.416f, 1.0f);
			FinishColor.disabledActiveColor = new Color(0.6f, 0.588f, 0.625f, 1.0f);
			FinishColor.hoverColor = FinishColor.activeColor;
			FinishColor.disabledhoverColor = new Color(0.48f, 0.46f, 0.5f, 1.0f);
			FinishTask = null;
			IsScheduledTimeID = string.Empty;
			Work = null;
			PUtil.InitLibrary();
			LocString.CreateLocStringKeys(typeof(FinishTasksStrings.DUPLICANTS));
			LocString.CreateLocStringKeys(typeof(FinishTasksStrings.UI));
			new PLocalization().Register();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to Chore to add a precondition for not starting new work chores during
		/// finish tasks blocks.
		/// </summary>
		[HarmonyPatch(typeof(Chore), nameof(Chore.AddPrecondition))]
		public static class Chore_AddPrecondition_Patch {
			/// <summary>
			/// Applied after AddPrecondition runs.
			/// </summary>
			internal static void Postfix(Chore __instance, Chore.Precondition precondition,
					object data) {
				if (precondition.id == IsScheduledTimeID && (data is ScheduleBlockType type) &&
						type == Work)
					// Any task classified as Work gets our finish time precondition
					__instance.AddPrecondition(CAN_START_NEW, __instance);
			}
		}

		/// <summary>
		/// Applied to MingleMonitor to schedule our Finish Mingle chore during Finish Tasks
		/// time.
		/// </summary>
		[HarmonyPatch(typeof(MingleMonitor), nameof(MingleMonitor.InitializeStates))]
		public static class MingleMonitor_InitializeStates_Patch {
			/// <summary>
			/// Creates a Finish Tasks Mingle chore.
			/// </summary>
			private static Chore CreateMingleChore(MingleMonitor.Instance smi) {
				return new FinishMingleChore(smi.master);
			}

			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(MingleMonitor __instance) {
				__instance.mingle.ToggleRecurringChore(CreateMingleChore);
			}
		}

		/// <summary>
		/// Applied to MinionConfig to add a task completion sensor to each Duplicant.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.CreatePrefab))]
		public static class MinionConfig_CreatePrefab_Patch {
			/// <summary>
			/// Applied after CreatePrefab runs.
			/// </summary>
			internal static void Postfix(GameObject __result) {
				if (__result != null)
					__result.AddOrGet<FinishChoreDetector>();
			}
		}

		/// <summary>
		/// Applied to ScheduleBlockTypes to create and add our dummy chore type.
		/// </summary>
		[HarmonyPatch(typeof(ScheduleBlockTypes), MethodType.Constructor, typeof(ResourceSet))]
		public static class ScheduleBlockTypes_Constructor_Patch {
			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(ScheduleBlockTypes __instance) {
				var color = FinishColor != null ? FinishColor.activeColor : Color.green;
				// The type and color are not used by the base game
				FinishBlock = __instance.Add(new ScheduleBlockType(FINISHTASK.ID, __instance,
					FINISHTASK.NAME, FINISHTASK.DESCRIPTION, color));
				// Allow localization to update the string (this is running in Db.Initialize)
				CAN_START_NEW.description = FinishTasksStrings.DUPLICANTS.CHORES.
					PRECONDITIONS.CAN_START_NEW_TASK;
			}
		}

		/// <summary>
		/// Applied to ScheduleGroups to add our new block type.
		/// </summary>
		[HarmonyPatch(typeof(ScheduleGroups), MethodType.Constructor, typeof(ResourceSet))]
		public static class ScheduleGroups_Constructor_Patch {
			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(ScheduleGroups __instance) {
				Work = Db.Get().ScheduleBlockTypes.Work;
				if (Work == null || FinishBlock == null)
					PUtil.LogError("Schedule block types undefined for FinishTask group!");
				else
					// Default schedule does not contain this type
					FinishTask = __instance.Add(FINISHTASK.ID, 0, FINISHTASK.NAME, FINISHTASK.
						DESCRIPTION, FINISHTASK.NOTIFICATION_TOOLTIP,
						new List<ScheduleBlockType> {
							Work, FinishBlock
						});
				IsScheduledTimeID = ChorePreconditions.instance.IsScheduledTime.id;
			}
		}

		/// <summary>
		/// Applied to ScheduleScreen to add a color for our new block.
		/// </summary>
		[HarmonyPatch(typeof(ScheduleScreen), "OnPrefabInit")]
		public static class ScheduleScreen_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(Dictionary<string, ColorStyleSetting> ___paintStyles)
			{
				if (___paintStyles != null && FinishColor != null)
					___paintStyles[FINISHTASK.ID] = FinishColor;
			}
		}
	}
}
