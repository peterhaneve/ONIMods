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
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using PostLoadHandler = System.Action<Harmony.HarmonyInstance>;

namespace PeterHan.PLib {
	/// <summary>
	/// Static utility functions used across mods.
	/// </summary>
	public static class PUtil {
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
		/// Mod_OnLoad for proper PLib functionality.
		/// 
		/// Optionally logs the mod name and version when a mod initializes.
		/// </summary>
		public static void InitLibrary(bool logVersion = true) {
			var assembly = Assembly.GetCallingAssembly();
			if (assembly != null) {
				PRegistry.Init();
				if (logVersion)
					Debug.LogFormat("[PLib] Mod {0} initialized, version {1}",
						assembly.GetName()?.Name, assembly.GetFileVersion() ?? "Unknown");
			} else
				// Probably impossible
				Debug.LogError("[PLib] Somehow called from null assembly!");
		}

		/// <summary>
		/// Loads a DDS sprite embedded in the current assembly.
		/// 
		/// It must be encoded using the DXT5 format.
		/// </summary>
		/// <param name="path">The fully qualified path to the DDS image to load.</param>
		/// <param name="width">The desired width.</param>
		/// <param name="height">The desired height.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		public static Sprite LoadSprite(string path, int width, int height) {
			return LoadSprite(path, width, height, Vector4.zero);
		}

		/// <summary>
		/// Loads a DDS sprite embedded in the current assembly as a 9-slice sprite.
		/// 
		/// It must be encoded using the DXT5 format.
		/// </summary>
		/// <param name="path">The fully qualified path to the DDS image to load.</param>
		/// <param name="width">The desired width.</param>
		/// <param name="height">The desired height.</param>
		/// <param name="border">The sprite border.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		public static Sprite LoadSprite(string path, int width, int height, Vector4 border) {
			return UI.PUIUtils.LoadSprite(path, width, height, border, true);
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
		/// <param name="message">The message to log.</param>
		public static void LogException(Exception thrown) {
			Debug.LogErrorFormat("[PLib/{0}] {1} {2} {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs an exception message to the debug log at WARNING level.
		/// </summary>
		/// <param name="message">The message to log.</param>
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
		/// Registers a method which will be run after PLib and all mods load. It will be
		/// passed a HarmonyInstance which can be used to make late patches.
		/// </summary>
		/// <param name="callback">The method to invoke.</param>
		public static void RegisterPostload(PostLoadHandler callback) {
			if (callback == null)
				throw new ArgumentNullException("callback");
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
	}
}
