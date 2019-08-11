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
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Static utility functions used across mods.
	/// </summary>
	public static class PLibUtil {
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
			Debug.LogErrorFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name, message);
		}

		/// <summary>
		/// Logs an exception message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogException(Exception thrown) {
			Debug.LogErrorFormat("[PLib/{0}] {1} {2} at {3}", Assembly.GetCallingAssembly()?.
				GetName()?.Name, thrown.GetType(), thrown.Message, thrown.StackTrace);
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogWarning(object message) {
			Debug.LogWarningFormat("[PLib/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name, message);
		}
	}
}
