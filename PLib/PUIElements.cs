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

namespace PeterHan.PLib {
	/// <summary>
	/// Used for creating and managing UI elements.
	/// </summary>
	public sealed class PUIElements {
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
				result.AppendFormat(", Text={0}", lt.text);
			foreach (var field in fields)
				result.AppendFormat(", {0}={1}", field.Name, field.GetValue(component) ??
					"null");
		}

		/// <summary>
		/// Adds an auto-fit resizer to a UI element.
		/// </summary>
		/// <param name="uiElement">The element to resize.</param>
		public static void AddSizeFitter(GameObject uiElement) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var fitter = uiElement.AddOrGet<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.MinSize;
			fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
			fitter.enabled = true;
			fitter.SetLayoutHorizontal();
			fitter.SetLayoutVertical();
		}

		/// <summary>
		/// Creates a button.
		/// </summary>
		/// <param name="parent">The parent which will contain the button.</param>
		/// <param name="template">The template button to use.</param>
		/// <param name="name">The button name.</param>
		/// <param name="onClick">The action to execute on click (optional).</param>
		/// <returns>The matching button.</returns>
		public static GameObject CreateButton(GameObject parent, Component template,
				string name = null, System.Action onClick = null) {
			if (parent == null)
				throw new ArgumentNullException("parent");
			if (template == null)
				throw new ArgumentNullException("template");
			// Create the button
			var button = Util.KInstantiateUI(template.gameObject, parent.transform.
				gameObject, true);
			if (!string.IsNullOrEmpty(name))
				button.name = name;
			// Add action
			if (onClick != null) {
				var kButton = button.GetComponent<KButton>();
				if (kButton != null)
					kButton.onClick += onClick;
			}
			AddSizeFitter(button);
#if false
			// TODO Maybe someday...
			var button = new GameObject(name ?? "Button");
			button.SetActive(true);
			button.AddComponent<CanvasRenderer>();
			var kButton = button.AddComponent<KButton>();
			// Set on click event
			if (onClick != null)
				kButton.onClick += onClick;
			kButton.additionalKImages = new KImage[0];
			kButton.bgImage = template.gameObject.GetComponent<KButton>()?.bgImage;
			// Set colors
			kButton.colorStyleSetting = PUITuning.BUTTON_STYLE;
			var kImage = button.AddComponent<KImage>();
			kImage.colorStyleSetting = PUITuning.BUTTON_STYLE;
			kImage.color = PUITuning.BUTTON_COLOR;
			button.AddComponent<LayoutElement>();
			button.AddComponent<ToolTip>();
			AddSizeFitter(button);
			// Add text to the button
			var textChild = new GameObject("Text");
			textChild.SetActive(true);
			textChild.transform.SetParent(button.transform);
			textChild.AddComponent<CanvasRenderer>();
			textChild.AddComponent<SetTextStyleSetting>().SetStyle(PUITuning.BUTTON_TEXT_STYLE);
			DebugObjectTree(button);
#endif
			return button;
		}

		/// <summary>
		/// Dumps information about the parent tree of the specified GameObject to the debug
		/// log.
		/// </summary>
		/// <param name="item">The item to determine hierarchy.</param>
		public static void DebugObjectHierarchy(GameObject item) {
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
			PUtil.LogDebug("Object Tree:" + Environment.NewLine + info);
		}

		/// <summary>
		/// Dumps information about the specified GameObject to the debug log.
		/// </summary>
		/// <param name="root">The root hierarchy to dump.</param>
		public static void DebugObjectTree(GameObject root) {
			string info = "null";
			if (root != null)
				info = GetObjectTree(root, 0);
			PUtil.LogDebug("Object Dump:" + Environment.NewLine + info);
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
			result.Append(sol).AppendFormat("GameObject[{0}, {1:D} child(ren)]", root.
				name, n).AppendLine();
			// Transformation
			result.Append(sol).AppendFormat(" Translation=<{0}> Rotation=<{1}> Scale=<{2}>",
				transform.localPosition, transform.localRotation, transform.localScale).
				AppendLine();
			// Components
			foreach (var component in root.GetComponents<Component>())
				if (component != null && !(component is Transform)) {
					// Exclude destroyed components and Transform objects
					result.Append(sol).Append(" Component[").Append(component.GetType().
						FullName);
					AddComponentText(result, component);
					result.AppendLine("]");
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
		/// Sets a UI element's text.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="text">The text to display on the element.</param>
		public static void SetText(GameObject uiElement, string text) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var title = uiElement.GetComponentInChildren<LocText>();
			if (title != null)
				title.text = text ?? string.Empty;
		}

		/// <summary>
		/// Sets a UI element's tool tip.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="tooltip">The tool tip text to display when hovered.</param>
		public static void SetToolTip(GameObject uiElement, string tooltip) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			if (!string.IsNullOrEmpty(tooltip)) {
				var tooltipComponent = uiElement.AddOrGet<ToolTip>();
				tooltipComponent.toolTip = tooltip;
			}
		}

		/// <summary>
		/// Shows a confirmation or message dialog based on a prefab.
		/// </summary>
		/// <param name="prefab">The dialog to show.</param>
		/// <param name="parent">The dialog's parent.</param>
		/// <param name="message">The message to display.</param>
		/// <returns>The dialog created.</returns>
		public static ConfirmDialogScreen ShowConfirmDialog(GameObject prefab,
				GameObject parent, string message) {
			if (prefab == null)
				throw new ArgumentNullException("prefab");
			if (parent == null)
				throw new ArgumentNullException("parent");
			var confirmDialog = Util.KInstantiateUI(prefab, parent, false).GetComponent<
				ConfirmDialogScreen>();
			confirmDialog.PopupConfirmDialog(message, null, null, null, null,
				null, null, null, null, true);
			confirmDialog.gameObject.SetActive(true);
			return confirmDialog;
		}
	}
}
