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

using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// The proper way to monitor stuck Duplicants, as a state machine!
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class ClaustrophobiaMonitor : StateMachineComponent<ClaustrophobiaMonitor.
			Instance> {
		/// <summary>
		/// Consolidates "Confined!" notification messages.
		/// </summary>
		/// <param name="present">The available notifications.</param>
		/// <returns>The resulting notification string.</returns>
		private static string ConsolidateConfined(List<Notification> present, object _) {
			return ClaustrophobiaStrings.CONFINED_DESC + present.ReduceMessages(true);
		}

		/// <summary>
		/// Consolidates "Trapped!" notification messages.
		/// </summary>
		/// <param name="present">The available notifications.</param>
		/// <returns>The resulting notification string.</returns>
		private static string ConsolidateTrapped(List<Notification> present, object _) {
			return ClaustrophobiaStrings.TRAPPED_DESC + present.ReduceMessages(true);
		}

		/// <summary>
		/// Creates a notification, with a workaround for the new Automation Update.
		/// </summary>
		/// <param name="title">The notification title.</param>
		/// <param name="type">The notification type.</param>
		/// <param name="consolidate">The function to consolidate stacked notifications.</param>
		/// <param name="onClick">The function to call when the notification is selected.</param>
		/// <returns>The resulting notification.</returns>
		private static Notification CreateNotification(string title, NotificationType type,
				Func<List<Notification>, object, string> consolidate,
				Notification.ClickCallback onClick) {
			return new Notification(title, type, HashedString.Invalid, consolidate,
				null, false, 0.0f, onClick);
		}

		/// <summary>
		/// Returns true if the Duplicant has been pending stuck or trapped for long
		/// enough.
		/// </summary>
		/// <param name="smi">The current claustrophobic state.</param>
		/// <returns>true if the Duplicant has been in the current state for more than the
		/// configured threshold, or false otherwise.</returns>
		private static bool StuckLongEnough(Instance smi) {
			return smi.timeinstate >= Math.Max(1, ClaustrophobiaPatches.Options?.
				StuckThreshold ?? 0);
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			smi.sm.IsConfined.Set(false, smi);
			smi.sm.IsTrapped.Set(false, smi);
			smi.StartSM();
#if DEBUG
			PUtil.LogDebug("Added " + gameObject?.name);
#endif
		}

		/// <summary>
		/// The states for monitoring stuck Duplicants.
		/// </summary>
		public sealed class States : GameStateMachine<States, Instance, ClaustrophobiaMonitor> {
#pragma warning disable CS0649
			/// <summary>
			/// Whether this Duplicant is confined.
			/// </summary>
			internal BoolParameter IsConfined;

			/// <summary>
			/// Whether this Duplicant is trapped.
			/// </summary>
			internal BoolParameter IsTrapped;

			/// <summary>
			/// The state when a Duplicant is able to move freely.
			/// </summary>
			internal State confined;

			/// <summary>
			/// The state when a Duplicant has died and should no longer generate notifications.
			/// </summary>
			internal State dead;

			/// <summary>
			/// The state when a Duplicant is neither trapped nor confined.
			/// </summary>
			internal State free;

			/// <summary>
			/// The state when a Duplicant is about to become confined.
			/// </summary>
			internal State pendingConfined;

			/// <summary>
			/// The state when a Duplicant is about to become trapped.
			/// </summary>
			internal State pendingTrapped;

			/// <summary>
			/// The state when a Duplicant is unable to access their beds, baths, or mess table.
			/// </summary>
			internal State trapped;
#pragma warning restore CS0649

			public override void InitializeStates(out BaseState default_state) {
				default_state = free;
				// free -> confined or trapped if parameters are triggered
				free.ParamTransition(IsConfined, pendingConfined, IsTrue).ParamTransition(
					IsTrapped, pendingTrapped, IsTrue).TagTransition(GameTags.Dead, dead);
				// pending trapped -> reschedule check every second, enter trapped if there
				// for long enough
				pendingTrapped.Update((smi, time) => ClaustrophobiaChecker.Instance?.
					ForceCheckDuplicant(smi.gameObject), UpdateRate.SIM_1000ms).
					Transition(trapped, StuckLongEnough, UpdateRate.SIM_1000ms).
					ParamTransition(IsTrapped, free, IsFalse).TagTransition(GameTags.Dead,
					dead);
				// pending confined -> reschedule check every second, enter confined if there
				// for long enough
				pendingConfined.Update((smi, time) => ClaustrophobiaChecker.Instance?.
					ForceCheckDuplicant(smi.gameObject), UpdateRate.SIM_1000ms).
					Transition(confined, StuckLongEnough, UpdateRate.SIM_1000ms).
					ParamTransition(IsConfined, free, IsFalse).TagTransition(GameTags.Dead,
					dead);
				// trapped -> notification, log, enter free when no longer trapped
				trapped.ToggleNotification((instance) => instance.CreateTrapped()).
					ParamTransition(IsTrapped, free, IsFalse).TagTransition(GameTags.Dead,
					dead).Enter((smi) => smi.LogStuck(ClaustrophobiaStrings.TRAPPED_TITLE)).
					Exit((smi) => smi.LogNoLongerStuck(ClaustrophobiaStrings.TRAPPED_TITLE));
				// confined -> notification, log, enter free when no longer confined
				confined.ToggleNotification((instance) => instance.CreateConfined()).
					ParamTransition(IsConfined, free, IsFalse).TagTransition(GameTags.Dead,
					dead).Enter((smi) => smi.LogStuck(ClaustrophobiaStrings.CONFINED_TITLE)).
					Exit((smi) => smi.LogNoLongerStuck(ClaustrophobiaStrings.CONFINED_TITLE));
#if DEBUG
				dead.Enter((smi) => {
					PUtil.LogDebug("{0} has died".F(smi.master.name));
					smi.StopSM("Dead");
				});
#else
				dead.Enter((smi) => smi.StopSM("Dead"));
#endif
			}
		}

		/// <summary>
		/// The Duplicant-specific parameters of the claustrophobia status checker.
		/// </summary>
		public sealed class Instance : GameStateMachine<States, Instance, ClaustrophobiaMonitor, object>.GameInstance {
			public Instance(ClaustrophobiaMonitor master) : base(master) { }

			/// <summary>
			/// Creates a "confined" notification.
			/// </summary>
			/// <returns>A confined Duplicant notification.</returns>
			internal Notification CreateConfined() {
				return CreateNotification(ClaustrophobiaStrings.CONFINED_TITLE,
					NotificationType.DuplicantThreatening, ConsolidateConfined,
					SelectDuplicant);
			}

			/// <summary>
			/// Creates a "trapped" notification.
			/// </summary>
			/// <returns>A trapped Duplicant notification.</returns>
			internal Notification CreateTrapped() {
				return CreateNotification(ClaustrophobiaStrings.TRAPPED_TITLE,
					NotificationType.Bad, ConsolidateTrapped, SelectDuplicant);
			}

			/// <summary>
			/// Logs when a Duplicant is freed from being trapped or confined.
			/// </summary>
			internal void LogNoLongerStuck(string header) {
				PUtil.LogDebug("{0} is no longer {1}".F(master.name, header));
			}

			/// <summary>
			/// Logs when a Duplicant becomes trapped or confined.
			/// </summary>
			internal void LogStuck(string header) {
				PUtil.LogWarning("{0} is now {1}!".F(master.name, header));
			}

			/// <summary>
			/// Selects trapped or confined duplicants when a notification is clicked.
			/// </summary>
			private void SelectDuplicant(object _) {
				PUtil.CenterAndSelect(master);
			}
		}
	}
}
