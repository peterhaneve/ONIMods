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

using Harmony;
using PeterHan.PLib;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.Resculpt {
	/// <summary>
	/// Patches which will be applied via annotations for Resculpt.
	/// </summary>
	public static class ResculptPatches {
		/// <summary>
		/// The default sprite to use for the button if the custom sprites fail to load.
		/// </summary>
		private const string DEFAULT_SPRITE = "action_control";

		/// <summary>
		/// Loads the sprites for this mod and registers them in the Assets class.
		/// </summary>
		private static void LoadImages() {
			LoadImage("repaint.png", ResculptStrings.REPAINT_SPRITE);
			LoadImage("resculpt.png", ResculptStrings.RESCULPT_SPRITE);
		}

		/// <summary>
		/// Loads the specified sprite into the assets.
		/// </summary>
		/// <param name="path">The image file name.</param>
		/// <param name="name">The desired sprite name.</param>
		private static void LoadImage(string path, string name) {
			var sprite = PUtil.LoadSprite("PeterHan.Resculpt." + path);
			if (sprite == null)
				sprite = Assets.GetSprite(DEFAULT_SPRITE);
			if (sprite != null)
				sprite.name = name;
			Assets.Sprites.Add(name, sprite);
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Applied to Artable to allow repainting.
		/// </summary>
		[HarmonyPatch(typeof(Artable), "OnSpawn")]
		public static class Artable_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Artable __instance) {
				var go = __instance.gameObject;
				if (go != null) {
					var rs = go.AddOrGet<Resculptable>();
					// Is it a painting?
					if (__instance is Painting) {
						rs.ButtonText = ResculptStrings.REPAINT_BUTTON;
						rs.ButtonIcon = ResculptStrings.REPAINT_SPRITE;
					}
				}
			}
		}

		/// <summary>
		/// Applied to Assets to load the button images when necessary.
		/// </summary>
		[HarmonyPatch(typeof(Assets), "OnPrefabInit")]
		public static class Assets_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix() {
				LoadImages();
			}
		}

		/// <summary>
		/// Applied to UserMenuScreen to add our new button icons to the menu.
		/// </summary>
		[HarmonyPatch(typeof(UserMenuScreen), "OnSpawn")]
		public static class UserMenuScreen_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(ref Sprite[] ___icons) {
				var oldIcons = ___icons;
				if (oldIcons != null) {
					var newIcons = new List<Sprite>(oldIcons.Length + 2);
					bool hasRepaint = false, hasResculpt = false;
					// Have we done it already?
					newIcons.AddRange(oldIcons);
					foreach (var icon in newIcons)
						if (icon != null) {
							string name = icon.name;
							if (name == ResculptStrings.REPAINT_SPRITE)
								hasRepaint = true;
							else if (name == ResculptStrings.RESCULPT_SPRITE)
								hasResculpt = true;
						}
					// Append the new icons from this mod, if they are not already present
					if (!hasRepaint)
						newIcons.Add(Assets.GetSprite(ResculptStrings.REPAINT_SPRITE));
					if (!hasResculpt)
						newIcons.Add(Assets.GetSprite(ResculptStrings.RESCULPT_SPRITE));
					___icons = newIcons.ToArray();
				}
			}
		}
	}
}
