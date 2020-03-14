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
using System.Collections.Generic;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// Handles Duplicant navigation through an airlock door.
	/// </summary>
	public sealed class AirlockDoorTransitionLayer : TransitionDriver.OverrideLayer {
		/// <summary>
		/// The doors to be opened.
		/// </summary>
		private readonly ICollection<AirlockDoor> doors;

		public AirlockDoorTransitionLayer(Navigator navigator) : base(navigator) {
			doors = new List<AirlockDoor>(4);
		}

		/// <summary>
		/// Adds a door if it is present in this cell.
		/// </summary>
		/// <param name="cell">The cell to check for the door.</param>
		private void AddDoor(int cell) {
			var door = GetDoor(cell);
			if (door != null && !doors.Contains(door))
				doors.Add(door);
		}

		/// <summary>
		/// Checks to see if all airlock doors in the way are open.
		/// </summary>
		/// <returns>true if the Duplicant can now pass, or false otherwise.</returns>
		private bool AreAllDoorsOpen() {
			bool open = true;
			foreach (var door in doors)
				if (door != null && !door.IsOpen) {
					open = false;
					break;
				}
			return open;
		}

		public override void BeginTransition(Navigator navigator, Navigator.
				ActiveTransition transition) {
			base.BeginTransition(navigator, transition);
			int cell = Grid.PosToCell(navigator);
			int targetCell = Grid.OffsetCell(cell, transition.x, transition.y);
			AddDoor(targetCell);
			// If duplicant is inside a tube they are only 1 cell tall
			if (navigator.CurrentNavType != NavType.Tube)
				AddDoor(Grid.CellAbove(targetCell));
			// Include any other offsets
			foreach (var offset in transition.navGridTransition.voidOffsets)
				AddDoor(Grid.OffsetCell(cell, offset));
			// If not open, start a transition with the dupe waiting for the door
			if (doors.Count > 0 && !AreAllDoorsOpen()) {
				transition.anim = navigator.NavGrid.GetIdleAnim(navigator.CurrentNavType);
				transition.isLooping = false;
				transition.end = transition.start;
				transition.speed = 1.0f;
				transition.animSpeed = 1.0f;
				transition.x = 0;
				transition.y = 0;
				transition.isCompleteCB = AreAllDoorsOpen;
			}
			foreach (var door in doors)
				door.Open();
		}

		/*public override void UpdateTransition(Navigator navigator, Navigator.ActiveTransition transition) {
			base.UpdateTransition(navigator, transition);
		}*/

		public override void EndTransition(Navigator navigator, Navigator.
				ActiveTransition transition) {
			base.EndTransition(navigator, transition);
			foreach (var door in doors)
				if (door != null)
					door.Close();
			doors.Clear();
		}

		/// <summary>
		/// Gets the airlock door at the specified cell.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>The airlock door there, or null if no door is there.</returns>
		private AirlockDoor GetDoor(int cell) {
			AirlockDoor door = null;
			if (Grid.HasDoor[cell]) {
				var ad = Grid.Objects[cell, (int)ObjectLayer.Building].
					GetComponentSafe<AirlockDoor>();
				if (ad != null && ad.isSpawned)
					door = ad;
			}
			return door;
		}
	}
}
