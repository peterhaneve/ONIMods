/*
 * Copyright 2022 Peter Han
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

using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// Forces duplicants to stop if necessary when passing sink like buildings.
	/// </summary>
	public sealed class SinkCheckpoint : WorkCheckpoint<HandSanitizer.Work> {
		/// <summary>
		/// The layer for buildings.
		/// </summary>
		private readonly int buildingLayer;

#pragma warning disable CS0649
#pragma warning disable IDE0044 // Add readonly modifier
		[MyCmpReq]
		private HandSanitizer handSanitizer;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public SinkCheckpoint() : base() {
			buildingLayer = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Building),
				ObjectLayer.Building);
		}

		/// <summary>
		/// Checks for another usable sink in the specified direction. It is assumed that the
		/// current sink already requires a stop in that direction.
		/// </summary>
		/// <param name="dir">true if moving right, or false if moving left.</param>
		/// <returns>false if another working sink is available for the Duplicant
		/// to use, or true if no other sink is available</returns>
		private bool CheckForOtherSink(bool dir) {
			GameObject sink = gameObject, nSink;
			bool stop = true;
			int cell;
			if (sink != null && Grid.IsValidCell(cell = Grid.PosToCell(sink))) {
				int offset = 2;
				if (sink.TryGetComponent(out Building building))
					offset = building.Def.WidthInCells;
				cell = Grid.OffsetCell(cell, new CellOffset(dir ? offset : -offset, 0));
				if (Grid.IsValidBuildingCell(cell) && (nSink = Grid.Objects[cell,
						buildingLayer]) != null) {
					// Must be immediately next to this one, same type, and working
					stop = sink.PrefabID() != nSink.PrefabID() || !nSink.TryGetComponent(
						out Operational op) || !op.IsOperational || !nSink.TryGetComponent(
						out DirectionControl dc) || dc.allowedDirection != direction.
						allowedDirection;
					if (!stop && nSink.TryGetComponent(out SinkCheckpoint nextSink) &&
							nextSink.inUse && nSink != sink)
						// Check that sink for a suitable destination
						stop = nextSink.CheckForOtherSink(dir);
				}
			}
			return stop;
		}

		/// <summary>
		/// Checks to see if the sink should be used by the specified Duplicant.
		/// </summary>
		/// <param name="dupe">The Duplicant which is passing by.</param>
		/// <returns>true if they should use the sink, or false otherwise.</returns>
		private bool NeedsToUse(GameObject dupe) {
			// CanSanitizeSuit still exists, but is unused!!!
			// If always use, can use
			// Otherwise, use if primary element has a disease
			return handSanitizer.alwaysUse || (dupe.TryGetComponent(out PrimaryElement
				element) && element.DiseaseIdx != Klei.SimUtil.DiseaseInfo.Invalid.idx);
		}

		protected override bool MustStop(GameObject reactor, float direction) {
			return NeedsToUse(reactor) && CheckForOtherSink(direction > 0.0f);
		}
	}
}
