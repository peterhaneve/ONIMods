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
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Utility functions for dealing with Unity UIs.
	/// </summary>
	public static class PUIUtils {
		/// <summary>
		/// Adds text describing a particular component if available.
		/// </summary>
		/// <param name="result">The location to append the text.</param>
		/// <param name="component">The component to describe.</param>
		private static void AddComponentText(StringBuilder result, Component component) {
			// Include all fields
			var fields = component.GetType().GetFields(BindingFlags.DeclaredOnly |
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			// Class specific
			if (component is LocText lt)
				result.AppendFormat(", Text={0}, Color={1}, Font={2}", lt.text, lt.color,
					lt.font);
			else if (component is Image im) {
				result.AppendFormat(", Color={0}", im.color);
				if (im is KImage ki)
					result.AppendFormat(", Sprite={0}", ki.sprite);
			} else if (component is HorizontalOrVerticalLayoutGroup lg)
				result.AppendFormat(", Child Align={0}, Control W={1}, Control H={2}",
					lg.childAlignment, lg.childControlWidth, lg.childControlHeight);
			foreach (var field in fields) {
				object value = field.GetValue(component) ?? "null";
				// Value type specific
				if (value is LayerMask lm)
					value = "Layer #" + lm.value;
				else if (value is System.Collections.ICollection ic)
					value = "[" + ic.Join() + "]";
				else if (value is Array ar)
					value = "[" + ar.Join() + "]";
				result.AppendFormat(", {0}={1}", field.Name, value);
			}
		}

		/// <summary>
		/// Builds a PLib UI object and adds it to an existing UI object.
		/// </summary>
		/// <param name="component">The UI object to add.</param>
		/// <param name="parent">The parent of the new object.</param>
		/// <param name="index">The sibling index to insert the element at, if provided.</param>
		/// <returns>The built version of the UI object.</returns>
		public static GameObject AddTo(this IUIComponent component, GameObject parent,
				int index = -2) {
			if (component == null)
				throw new ArgumentNullException("component");
			if (parent == null)
				throw new ArgumentNullException("parent");
			var child = component.Build();
			PUIElements.SetParent(child, parent);
			if (index == -1)
				child.transform.SetAsLastSibling();
			else if (index >= 0)
				child.transform.SetSiblingIndex(index);
			return child;
		}

		/// <summary>
		/// Dumps information about the parent tree of the specified GameObject to the debug
		/// log.
		/// </summary>
		/// <param name="item">The item to determine hierarchy.</param>
		public static void DebugObjectHierarchy(this GameObject item) {
			string info = "null";
			if (item != null) {
				var result = new StringBuilder(256);
				do {
					result.Append("- ");
					result.Append(item.name ?? "Unnamed");
					item = item.transform?.parent?.gameObject;
					if (item != null)
						result.AppendLine();
				} while (item != null);
				info = result.ToString();
			}
			LogUIDebug("Object Tree:" + Environment.NewLine + info);
		}

		/// <summary>
		/// Dumps information about the specified GameObject to the debug log.
		/// </summary>
		/// <param name="root">The root hierarchy to dump.</param>
		public static void DebugObjectTree(this GameObject root) {
			string info = "null";
			if (root != null)
				info = GetObjectTree(root, 0);
			LogUIDebug("Object Dump:" + Environment.NewLine + info);
		}

		/// <summary>
		/// Creates a string recursively describing the specified GameObject.
		/// </summary>
		/// <param name="root">The root GameObject hierarchy.</param>
		/// <returns>A string describing this game object.</returns>
		private static string GetObjectTree(GameObject root, int indent) {
			var result = new StringBuilder(1024);
			// Calculate indent to make nested reading easier
			var solBuilder = new StringBuilder(indent);
			for (int i = 0; i < indent; i++)
				solBuilder.Append(' ');
			string sol = solBuilder.ToString();
			var transform = root.transform;
			int n = transform.childCount;
			// Basic information
			result.Append(sol).AppendFormat("GameObject[{0}, {1:D} child(ren), Layer {2:D}, " +
				"Active={3}]", root.name, n, root.layer, root.activeInHierarchy).AppendLine();
			// Transformation
			result.Append(sol).AppendFormat(" Translation={0} [{3}] Rotation={1} [{4}] " +
				"Scale={2}", transform.position, transform.rotation, transform.
				localScale, transform.localPosition, transform.localRotation).AppendLine();
			// Components
			foreach (var component in root.GetComponents<Component>()) {
				if (component is RectTransform rt) {
					// UI rectangle
					var rect = rt.rect;
					Vector2 pivot = rt.pivot, aMin = rt.anchorMin, aMax = rt.anchorMax;
					result.Append(sol).AppendFormat(" Rect[Coords=({0:F2},{1:F2}) " +
						"Size=({2:F2},{3:F2}) Pivot=({4:F2},{5:F2}) ", rect.xMin,
						rect.yMin, rect.width, rect.height, pivot.x, pivot.y);
					result.AppendFormat("AnchorMin=({0:F2},{1:F2}), AnchorMax=({2:F2}," +
						"{3:F2})]", aMin.x, aMin.y, aMax.x, aMax.y).AppendLine();
				} else if (component != null && !(component is Transform)) {
					// Exclude destroyed components and Transform objects
					result.Append(sol).Append(" Component[").Append(component.GetType().
						FullName);
					AddComponentText(result, component);
					result.AppendLine("]");
				}
			}
			// Children
			if (n > 0)
				result.Append(sol).AppendLine(" Children:");
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i).gameObject;
				if (child != null)
					// Exclude destroyed objects
					result.AppendLine(GetObjectTree(child, indent + 2));
			}
			return result.ToString().TrimEnd();
		}

		/// <summary>
		/// Logs a debug message encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogUIDebug(string message) {
			Debug.LogFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
		}

		/// <summary>
		/// Logs a warning encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogUIWarning(string message) {
			Debug.LogWarningFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", message);
		}
	}
}
