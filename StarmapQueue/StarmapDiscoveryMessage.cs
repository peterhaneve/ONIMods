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

using System;

namespace PeterHan.StarmapQueue {
	/// <summary>
	/// A message emitted when a starmap destination is discovered.
	/// </summary>
	public sealed class StarmapDiscoveryMessage : Message {
		/// <summary>
		/// The destination which was just discovered.
		/// </summary>
		private readonly SpaceDestination destination;

		public StarmapDiscoveryMessage(SpaceDestination destination) {
			this.destination = destination ?? throw new ArgumentNullException("destination");
			if (destination.id == -1)
				throw new ArgumentException("Invalid space destination");
		}

		public override string GetSound() {
			return "AI_Notification_ResearchComplete";
		}

		public override string GetMessageBody() {
			string destType = destination.type, destName = null;
			if (destType != null) {
				destName = Db.Get().SpaceDestinationTypes.Get(destType)?.Name;
				// Link it if possible
				if (destName != null)
					destName = STRINGS.UI.FormatAsLink(destName, destType.ToUpperInvariant());
			}
			return string.Format(StarmapQueueStrings.STARMAP_DISCOVERY_BODY, destName ??
				StarmapQueueStrings.STARMAP_DISCOVERY_UNKNOWN);
		}

		public override string GetTitle() {
			return StarmapQueueStrings.STARMAP_DISCOVERY_TITLE;
		}

		public override string GetTooltip() {
			return GetMessageBody();
		}

		public override bool IsValid() {
			return true;
		}
	}
}
