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

using System;
using UnityEngine;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Utility and helper functions to perform common game-related (not UI) tasks.
	/// </summary>
	public static class PGameUtils {
		/// <summary>
		/// Centers and selects an entity.
		/// </summary>
		/// <param name="entity">The entity to center and focus.</param>
		public static void CenterAndSelect(KMonoBehaviour entity) {
			if (entity != null && entity.TryGetComponent(out KSelectable select))
				SelectTool.Instance.SelectAndFocus(entity.transform.position, select, Vector3.
					zero);
		}

		/// <summary>
		/// Copies the sounds from one animation to another animation.
		/// </summary>
		/// <param name="dstAnim">The destination anim file name.</param>
		/// <param name="srcAnim">The source anim file name.</param>
		public static void CopySoundsToAnim(string dstAnim, string srcAnim) {
			if (string.IsNullOrEmpty(dstAnim))
				throw new ArgumentNullException(nameof(dstAnim));
			if (string.IsNullOrEmpty(srcAnim))
				throw new ArgumentNullException(nameof(srcAnim));
			var anim = Assets.GetAnim(dstAnim);
			if (anim != null) {
				var audioSheet = GameAudioSheets.Get();
				var animData = anim.GetData();
				// For each anim in the kanim, look for existing sound events under the old
				// anim's file name
				for (int i = 0; i < animData.animCount; i++) {
					string animName = animData.GetAnim(i)?.name ?? "";
					var events = audioSheet.GetEvents(srcAnim + "." + animName);
					if (events != null) {
#if DEBUG
						PUtil.LogDebug("Adding {0:D} audio event(s) to anim {1}.{2}".F(events.
							Count, dstAnim, animName));
#endif
						audioSheet.events[dstAnim + "." + animName] = events;
					}
				}
			} else
				PUtil.LogWarning("Destination animation \"{0}\" not found!".F(dstAnim));
		}

		/// <summary>
		/// Creates a popup message at the specified cell location on the Move layer.
		/// </summary>
		/// <param name="image">The image to display, likely from PopFXManager.Instance.</param>
		/// <param name="text">The text to display.</param>
		/// <param name="cell">The cell location to create the message.</param>
		public static void CreatePopup(Sprite image, string text, int cell) {
			CreatePopup(image, text, Grid.CellToPosCBC(cell, Grid.SceneLayer.Move));
		}

		/// <summary>
		/// Creates a popup message at the specified location.
		/// </summary>
		/// <param name="image">The image to display, likely from PopFXManager.Instance.</param>
		/// <param name="text">The text to display.</param>
		/// <param name="position">The position to create the message.</param>
		public static void CreatePopup(Sprite image, string text, Vector3 position) {
			PopFXManager.Instance.SpawnFX(image, text, null, position);
		}

		/// <summary>
		/// Creates a default user menu handler for a class implementing IRefreshUserMenu.
		/// </summary>
		/// <typeparam name="T">The class to handle events.</typeparam>
		/// <returns>A handler which can be used to Subscribe for RefreshUserMenu events.</returns>
		public static EventSystem.IntraObjectHandler<T> CreateUserMenuHandler<T>()
				where T : Component, IRefreshUserMenu {
			return new Action<T, object>((T target, object ignore) => {
#if DEBUG
				PUtil.LogDebug("OnRefreshUserMenu<{0}> on {1}".F(typeof(T).Name, target));
#endif
				target.OnRefreshUserMenu();
			});
		}

		/// <summary>
		/// Retrieves an object layer by its name, resolving the value at runtime to handle
		/// differences in the layer enum. This method is slower than a direct lookup -
		/// consider caching the result.
		/// </summary>
		/// <param name="name">The name of the layer (use nameof()!)</param>
		/// <param name="defValue">The default value (use the value at compile time)</param>
		/// <returns>The value to use for this object layer.</returns>
		public static ObjectLayer GetObjectLayer(string name, ObjectLayer defValue) {
			if (!Enum.TryParse(name, out ObjectLayer value))
				value = defValue;
			return value;
		}

		/// <summary>
		/// Highlights an entity. Use Color.black to unhighlight it.
		/// </summary>
		/// <param name="entity">The entity to highlight.</param>
		/// <param name="highlightColor">The color to highlight it.</param>
		public static void HighlightEntity(Component entity, Color highlightColor) {
			if (entity != null && entity.TryGetComponent(out KAnimControllerBase kbac))
				kbac.HighlightColour = highlightColor;
		}

		/// <summary>
		/// Plays a sound effect.
		/// </summary>
		/// <param name="name">The sound effect name to play.</param>
		/// <param name="position">The position where the sound is generated.</param>
		public static void PlaySound(string name, Vector3 position) {
			SoundEvent.PlayOneShot(GlobalAssets.GetSound(name), position);
		}

		/// <summary>
		/// Saves the current list of mods.
		/// </summary>
		public static void SaveMods() {
			Global.Instance.modManager.Save();
		}
	}
}
