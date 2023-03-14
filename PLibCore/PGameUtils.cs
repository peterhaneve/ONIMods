/*
 * Copyright 2023 Peter Han
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
using PeterHan.PLib.Detours;
using UnityEngine;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Utility and helper functions to perform common game-related (not UI) tasks.
	/// </summary>
	public static class PGameUtils {
		/// <summary>
		/// Creates a new sound event in the audio sheets.
		/// </summary>
		private delegate void CreateSoundDelegate(AudioSheets instance, string file_name,
			string anim_name, string type, float min_interval, string sound_name, int frame,
			string dlcId);

		private static readonly DetouredMethod<CreateSoundDelegate> CREATE_SOUND =
			typeof(AudioSheets).DetourLazy<CreateSoundDelegate>("CreateSound");

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
		/// Copies the sounds from one animation to another animation. Since Hot Shots this
		/// method only copies sounds present in the base game audio sheets, not any sounds
		/// that may have been added by other mods.
		/// </summary>
		/// <param name="dstAnim">The destination anim file name.</param>
		/// <param name="srcAnim">The source anim file name.</param>
		public static void CopySoundsToAnim(string dstAnim, string srcAnim) {
			if (string.IsNullOrEmpty(dstAnim))
				throw new ArgumentNullException(nameof(dstAnim));
			if (string.IsNullOrEmpty(srcAnim))
				throw new ArgumentNullException(nameof(srcAnim));
			if (Assets.GetAnim(dstAnim) != null) {
				var audioSheet = GameAudioSheets.Get();
				// Go through sound infos on the old audio sheets, much easier than deep
				// copying all the sound infos with a giant switch case
				try {
					foreach (var sheet in audioSheet.sheets) {
						var infos = sheet.soundInfos;
						int n = infos.Length;
						for (int i = 0; i < n; i++) {
							var soundInfo = infos[i];
							if (DlcManager.IsContentActive(soundInfo.RequiredDlcId) &&
									soundInfo.File == srcAnim)
								CreateAllSounds(audioSheet, dstAnim, soundInfo, sheet.
									defaultType);
						}
					}
				} catch (Exception e) {
					PUtil.LogWarning("Unable to copy sound files from {0} to {1}:".F(srcAnim,
						dstAnim));
					PUtil.LogExcWarn(e);
				}
			} else
				PUtil.LogWarning("Destination animation \"{0}\" not found!".F(dstAnim));
		}

		/// <summary>
		/// Calls out to the base game CreateSound delegate in AudioSheets.
		/// </summary>
		/// <param name="sheet">The location where the sound event will be stored.</param>
		/// <param name="file">The animation file name.</param>
		/// <param name="type">The event type to create.</param>
		/// <param name="info">Used for the minimum interval, DLC ID, and anim name.</param>
		/// <param name="sound">The sound name to play.</param>
		/// <param name="frame">The frame index to start the sound.</param>
		/// <returns>1 if the sound was created, or 0 if the sound was not created.</returns>
		private static int CreateSound(AudioSheets sheet, string file, string type,
				AudioSheet.SoundInfo info, string sound, int frame) {
			int n = 0;
			if (!string.IsNullOrEmpty(sound) && CREATE_SOUND != null) {
				CREATE_SOUND.Invoke(sheet, file, info.Anim, type, info.MinInterval, sound,
					frame, info.RequiredDlcId);
				n = 1;
			}
			return n;
		}

		/// <summary>
		/// Creates all of the sounds in the prefab (master) audio sheets, but with a different
		/// animation name substituted.
		/// </summary>
		/// <param name="sheet">The location where the sound event will be stored.</param>
		/// <param name="animFile">The substitute anim file name to use instead.</param>
		/// <param name="info">The sounds to be created.</param>
		/// <param name="defaultType">The sound type to use if the type is blank.</param>
		private static void CreateAllSounds(AudioSheets sheet, string animFile,
				AudioSheet.SoundInfo info, string defaultType) {
			string type = info.Type;
			int n;
			if (string.IsNullOrEmpty(type))
				type = defaultType;
			// Believe it or not this is better than trying to re-create a variant of
			// CreateSound again
			n = CreateSound(sheet, animFile, type, info, info.Name0, info.Frame0);
			n += CreateSound(sheet, animFile, type, info, info.Name1, info.Frame1);
			n += CreateSound(sheet, animFile, type, info, info.Name2, info.Frame2);
			n += CreateSound(sheet, animFile, type, info, info.Name3, info.Frame3);
			n += CreateSound(sheet, animFile, type, info, info.Name4, info.Frame4);
			n += CreateSound(sheet, animFile, type, info, info.Name5, info.Frame5);
			n += CreateSound(sheet, animFile, type, info, info.Name6, info.Frame6);
			n += CreateSound(sheet, animFile, type, info, info.Name7, info.Frame7);
			n += CreateSound(sheet, animFile, type, info, info.Name8, info.Frame8);
			n += CreateSound(sheet, animFile, type, info, info.Name9, info.Frame9);
			n += CreateSound(sheet, animFile, type, info, info.Name10, info.Frame10);
			n += CreateSound(sheet, animFile, type, info, info.Name11, info.Frame11);
#if DEBUG
			PUtil.LogDebug("Added {0:D} audio event(s) to anim {1}".F(n, animFile));
#endif
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
