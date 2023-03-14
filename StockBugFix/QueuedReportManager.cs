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

using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Manages queued reports of mod updates.
	/// </summary>
	internal sealed class QueuedReportManager {
		/// <summary>
		/// The time to wait after the last queued report before actually reporting.
		/// 
		/// Only reports on the main menu.
		/// </summary>
		internal const float QUEUED_REPORT_DELAY = 3.0f;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static QueuedReportManager Instance { get; } = new QueuedReportManager();

		/// <summary>
		/// Queues a delayed report. If no more delayed reports occur for 3 seconds after the
		/// main menu loads, the report will be executed.
		/// </summary>
		/// <param name="manager">The active mod manager.</param>
		internal static void QueueDelayedReport(KMod.Manager manager, GameObject _) {
			Instance.QueueReport(false);
		}

		/// <summary>
		/// Queues a delayed sanitize. If no more delayed reports occur for 3 seconds after the
		/// main menu loads, the report will be executed.
		/// </summary>
		/// <param name="manager">The active mod manager.</param>
		internal static void QueueDelayedSanitize(KMod.Manager manager, GameObject _) {
			Instance.QueueReport(true);
		}

		/// <summary>
		/// The last Time.unscaledTime value when a report was generated. 0.0 if no
		/// report is pending.
		/// </summary>
		private float lastReportTime;

		/// <summary>
		/// Manages multithreaded access to this object.
		/// </summary>
		private readonly object reportLock;

		/// <summary>
		/// Whether a sanitization pass was requested by Steam.
		/// </summary>
		private bool sanitizeRequested;

		private QueuedReportManager() {
			lastReportTime = 0.0f;
			reportLock = new object();
			sanitizeRequested = false;
		}

		/// <summary>
		/// Checks to see if a queued report needs to be shown, and if so, clears the
		/// request and shows the report.
		/// </summary>
		/// <param name="parent">The parent of the dialog if necessary.</param>
		internal void CheckQueuedReport(GameObject parent) {
			bool report = false, sanitize = false;
			lock (reportLock) {
				float time = lastReportTime;
				// Due for a report?
				if (time > 0.0f && (Time.unscaledTime - lastReportTime) >
						QUEUED_REPORT_DELAY) {
					sanitize = sanitizeRequested;
					report = true;
					lastReportTime = 0.0f;
					sanitizeRequested = false;
				}
			}
			if (report) {
				// Automatically does nothing if 0 events
				if (sanitize)
					Global.Instance.modManager.Sanitize(parent);
				else
					Global.Instance.modManager.Report(parent);
			}
		}

		/// <summary>
		/// Queues a request for a mod status report dialog.
		/// </summary>
		internal void QueueReport(bool sanitize) {
			lock (reportLock) {
				lastReportTime = Time.unscaledTime;
				if (sanitize)
					sanitizeRequested = true;
			}
		}
	}
}
