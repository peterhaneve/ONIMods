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

using PeterHan.PLib.UI;
using System;
using UnityEngine;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Stores sprites used in Debug Not Included.
	/// </summary>
	internal static class SpriteRegistry {
		/// <summary>
		/// The base path for the sprites to load.
		/// </summary>
		private const string BASE_PATH = "PeterHan.DebugNotIncluded.images.";

		/// <summary>
		/// Whether the sprites have been loaded.
		/// </summary>
		private static bool spritesLoaded;

		/// <summary>
		/// The sprite used for the move to bottom icon.
		/// </summary>
		private static Sprite MODS_BOTTOM;

		/// <summary>
		/// The sprite used for the move to top icon.
		/// </summary>
		private static Sprite MODS_TOP;

		/// <summary>
		/// Gets the sprite used for the move to bottom icon.
		/// </summary>
		public static Sprite GetBottomIcon() {
			LoadSprites();
			return MODS_BOTTOM;
		}

		/// <summary>
		/// Gets the sprite used for the move to top icon.
		/// </summary>
		public static Sprite GetTopIcon() {
			LoadSprites();
			return MODS_TOP;
		}

		/// <summary>
		/// Loads the specified sprite.
		/// </summary>
		/// <param name="name">The sprite file name without the extension.</param>
		private static Sprite LoadSprite(string name) {
			var sprite = PUIUtils.LoadSprite(BASE_PATH + name + ".png");
			sprite.name = name;
			return sprite;
		}

		/// <summary>
		/// Loads the sprites if they are not already loaded.
		/// </summary>
		private static void LoadSprites() {
			if (!spritesLoaded) {
				try {
					MODS_BOTTOM = LoadSprite("icon_mods_bottom");
					MODS_TOP = LoadSprite("icon_mods_top");
				} catch (ArgumentException e) {
					// Could not load the icons, but better this than crashing
					DebugLogger.LogException(e);
				}
				spritesLoaded = true;
			}
		}
	}
}
