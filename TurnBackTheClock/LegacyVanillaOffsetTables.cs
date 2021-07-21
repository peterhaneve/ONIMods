/*
 * Copyright 2021 Peter Han
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

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// The offset tables used for diagonal access prior to MD-471618.
	/// </summary>
	internal static class LegacyVanillaOffsetTables {
		public static readonly CellOffset[][] InvertedStandardTable = OffsetTable.Mirror(new CellOffset[][] {
			new CellOffset[]
			{
				new CellOffset(0, 0)
			},
			new CellOffset[]
			{
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(0, 2),
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(0, 3),
				new CellOffset(0, 1),
				new CellOffset(0, 2)
			},
			new CellOffset[]
			{
				new CellOffset(0, -1)
			},
			new CellOffset[]
			{
				new CellOffset(0, -2)
			},
			new CellOffset[]
			{
				new CellOffset(0, -3),
				new CellOffset(0, -2),
				new CellOffset(0, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(1, 1),
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 1),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(1, 2),
				new CellOffset(1, 0),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 2),
				new CellOffset(0, 1),
				new CellOffset(0, 2)
			},
			new CellOffset[]
			{
				new CellOffset(1, 3),
				new CellOffset(1, 2),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 3),
				new CellOffset(0, 1),
				new CellOffset(0, 2),
				new CellOffset(0, 3)
			},
			new CellOffset[]
			{
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -2),
				new CellOffset(1, 0),
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -2),
				new CellOffset(1, -1),
				new CellOffset(0, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -3),
				new CellOffset(1, 0),
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -3),
				new CellOffset(0, -1),
				new CellOffset(0, -2)
			},
			new CellOffset[]
			{
				new CellOffset(1, -3),
				new CellOffset(0, -1),
				new CellOffset(-1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 0),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(2, 1),
				new CellOffset(1, 1),
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 1),
				new CellOffset(1, 1),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(2, 2),
				new CellOffset(1, 2),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 3),
				new CellOffset(1, 1),
				new CellOffset(1, 2),
				new CellOffset(1, 3)
			},
			new CellOffset[]
			{
				new CellOffset(2, -1),
				new CellOffset(2, 0),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(2, -2),
				new CellOffset(1, 0),
				new CellOffset(1, -1),
				new CellOffset(2, -1)
			},
			new CellOffset[]
			{
				new CellOffset(2, -3),
				new CellOffset(1, 0),
				new CellOffset(1, -1),
				new CellOffset(1, -2)
			}
		});

		public static readonly CellOffset[][] InvertedStandardTableWithCorners = OffsetTable.Mirror(new CellOffset[][] {
			new CellOffset[]
			{
				new CellOffset(0, 0)
			},
			new CellOffset[]
			{
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(0, 2),
				new CellOffset(0, 1)
			},
			new CellOffset[]
			{
				new CellOffset(0, 3),
				new CellOffset(0, 1),
				new CellOffset(0, 2)
			},
			new CellOffset[]
			{
				new CellOffset(0, -1)
			},
			new CellOffset[]
			{
				new CellOffset(0, -2)
			},
			new CellOffset[]
			{
				new CellOffset(0, -3),
				new CellOffset(0, -2),
				new CellOffset(0, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 1),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(1, 2),
				new CellOffset(1, 0),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 2),
				new CellOffset(0, 1),
				new CellOffset(0, 2)
			},
			new CellOffset[]
			{
				new CellOffset(1, 3),
				new CellOffset(1, 2),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(1, 3),
				new CellOffset(0, 1),
				new CellOffset(0, 2),
				new CellOffset(0, 3)
			},
			new CellOffset[]
			{
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -2),
				new CellOffset(1, 0),
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -2),
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(1, -3),
				new CellOffset(1, 0),
				new CellOffset(1, -1),
				new CellOffset(1, -2)
			},
			new CellOffset[]
			{
				new CellOffset(1, -3),
				new CellOffset(1, -2),
				new CellOffset(1, -1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 0),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(2, 1),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 2),
				new CellOffset(1, 2),
				new CellOffset(1, 1)
			},
			new CellOffset[]
			{
				new CellOffset(2, 3),
				new CellOffset(1, 1),
				new CellOffset(1, 2),
				new CellOffset(1, 3)
			},
			new CellOffset[]
			{
				new CellOffset(2, -1),
				new CellOffset(2, 0),
				new CellOffset(1, 0)
			},
			new CellOffset[]
			{
				new CellOffset(2, -2),
				new CellOffset(1, 0),
				new CellOffset(1, -1),
				new CellOffset(2, -1)
			},
			new CellOffset[]
			{
				new CellOffset(2, -3),
				new CellOffset(1, 0),
				new CellOffset(1, -1),
				new CellOffset(1, -2)
			}
		});
	}
}
