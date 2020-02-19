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

using Klei;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

using PostLoadHandler = System.Action<Harmony.HarmonyInstance>;

namespace PeterHan.PLib {
	/// <summary>
	/// Static utility functions used across mods.
	/// </summary>
	public static class PUtil {
		/// <summary>
		/// Retrieves the current changelist version of the game. LU-371502 has a version of
		/// 371502u.
		/// 
		/// If the version cannot be determined, returns 0.
		/// </summary>
		public static uint GameVersion { get; }

		/// <summary>
		/// The first released version of the new Automation Update.
		/// </summary>
		public const uint VERSION_AP_PREVIEW = 395113u;

		/// <summary>
		/// Whether PLib has been initialized.
		/// </summary>
		internal static bool PLibInit { get; private set; }

		/// <summary>
		/// The characters which are not allowed in file names.
		/// </summary>
		private static readonly HashSet<char> INVALID_FILE_CHARS;

		static PUtil() {
			INVALID_FILE_CHARS = new HashSet<char>(Path.GetInvalidFileNameChars());
			PLibInit = false;
			GameVersion = GetGameVersion();
		}

		/// <summary>
		/// Adds a colony achievement to the colony summary screen. Must be invoked after the
		/// database is initialized (Db.Initialize() postfix recommended).
		/// </summary>
		/// <param name="achievement">The achievement to add.</param>
		public static void AddColonyAchievement(Database.ColonyAchievement achievement) {
			if (achievement == null)
				throw new ArgumentNullException("achievement");
			Db.Get()?.ColonyAchievements?.resources?.Add(achievement);
		}

		/// <summary>
		/// Adds the name and description for a status item.
		/// 
		/// Must be used before the StatusItem is first instantiated.
		/// </summary>
		/// <param name="id">The status item ID.</param>
		/// <param name="category">The status item category.</param>
		/// <param name="name">The name to display in the UI.</param>
		/// <param name="desc">The description to display in the UI.</param>
		public static void AddStatusItemStrings(string id, string category, string name,
				string desc) {
			string uid = id.ToUpperInvariant();
			string ucategory = category.ToUpperInvariant();
			Strings.Add("STRINGS." + ucategory + ".STATUSITEMS." + uid + ".NAME", name);
			Strings.Add("STRINGS." + ucategory + ".STATUSITEMS." + uid + ".TOOLTIP", desc);
		}

		/// <summary>
		/// Centers and selects an entity.
		/// </summary>
		/// <param name="entity">The entity to center and focus.</param>
		public static void CenterAndSelect(KMonoBehaviour entity) {
			if (entity != null) {
				Transform transform = entity.transform;
				SelectTool.Instance.SelectAndFocus(transform.transform.GetPosition(),
					transform.GetComponent<KSelectable>(), Vector3.zero);
			}
		}

		/// <summary>
		/// Copies the sounds from one animation to another animation.
		/// </summary>
		/// <param name="dstAnim">The destination anim file name.</param>
		/// <param name="srcAnim">The source anim file name.</param>
		public static void CopySoundsToAnim(string dstAnim, string srcAnim) {
			if (string.IsNullOrEmpty(dstAnim))
				throw new ArgumentNullException("dstAnim");
			if (string.IsNullOrEmpty(srcAnim))
				throw new ArgumentNullException("srcAnim");
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
						LogDebug("Adding {0:D} audio event(s) to anim {1}.{2}".F(events.Count,
							dstAnim, animName));
#endif
						audioSheet.events[dstAnim + "." + animName] = events;
					}
				}
			} else
				LogWarning("Destination animation \"{0}\" not found!".F(dstAnim));
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
		/// Finds the distance between two points.
		/// </summary>
		/// <param name="x1">The first X coordinate.</param>
		/// <param name="y1">The first Y coordinate.</param>
		/// <param name="x2">The second X coordinate.</param>
		/// <param name="y2">The second Y coordinate.</param>
		/// <returns>The non-taxicab (straight line) distance between the points.</returns>
		public static float Distance(float x1, float y1, float x2, float y2) {
			float dx = x2 - x1, dy = y2 - y1;
			return Mathf.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// Finds the distance between two points.
		/// </summary>
		/// <param name="x1">The first X coordinate.</param>
		/// <param name="y1">The first Y coordinate.</param>
		/// <param name="x2">The second X coordinate.</param>
		/// <param name="y2">The second Y coordinate.</param>
		/// <returns>The non-taxicab (straight line) distance between the points.</returns>
		public static double Distance(double x1, double y1, double x2, double y2) {
			double dx = x2 - x1, dy = y2 - y1;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// Executes all post-load handlers.
		/// </summary>
		internal static void ExecutePostload() {
			IList<PostLoadHandler> postload = null;
			lock (PSharedData.GetLock(PRegistry.KEY_POSTLOAD_LOCK)) {
				// Get list holding postload information
				var list = PSharedData.GetData<IList<PostLoadHandler>>(PRegistry.
					KEY_POSTLOAD_TABLE);
				if (list != null)
					postload = new List<PostLoadHandler>(list);
			}
			// If there were any, run them
			if (postload != null) {
				var hInst = Harmony.HarmonyInstance.Create("PLib.PostLoad");
				PRegistry.LogPatchDebug("Executing {0:D} post-load handler(s)".F(postload.
					Count));
				foreach (var handler in postload)
					try {
						handler?.Invoke(hInst);
					} catch (Exception e) {
						var method = handler.Method;
						// Say which mod's postload crashed
						if (method != null)
							PRegistry.LogPatchWarning("Postload handler for {0} failed:".F(
								method.DeclaringType.Assembly?.GetName()?.Name ?? "?"));
						LogException(e);
					}
			}
		}

		/// <summary>
		/// Retrieves the current game version from the Klei code.
		/// </summary>
		/// <returns>The change list version of the game, or 0 if it cannot be determined.</returns>
		private static uint GetGameVersion() {
			/*
			 * KleiVersion.ChangeList is a const which is substituted at compile time; if
			 * accessed directly, PLib would have a version "baked in" and would never
			 * update depending on the game version in use.
			 */
			var field = PPatchTools.GetFieldSafe(typeof(KleiVersion), nameof(KleiVersion.
				ChangeList), true);
			uint ver = 0U;
			if (field != null && field.GetValue(null) is uint newVer)
				ver = newVer;
			return ver;
		}

		/// <summary>
		/// Highlights an entity. Use Color.black to unhighlight it.
		/// </summary>
		/// <param name="entity">The entity to highlight.</param>
		/// <param name="highlightColor">The color to highlight it.</param>
		public static void HighlightEntity(Component entity, Color highlightColor) {
			var component = entity?.GetComponent<KAnimControllerBase>();
			if (component != null)
				component.HighlightColour = highlightColor;
		}

		/// <summary>
		/// Initializes the PLib patch bootstrapper for shared code. <b>Must</b> be called in
		/// OnLoad for proper PLib functionality.
		/// 
		/// Optionally logs the mod name and version when a mod initializes.
		/// </summary>
		public static void InitLibrary(bool logVersion = true) {
			var assembly = Assembly.GetCallingAssembly();
			if (assembly != null) {
				bool needInit;
				// Check if PLib was already initialized
				lock (assembly) {
					needInit = !PLibInit;
					if (needInit)
						PLibInit = true;
				}
				if (needInit) {
					// Only if not already initialized
					PRegistry.Init();
					if (logVersion)
						Debug.LogFormat("[PLib] Mod {0} initialized, version {1}",
							assembly.GetName()?.Name, assembly.GetFileVersion() ?? "Unknown");
				}
			} else
				// Probably impossible
				Debug.LogError("[PLib] Somehow called from null assembly!");
		}

		/// <summary>
		/// Returns true if the file is a valid file name. If the argument contains path
		/// separator characters, this method returns false, since that is not a valid file
		/// name.
		/// 
		/// Null and empty file names are not valid file names.
		/// </summary>
		/// <param name="file">The file name to check.</param>
		/// <returns>true if the name could be used to name a file, or false otherwise.</returns>
		public static bool IsValidFileName(string file) {
			bool valid = (file != null);
			if (valid) {
				// Cannot contain characters in INVALID_FILE_CHARS
				int len = file.Length;
				for (int i = 0; i < len && valid; i++)
					if (INVALID_FILE_CHARS.Contains(file[i]))
						valid = false;
			}
			return valid;
		}

		/// <summary>
		/// Loads a sprite embedded in the calling assembly.
		/// 
		/// It may be encoded using PNG, DXT5, or JPG format.
		/// </summary>
		/// <param name="path">The fully qualified path to the image to load.</param>
		/// <param name="border">The sprite border. If there is no 9-patch border, use default(Vector4).</param>
		/// <param name="log">true to log the sprite load, or false to load silently.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		public static Sprite LoadSprite(string path, Vector4 border = default, bool log = true) {
			var assembly = Assembly.GetCallingAssembly() ?? Assembly.GetExecutingAssembly();
			return UI.PUIUtils.LoadSprite(assembly, path, border, log);
		}

		/// <summary>
		/// Loads a DDS sprite embedded in the calling assembly.
		/// 
		/// It must be encoded using the DXT5 format.
		/// </summary>
		/// <param name="path">The fully qualified path to the DDS image to load.</param>
		/// <param name="width">The desired width.</param>
		/// <param name="height">The desired height.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		[Obsolete("LoadSprite(path, Vector4, bool) allows the use of PNG/JPG images which scale far better")]
		public static Sprite LoadSprite(string path, int width, int height) {
			var assembly = Assembly.GetCallingAssembly() ?? Assembly.GetExecutingAssembly();
			return UI.PUIUtils.LoadSpriteLegacy(assembly, path, width, height, default);
		}

		/// <summary>
		/// Loads a DDS sprite embedded in the calling assembly as a 9-slice sprite.
		/// 
		/// It must be encoded using the DXT5 format.
		/// </summary>
		/// <param name="path">The fully qualified path to the DDS image to load.</param>
		/// <param name="width">The desired width.</param>
		/// <param name="height">The desired height.</param>
		/// <param name="border">The sprite border.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		[Obsolete("LoadSprite(path, Vector4, bool) allows the use of PNG/JPG images which scale far better")]
		public static Sprite LoadSprite(string path, int width, int height, Vector4 border) {
			var assembly = Assembly.GetCallingAssembly() ?? Assembly.GetExecutingAssembly();
			return UI.PUIUtils.LoadSpriteLegacy(assembly, path, width, height, border);
		}

		/// <summary>
		/// Logs a message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogDebug(object message) {
			Debug.LogFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.Name,
				message);
		}

		/// <summary>
		/// Logs an error message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogError(object message) {
			// Cannot make a utility property or method for Assembly.GetCalling... because
			// its caller would then be the assembly PLib is in, not the assembly which
			// invoked LogXXX
			Debug.LogErrorFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
		}

		/// <summary>
		/// Logs an exception message to the debug log.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		public static void LogException(Exception thrown) {
			Debug.LogErrorFormat("[PLib/{0}] {1} {2} {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs an exception message to the debug log at WARNING level.
		/// </summary>
		/// <param name="thrown">The exception to log.</param>
		public static void LogExcWarn(Exception thrown) {
			Debug.LogWarningFormat("[PLib/{0}] {1} {2} {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs the mod name and version when a mod initializes. Also initializes the PLib
		/// patch bootstrapper for shared code.
		/// 
		/// At the suggestion of some folks, this method has been renamed to InitLibrary.
		/// </summary>
		[Obsolete("LogModInit is obsolete. Use InitLibrary(bool) instead.")]
		public static void LogModInit() {
			InitLibrary();
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogWarning(object message) {
			Debug.LogWarningFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
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
		/// Registers a method which will be run after PLib and all mods load. It will be
		/// passed a HarmonyInstance which can be used to make late patches.
		/// </summary>
		/// <param name="callback">The method to invoke.</param>
		public static void RegisterPostload(PostLoadHandler callback) {
			if (callback == null)
				throw new ArgumentNullException("callback");
			// Some others used this call before the library was initialized
			if (!PLibInit) {
				InitLibrary(false);
				LogWarning("PUtil.InitLibrary was not called before using RegisterPostload!");
			}
			lock (PSharedData.GetLock(PRegistry.KEY_POSTLOAD_LOCK)) {
				// Get list holding postload information
				var list = PSharedData.GetData<IList<PostLoadHandler>>(PRegistry.
					KEY_POSTLOAD_TABLE);
				if (list == null)
					PSharedData.PutData(PRegistry.KEY_POSTLOAD_TABLE, list =
						new List<PostLoadHandler>(16));
				list.Add(callback);
				string name = Assembly.GetCallingAssembly()?.GetName()?.Name;
				if (name != null)
					PRegistry.LogPatchDebug("Registered post-load handler for " + name);
			}
		}

		/// <summary>
		/// Attempts to parse an enumeration's value.
		/// </summary>
		/// <typeparam name="T">The enumeration type to parse.</typeparam>
		/// <param name="enumValue">The value to parse.</param>
		/// <param name="ifNotFound">The value to use if the provided value is invalid.</param>
		/// <param name="ignoreCase">true to ignore case, or false (default) to be case sensitive.</param>
		/// <returns>The parsed enumeration value, or ifNotFound if the value string is invalid.
		/// Note that out of range integer strings will be successfully parsed and converted to
		/// the enumeration type.</returns>
		public static T TryParseEnum<T>(string enumValue, T ifNotFound = default,
				bool ignoreCase = false) where T : Enum {
			T value = ifNotFound;
			if (enumValue == null)
				throw new ArgumentNullException("enumName");
			try {
				value = (T)Enum.Parse(typeof(T), enumValue, ignoreCase);
			} catch (InvalidCastException) {
			} catch (ArgumentException) {
			} catch (OverflowException) {
			}
			return value;
		}

		/// <summary>
		/// A callback function for the YAML parser to process errors that it throws.
		/// </summary>
		/// <param name="error">The YAML parsing error</param>
		internal static void YamlParseErrorCB(YamlIO.Error error, bool _) {
			throw new InvalidDataException(string.Format("{0} parse error in {1}\n{2}", error.
				severity, error.file.full_path, error.message), error.inner_exception);
		}
	}
}
