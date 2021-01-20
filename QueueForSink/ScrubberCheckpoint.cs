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

using PeterHan.PLib.Buildings;
using UnityEngine;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// Forces duplicants to stop if necessary when passing ore scrubbers.
	/// </summary>
	public sealed class ScrubberCheckpoint : WorkCheckpoint<OreScrubber.Work> {
		// Scrubbers are 3x3
		private const int OFFSET = 3;

		/// <summary>
		/// The layer for buildings.
		/// </summary>
		private readonly int buildingLayer;

		public ScrubberCheckpoint() : base() {
			buildingLayer = (int)PBuilding.GetObjectLayer(nameof(ObjectLayer.Building),
				ObjectLayer.Building);
		}

		/// <summary>
		/// Checks for another usable ore scrubber in the specified direction. It is assumed
		/// that the current scrubber already requires a stop in that direction.
		/// </summary>
		/// <param name="dir">true if moving right, or false if moving left.</param>
		/// <returns>false if another working scrubber is available for the Duplicant
		/// to use, or true if no other scrubber is available</returns>
		private bool CheckForOtherScrubber(bool dir) {
			GameObject scrubber = gameObject, nScrub;
			bool stop = true;
			int cell;
			if (scrubber != null && Grid.IsValidCell(cell = Grid.PosToCell(scrubber))) {
				cell = Grid.OffsetCell(cell, new CellOffset(dir ? OFFSET : -OFFSET, 0));
				// Is cell valid?
				if (Grid.IsValidBuildingCell(cell) && (nScrub = Grid.Objects[cell,
						buildingLayer]) != null) {
					var nextScrubber = nScrub.GetComponent<ScrubberCheckpoint>();
					var op = nScrub.GetComponent<Operational>();
					var dc = nScrub.GetComponent<DirectionControl>();
					// Must be immediately next to this one, same type, and working
					stop = scrubber.PrefabID() != nScrub.PrefabID() || op == null || !op.
						IsOperational || dc == null || dc.allowedDirection != direction.
						allowedDirection;
					if (!stop && nextScrubber != null && nextScrubber.inUse && nScrub !=
							scrubber)
						// Check that scrubber for a suitable destination
						stop = nextScrubber.CheckForOtherScrubber(dir);
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
