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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PeterHan.PLib {
	/// <summary>
	/// Represents a key bind created and managed by PLib. This class is only used for
	/// serialization and is not public to PLib consumers.
	/// </summary>
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	internal sealed class SerializedKeyBinding {
		/// <summary>
		/// The action key that will be triggered when this key is pressed.
		/// </summary>
		[JsonProperty]
		public string Action { get; }

		/// <summary>
		/// The gamepad button to bind.
		/// </summary>
		[JsonProperty]
		[JsonConverter(typeof(StringEnumConverter))]
		public GamepadButton GamePadButton { get; }

		/// <summary>
		/// The key code.
		/// </summary>
		[JsonProperty]
		[JsonConverter(typeof(StringEnumConverter))]
		public KKeyCode KeyCode { get; }

		/// <summary>
		/// The modifier code.
		/// </summary>
		[JsonProperty]
		[JsonConverter(typeof(StringEnumConverter))]
		public Modifier Modifiers { get; }

		public SerializedKeyBinding(string action, KKeyCode keyCode, Modifier modifier,
				GamepadButton gamePadButton) {
			Action = action;
			GamePadButton = gamePadButton;
			KeyCode = keyCode;
			Modifiers = modifier;
		}

		public override string ToString() {
			return "{0} + {1} = {2}".F(KeyCode, Modifiers, Action);
		}
	}
}
