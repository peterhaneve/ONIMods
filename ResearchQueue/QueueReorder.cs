/*
 * Copyright 2026 Peter Han, Nick Weedon
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
using System.Collections.Generic;

namespace PeterHan.ResearchQueue {
	/// <summary>
	/// Reorders a research queue while keeping it a valid research order. Moving an item
	/// carries its chain with it: moving up stacks its queued prerequisites directly above
	/// the insertion point, and moving down pushes its queued dependents directly below it.
	///
	/// The queue must be closed under unfinished prerequisites (every prerequisite of a
	/// queued item is either queued before it or already researched), as the game keeps
	/// it; chains are only traced through queued items.
	///
	/// Pure logic with no game types.
	/// </summary>
	public static class QueueReorder {
		/// <summary>
		/// Converts a drop slot ("insert before row k", 0 .. Count) into the target index
		/// used by Move and GetChain (the index in the queue with the moved item removed).
		/// </summary>
		public static int SlotToIndex(int fromIndex, int insertBefore) {
			return insertBefore > fromIndex ? insertBefore - 1 : insertBefore;
		}

		/// <summary>
		/// Returns the other items that would travel with the moved item: its queued
		/// transitive prerequisites that it passes when moving up, or its queued transitive
		/// dependents that it passes when moving down. Empty if nothing else moves.
		/// </summary>
		/// <param name="queue">The current queue (must be a valid order).</param>
		/// <param name="fromIndex">The index of the item to move.</param>
		/// <param name="toIndex">The target index, counted in the queue with the item
		/// removed (0 .. Count - 1). Clamped into range.</param>
		/// <param name="prerequisites">Returns the direct prerequisites of an item. Items
		/// not in the queue are ignored.</param>
		public static HashSet<T> GetChain<T>(IList<T> queue, int fromIndex, int toIndex,
				Func<T, IEnumerable<T>> prerequisites) {
			Validate(queue, fromIndex, prerequisites);
			toIndex = Clamp(toIndex, queue.Count);
			var chain = new HashSet<T>();
			var item = queue[fromIndex];
			if (toIndex < fromIndex) {
				// Up: the item passes queue[toIndex .. fromIndex - 1]; any transitive
				// prerequisite in that range comes with it
				var passed = new HashSet<T>();
				for (int i = toIndex; i < fromIndex; i++)
					passed.Add(queue[i]);
				var pending = new Stack<T>();
				pending.Push(item);
				while (pending.Count > 0)
					foreach (var pre in prerequisites(pending.Pop()) ?? Array.Empty<T>())
						if (pre != null && passed.Contains(pre) && chain.Add(pre))
							pending.Push(pre);
			} else if (toIndex > fromIndex) {
				// Down: the item passes queue[fromIndex + 1 .. toIndex]; any item there that
				// depends on the item (or on something already following it) comes along.
				// A valid order lists prerequisites first, so one forward pass suffices.
				var moving = new HashSet<T> { item };
				for (int i = fromIndex + 1; i <= toIndex; i++) {
					var cur = queue[i];
					foreach (var pre in prerequisites(cur) ?? Array.Empty<T>())
						if (pre != null && moving.Contains(pre)) {
							moving.Add(cur);
							chain.Add(cur);
							break;
						}
				}
			}
			return chain;
		}

		/// <summary>
		/// Computes the queue that results from moving one item, with its chain.
		/// </summary>
		/// <param name="queue">The current queue (must be a valid order). Not modified.</param>
		/// <param name="fromIndex">The index of the item to move.</param>
		/// <param name="toIndex">The target index, counted in the queue with the item
		/// removed (0 .. Count - 1). Clamped into range. Moving up, the chain is stacked
		/// starting at this slot with the item directly below it; moving down, the item goes
		/// after the item currently at this slot, with its dependents directly below it.</param>
		/// <param name="prerequisites">Returns the direct prerequisites of an item.</param>
		/// <returns>The reordered queue, as a new list.</returns>
		public static List<T> Move<T>(IList<T> queue, int fromIndex, int toIndex,
				Func<T, IEnumerable<T>> prerequisites) {
			var chain = GetChain(queue, fromIndex, toIndex, prerequisites);
			int n = queue.Count;
			toIndex = Clamp(toIndex, n);
			var result = new List<T>(n);
			if (toIndex == fromIndex) {
				result.AddRange(queue);
				return result;
			}
			bool up = toIndex < fromIndex;
			// The moving block keeps the original relative order, which is already valid
			var block = new List<T>(chain.Count + 1);
			int insertAt = 0;
			for (int i = 0; i < n; i++) {
				var q = queue[i];
				if (i == fromIndex || chain.Contains(q))
					block.Add(q);
				else {
					result.Add(q);
					// Up: everything before the target slot stays above the block.
					// Down: everything passed over (through queue[toIndex]) ends up above it.
					if (up ? i < toIndex : i <= toIndex)
						insertAt++;
				}
			}
			result.InsertRange(insertAt, block);
			return result;
		}

		private static int Clamp(int index, int count) {
			return index < 0 ? 0 : (index > count - 1 ? count - 1 : index);
		}

		private static void Validate<T>(IList<T> queue, int fromIndex,
				Func<T, IEnumerable<T>> prerequisites) {
			if (queue == null)
				throw new ArgumentNullException(nameof(queue));
			if (prerequisites == null)
				throw new ArgumentNullException(nameof(prerequisites));
			if (fromIndex < 0 || fromIndex >= queue.Count)
				throw new ArgumentOutOfRangeException(nameof(fromIndex));
		}
	}
}
