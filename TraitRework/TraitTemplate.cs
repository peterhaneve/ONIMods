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

using Klei.AI;
using PeterHan.PLib;
using System;
using System.Collections.Generic;

namespace PeterHan.TraitRework {
	/// <summary>
	/// A template which modifies a trait.
	/// </summary>
	public sealed class TraitTemplate {
		/// <summary>
		/// The chores which cannot be performed.
		/// </summary>
		public IList<string> DisabledChores { get; set; }

		/// <summary>
		/// If not null, used to append to the trait tooltip.
		/// </summary>
		public Func<string> ExtendedTooltip { get; set; }

		/// <summary>
		/// The trait ID to modify.
		/// </summary>
		public string ID { get; }

		/// <summary>
		/// The effects which cannot be gained when under the influence of this trait.
		/// </summary>
		public IList<string> IgnoredEffects { get; set; }

		/// <summary>
		/// Whether the trait should be positive.
		/// </summary>
		public bool IsPositive { get; set; }

		/// <summary>
		/// The modifiers to be applied.
		/// </summary>
		public IList<AttributeModifier> Modifiers { get; set; }

		/// <summary>
		/// Whether the trait can start on a Duplicant. If true, add this trait to the
		/// DUPLICANTSTATS arrays as well.
		/// </summary>
		public bool ValidStartingTrait { get; set; }

		public TraitTemplate(string id) {
			ID = id;
		}

		public override string ToString() {
			return "TraitTemplate[ID={0},Positive={1}]".F(ID, IsPositive);
		}
	}
}
