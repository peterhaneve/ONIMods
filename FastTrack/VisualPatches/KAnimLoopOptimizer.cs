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

using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Stores a list of all "idle" animations (that have no uv changes on any of their frames)
	/// and uses this to disable frame advance or looping on any of these anims once played.
	/// </summary>
	public sealed class KAnimLoopOptimizer {
		/// <summary>
		/// Animations of this many frames or more are never affected.
		/// </summary>
		public const int LONG_THRESHOLD = 30;

		/// <summary>
		/// Animations of this many frames or fewer are paused instead of run once.
		/// </summary>
		public const int SHORT_THRESHOLD = 5;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static KAnimLoopOptimizer Instance { get; private set; }

		/// <summary>
		/// Compares two animation frames.
		/// </summary>
		/// <param name="groupData">The group data to retrieve the frame elements.</param>
		/// <param name="a">The first frame.</param>
		/// <param name="b">The second frame.</param>
		/// <returns>true if they are equal, or false otherwise.</returns>
		private static bool CompareFrames(KBatchGroupData groupData, ref KAnim.Anim.Frame a,
				ref KAnim.Anim.Frame b) {
			// Each element specifies a frame from a particular symbol, position, tint, and
			// flags
			int ne = a.numElements;
			bool equal = ne == b.numElements;
			if (equal) {
				int startA = a.firstElementIdx, startB = b.firstElementIdx;
				// If they point to the same elements, they are automatically equal
				if (startA != startB)
					for (int i = 0; i < ne && equal; i++) {
						var elementA = groupData.GetFrameElement(i + startA);
						var elementB = groupData.GetFrameElement(i + startB);
						Color colorA = elementA.multColour, colorB = elementB.multColour;
						equal = elementA.symbol == elementB.symbol && colorA == colorB &&
							elementA.symbolIdx == elementB.symbolIdx && elementA.flags ==
							elementB.flags && elementA.transform == elementB.transform &&
							elementA.frame == elementB.frame;
					}
			}
			return equal;
		}

		/// <summary>
		/// Creates the instance and indexes the animations.
		/// </summary>
		internal static void CreateInstance() {
			var inst = new KAnimLoopOptimizer();
			inst.IndexAnims();
			Instance = inst;
		}

		/// <summary>
		/// The animations that are nothing but static idle animations.
		/// </summary>
		private readonly IDictionary<HashedString, AnimWrapper> idleAnims;

		private KAnimLoopOptimizer() {
			idleAnims = new Dictionary<HashedString, AnimWrapper>(128);
		}

		/// <summary>
		/// Optimizes the play mode used for animations.
		/// </summary>
		/// <param name="anim">The anim file that is playing.</param>
		/// <param name="currentMode">The current play mode requested by the game.</param>
		/// <returns>The play mode to use for playing it taking optimizations into account.</returns>
		public KAnim.PlayMode GetAnimState(KAnim.Anim anim, KAnim.PlayMode currentMode) {
			var mode = currentMode;
			if (anim != null && idleAnims.TryGetValue(anim.id, out AnimWrapper status) &&
					status.anim == anim)
				mode = status.veryTrivial ? KAnim.PlayMode.Paused : KAnim.PlayMode.Once;
			return mode;
		}

		/// <summary>
		/// Indexes a particular anim file to see if it makes any progress.
		/// </summary>
		/// <param name="manager">The current batched animation manager.</param>
		/// <param name="data">The file data to check.</param>
		private void IndexAnim(KAnimBatchManager manager, KAnimFileData data) {
			int n = data.animCount;
			var build = data.build;
			var bgd = manager.GetBatchGroupData(build.batchTag);
			for (int i = 0; i < n; i++) {
				// Anim specifies a number of frames from the batch group's frames
				var anim = data.GetAnim(i);
				int start = anim.firstFrameIdx, nf = anim.numFrames, end = start + nf;
				bool trivial = nf < LONG_THRESHOLD;
				var id = anim.id;
				if (nf > 1 && trivial) {
					var firstFrame = bgd.GetFrame(start++);
					trivial = firstFrame.idx >= 0;
					for (int j = start; j < end && trivial; j++) {
						// Frames of the animation are available from the batch group
						var nextFrame = bgd.GetFrame(j);
						trivial = nextFrame.idx >= 0 && CompareFrames(bgd, ref firstFrame,
							ref nextFrame);
					}
				}
				if (trivial && !idleAnims.ContainsKey(id))
					// There are a couple of collisions in ONI, but they are properly handled
					idleAnims.Add(id, new AnimWrapper(anim, nf));
			}
		}

		/// <summary>
		/// Indexes all kanims and finds those that do not actually make any progress.
		/// </summary>
		private void IndexAnims() {
			var manager = KAnimBatchManager.Instance();
			idleAnims.Clear();
			foreach (var file in Assets.Anims) {
				var data = file?.GetData();
				if (data != null && data.build != null)
					IndexAnim(manager, data);
			}
		}

		/// <summary>
		/// Stores the animation trivial state with the anim (to verify against hash
		/// collisions).
		/// </summary>
		private sealed class AnimWrapper {
			/// <summary>
			/// The wrapped anim (which has a file and bank name).
			/// </summary>
			internal readonly KAnim.Anim anim;

			/// <summary>
			/// If the animation is 5 frames or fewer, then it is very trivial and will be
			/// executed paused instead of once.
			/// </summary>
			internal readonly bool veryTrivial;

			internal AnimWrapper(KAnim.Anim anim, int numFrames) {
				this.anim = anim;
				veryTrivial = numFrames <= SHORT_THRESHOLD;
			}

			public override bool Equals(object obj) {
				return obj is AnimWrapper other && anim == other.anim;
			}

			public override int GetHashCode() {
				return anim.id.HashValue;
			}

			public override string ToString() {
				return anim.animFile.name + "." + anim.name;
			}
		}
	}
}
