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

using System;

namespace PeterHan.PLib {
	/// <summary>
	/// Represents a key bind created and managed by PLib.
	/// </summary>
	public sealed class PKeyBinding {
		/// <summary>
		/// The action to trigger.
		/// </summary>
		public PAction Action { get; }

		/// <summary>
		/// The gamepad button to bind.
		/// </summary>
		public GamepadButton GamePadButton { get; }

		/// <summary>
		/// The key code.
		/// </summary>
		public KKeyCode KeyCode { get; }

		/// <summary>
		/// The modifier code.
		/// </summary>
		public Modifier Modifier { get; }

		public PKeyBinding(PAction action, KKeyCode keyCode, Modifier modifier = Modifier.
				None, GamepadButton gamePadButton = GamepadButton.NumButtons) {
			Action = action ?? throw new ArgumentNullException("action");
			GamePadButton = gamePadButton;
			KeyCode = keyCode;
			Modifier = modifier;
		}

		public override string ToString() {
			return "{0} + {1} = \"{2}\"".F(Modifier, KeyCode, Action);
		}
	}
}
