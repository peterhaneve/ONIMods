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

using KMod;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Describes mod events in a user-friendly way.
	/// </summary>
	public sealed class ModEvents {
		/// <summary>
		/// The maximum number of total events.
		/// </summary>
		public const int MAX_EVENTS = 30;

		/// <summary>
		/// In the first pass over events, Describe will try to allocate at least this many
		/// mods per event type.
		/// </summary>
		private const int SMALL_EVENT_THRESHOLD = 4;

		/// <summary>
		/// Creates friendly description text for mod events.
		/// </summary>
		/// <param name="events">The events which occurred.</param>
		/// <returns>A textual description of the events.</returns>
		public static string Describe(IList<Event> events) {
			var message = new StringBuilder();
			if ((events?.Count ?? 0) > 0) {
				var ti = CultureInfo.CurrentCulture.TextInfo;
				var modEvents = new ModEvents(events);
				// Clean up extraneous events
				modEvents.DedupeEvents();
				message.AppendLine();
				// Determine how many events can be shown per category
				var types = modEvents.EventTypes;
				int between = types.Count;
				if (between > 0) {
					int leftover = MAX_EVENTS, perType = Math.Max(SMALL_EVENT_THRESHOLD,
						leftover / between);
					// If any events have very few items, give their remaining space to others
					foreach (var type in types) {
						int count = modEvents.GetEventsOfType(type)?.Count ?? 0;
						if (count < perType) {
							between--;
							leftover -= count;
						}
					}
					if (between < 1)
						perType = MAX_EVENTS;
					else
						perType = Math.Max(1, leftover / between);
					// For each event type, list its elements
					foreach (var type in types) {
						int count = perType;
						message.Append("<size=16>");
						message.Append(GetProperTitle(type, ti));
						message.AppendLine("</size>:");
						// Append the events
						foreach (var evt in modEvents.GetEventsOfType(type)) {
							message.Append(evt.mod.title);
							// More information for some events
							if (!string.IsNullOrEmpty(evt.details)) {
								message.Append(" (");
								message.Append(evt.details);
								message.Append(")");
							}
							message.AppendLine();
							// ... if too many
							if (--count <= 0) {
								message.AppendLine(STRINGS.UI.FRONTEND.MOD_DIALOGS.
									ADDITIONAL_MOD_EVENTS);
								break;
							}
						}
					}
				}
			}
			return message.ToString();
		}

		/// <summary>
		/// Creates a friendly title for the event.
		/// </summary>
		/// <param name="type">The event type.</param>
		/// <param name="ti">The current culture if available.</param>
		/// <returns>The header for events of that type.</returns>
		private static string GetProperTitle(EventType type, TextInfo ti = null) {
			Event.GetUIStrings(type, out string title, out string _);
			if (ti != null)
				title = ti.ToTitleCase(ti.ToLower(title));
			// Green for installed and red for uninstalled / crashed
			if (type == EventType.ActiveDuringCrash || type == EventType.NotFound)
				title = STRINGS.UI.FormatAsAutomationState(title, STRINGS.UI.
					AutomationState.Standby);
			else if (type == EventType.ExpectedActive)
				title = DebugNotIncludedStrings.UI.MODEVENTS.DEACTIVATED;
			else if (type == EventType.LoadError)
				title = DebugNotIncludedStrings.UI.MODEVENTS.NOTLOADED;
			else if (type == EventType.ExpectedInactive)
				title = DebugNotIncludedStrings.UI.MODEVENTS.ACTIVATED;
			else
				// Bold the title
				title = "<b><color=#DEDEFF>" + title + "</color></b>";
			return title;
		}

		/// <summary>
		/// The total number of events.
		/// </summary>
		public int EventCount { get; private set; }

		/// <summary>
		/// Gets the types of events which occurred. Sorted by event type order as declared in
		/// the EventType enum.
		/// </summary>
		public ICollection<EventType> EventTypes {
			get {
				return eventsByType.Keys;
			}
		}

		/// <summary>
		/// Categorizes events by type.
		/// </summary>
		private readonly IDictionary<EventType, IList<Event>> eventsByType;

		public ModEvents() {
			eventsByType = new SortedList<EventType, IList<Event>>(MAX_EVENTS * 4);
			EventCount = 0;
		}

		public ModEvents(IEnumerable<Event> events) : this() {
			if (events == null)
				throw new ArgumentNullException("events");
			foreach (var modEvent in events)
				AddEvent(modEvent);
		}

		/// <summary>
		/// Adds a mod event.
		/// </summary>
		/// <param name="evt">The event which occurred.</param>
		public void AddEvent(Event evt) {
			var type = evt.event_type;
			if (!eventsByType.TryGetValue(type, out IList<Event> eventsOfType))
				eventsByType[type] = eventsOfType = new List<Event>(MAX_EVENTS);
			eventsOfType.Add(evt);
			EventCount++;
		}

		/// <summary>
		/// Deduplicates events of the specified type.
		/// 
		/// If ExpectedActive, ExpectedInactive, VersionUpdate, or Deactivate events also
		/// reference the same mod, the duplicate event will be removed.
		/// </summary>
		/// <param name="type">The type of events to remove if duplicated.</param>
		private void Dedupe(EventType type) {
			if (eventsByType.TryGetValue(type, out IList<Event> victims)) {
				int n = victims.Count;
				var allEvents = ListPool<Event, ModEvents>.Allocate();
				// Events for Add, Remove, Update, and Reinstall
				eventsByType.TryGetValue(EventType.ExpectedActive, out IList<Event> addEvents);
				if (addEvents != null)
					allEvents.AddRange(addEvents);
				eventsByType.TryGetValue(EventType.ExpectedInactive, out addEvents);
				if (addEvents != null)
					allEvents.AddRange(addEvents);
				eventsByType.TryGetValue(EventType.Deactivated, out addEvents);
				if (addEvents != null)
					allEvents.AddRange(addEvents);
				eventsByType.TryGetValue(EventType.VersionUpdate, out addEvents);
				if (addEvents != null)
					allEvents.AddRange(addEvents);
				for (int i = 0; i < n; i++) {
					var mod = victims[i].mod;
					// Is this item included in the events that imply it?
					foreach (var evt in allEvents)
						if (mod.Match(evt.mod)) {
							victims.RemoveAt(i--);
							n--;
							EventCount--;
							break;
						}
				}
				if (n == 0)
					// All gone!
					eventsByType.Remove(type);
				allEvents.Recycle();
			}
		}

		/// <summary>
		/// Deduplicates "restart required" and "ordering changed" events, removing them if
		/// Add or Remove events name the same mod.
		/// </summary>
		public void DedupeEvents() {
			Dedupe(EventType.RestartRequested);
			Dedupe(EventType.OutOfOrder);
		}

		/// <summary>
		/// Returns the events of a particular type.
		/// </summary>
		/// <param name="type">The event type to query.</param>
		/// <returns>The events of that type, or null if no events occurred of that type.</returns>
		public ICollection<Event> GetEventsOfType(EventType type) {
			if (!eventsByType.TryGetValue(type, out IList<Event> eventsOfType))
				eventsOfType = null;
			return eventsOfType;
		}

		public override string ToString() {
			return string.Format("ModEvents[{0:D} events]", EventCount);
		}
	}
}
