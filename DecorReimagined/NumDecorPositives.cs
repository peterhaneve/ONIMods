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

using Database;
using PeterHan.PLib.Core;
using System;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// An achievement requirement with progress that requires a specified quantity of
	/// positive decor items affecting a cell at any given time.
	/// 
	/// The deprecated interface must be implemented to allow previous saves with this
	/// achievement to load.
	/// </summary>
	public sealed class NumDecorPositives : ColonyAchievementRequirement,
			AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The number of decor items required.
		/// </summary>
		private int required;

		public NumDecorPositives(int required) {
			if (required < 1)
				throw new ArgumentException("required > 0");
			this.required = required;
		}

		public void Deserialize(IReader reader) {
			required = reader.ReadInt32();
		}

		public override string GetProgress(bool complete) {
			return DecorReimaginedStrings.FEELSLIKEHOME_PROGRESS.text.F(complete ? required :
				(DecorCellManager.Instance?.NumPositiveDecor ?? 0));
		}

		public override bool Success() {
			return (DecorCellManager.Instance?.NumPositiveDecor ?? 0) >= required;
		}
	}
}
