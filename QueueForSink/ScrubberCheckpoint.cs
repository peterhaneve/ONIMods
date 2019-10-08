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

using UnityEngine;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// A class for a checkpoint on ore scrubbers.
	/// </summary>
	public sealed class ScrubberCheckpoint : WorkCheckpoint<OreScrubber.Work> {
		// Scrubbers are 3x3
		private const int OFFSET = 3;

		/// <summary>
		/// Checks for another usable ore scrubber in the specified direction. It is assumed
		/// that the current scrubber already requires a stop in that direction.
		/// </summary>
		/// <param name="dir">true if moving right, or false if moving left.</param>
		/// <returns>false if another working scrubber is available for the Duplicant
		/// to use, or true if no other scrubber is available</returns>
		private bool CheckForOtherScrubber(bool dir) {
			var scrubber = gameObject;
			bool stop = true;
			int cell;
			if (scrubber != null && Grid.IsValidCell(cell = Grid.PosToCell(scrubber))) {
				cell = Grid.OffsetCell(cell, new CellOffset(dir ? OFFSET : -OFFSET, 0));
				// Is cell valid?
				if (Grid.IsValidBuildingCell(cell)) {
					var nScrub = Grid.Objects[cell, (int)ObjectLayer.Building];
					// Must be immediately next to this one, same type, and working
					stop = nScrub == null || scrubber.PrefabID() != nScrub.PrefabID() ||
						nScrub.GetComponent<Operational>()?.IsOperational != true ||
						direction.allowedDirection != nScrub.GetComponent<DirectionControl>()?.
						allowedDirection;
				}
			}
			return stop;
		}

		protected override bool MustStop(GameObject reactor, float direction) {
			var storage = reactor.GetComponent<Storage>();
			bool stop = false;
			byte noDisease = Klei.SimUtil.DiseaseInfo.Invalid.idx;
			PrimaryElement element;
			if (storage != null)
				// Search all items, blacklist food, require a disease
				foreach (var item in storage.items)
					if (item != null && (element = item.GetComponent<PrimaryElement>()) !=
							null && element.DiseaseIdx != noDisease && !item.HasTag(
							GameTags.Edible)) {
						stop = true;
						break;
					}
			return stop && CheckForOtherScrubber(direction > 0.0f); 
		}
	}
}
