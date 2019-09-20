/*
 * Copyright 2019 Peter Han
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
using UnityEngine;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Abstract parent of all notification related to duplicants being stuck.
	/// </summary>
	abstract class AbstractStuckNotification : MonoBehaviour {
		/// <summary>
		/// The notification message.
		/// </summary>
		protected abstract string Message { get; }

		/// <summary>
		/// The severity of this message.
		/// </summary>
		protected abstract NotificationType Severity { get; }

		/// <summary>
		/// The notification title.
		/// </summary>
		protected abstract string Title { get; }

		/// <summary>
		/// The victim of this notification.
		/// </summary>
		protected MinionIdentity Victim {
			get {
				return gameObject?.GetComponent<MinionIdentity>();
			}
		}
		
		/// <summary>
		/// The notification which will be shown.
		/// </summary>
		private Notification notification;
		/// <summary>
		/// Whether the notification is currently visible.
		/// </summary>
		private bool visible;

		/// <summary>
		/// Creates a notification which will track stuck duplicants.
		/// </summary>
		/// <param name="victim">The duplicant which has become confined or trapped.</param>
		public AbstractStuckNotification() {
			notification = null;
			visible = false;
		}

		/// <summary>
		/// Consolidates notification messages.
		/// </summary>
		/// <param name="present">The available notifications.</param>
		/// <param name="data">The data passed to the tooltip.</param>
		/// <returns>The resulting notification string.</returns>
		private string Consolidate(List<Notification> present, object data) {
			return Message + present.ReduceMessages(true);
		}

		/// <summary>
		/// Unsubscribes this class if the Duplicant dies.
		/// </summary>
		/// <param name="parameter">The event parameter (unused).</param>
		private void DestroyNotification(object parameter) {
			Hide();
		}

		/// <summary>
		/// Gets the notification to be displayed.
		/// </summary>
		/// <returns>The notification for trapped or confined duplicant.</returns>
		private Notification GetNotification() {
			if (notification == null)
				notification = new Notification(Title, Severity, HashedString.Invalid,
					Consolidate, null, false, 0.0f, SelectDuplicant, null);
			return notification;
		}

		/// <summary>
		/// Hides the notification.
		/// </summary>
		public void Hide() {
			var victim = Victim;
			if (visible && victim != null) {
				PUtil.LogDebug("{0} is no longer {1}".F(victim.name, Title));
				// Unsubscribe from duplicant death notifications
				gameObject.GetComponent<Notifier>()?.Remove(GetNotification());
				victim.Unsubscribe((int)GameHashes.QueueDestroyObject, DestroyNotification);
				victim.Unsubscribe((int)GameHashes.Died, DestroyNotification);
				visible = false;
			}
		}

		/// <summary>
		/// Called when this object is destroyed.
		/// </summary>
		public void OnDestroy() {
#if DEBUG
			PUtil.LogDebug("Destroying stuck notification");
#endif
			gameObject?.GetComponent<Notifier>()?.Remove(GetNotification());
		}

		/// <summary>
		/// Selects trapped duplicants on notification select.
		/// </summary>
		/// <param name="parameter">The duplicant which is trapped.</param>
		protected void SelectDuplicant(object parameter) {
			PUtil.CenterAndSelect(Victim);
		}

		/// <summary>
		/// Shows the notification.
		/// </summary>
		public void Show() {
			var victim = Victim;
			if (!visible && victim != null) {
				PUtil.LogWarning("{0} is now {1}!".F(victim.name, Title));
				// Subscribe to duplicant death notifications
				gameObject.GetComponent<Notifier>()?.Add(GetNotification());
				victim.Subscribe((int)GameHashes.QueueDestroyObject, DestroyNotification);
				victim.Subscribe((int)GameHashes.Died, DestroyNotification);
				visible = true;
			}
		}

		public override string ToString() {
			return "{0} notification for {1}".F(Title, Victim?.name ?? "Unknown");
		}
	}
}
