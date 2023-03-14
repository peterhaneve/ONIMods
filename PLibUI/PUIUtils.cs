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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using SideScreenRef = DetailsScreen.SideScreenRef;

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
				BindingFlags.Instance | PPatchTools.BASE_FLAGS);
			// Class specific
			if (component is TMP_Text lt)
				result.AppendFormat(", Text={0}, Color={1}, Font={2}", lt.text, lt.color,
					lt.font);
			else if (component is Image im) {
				result.AppendFormat(", Color={0}", im.color);
				if (im.sprite != null)
					result.AppendFormat(", Sprite={0}", im.sprite);
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
				result.AppendFormat(", {0}={1}", field.Name, value);
			}
		}

		/// <summary>
		/// Adds a hot pink rectangle over the target matching its size, to help identify it
		/// better.
		/// </summary>
		/// <param name="parent">The target UI component.</param>
		public static void AddPinkOverlay(GameObject parent) {
			var child = PUIElements.CreateUI(parent, "Overlay");
			var img = child.AddComponent<Image>();
			img.color = new Color(1.0f, 0.0f, 1.0f, 0.2f);
		}

		/// <summary>
		/// Adds the specified side screen content to the side screen list. The side screen
		/// behavior should be defined in a class inherited from SideScreenContent.
		/// 
		/// The side screen will be added at the end of the list, which will cause it to
		/// appear above previous side screens in the details panel.
		/// 
		/// This method should be used in a postfix on DetailsScreen.OnPrefabInit.
		/// </summary>
		/// <typeparam name="T">The type of the controller that will determine how the side
		/// screen works. A new instance will be created and added as a component to the new
		/// side screen.</typeparam>
		/// <param name="uiPrefab">The UI prefab to use. If null is passed, the UI should
		/// be created and added to the GameObject hosting the controller object in its
		/// constructor.</param>
		public static void AddSideScreenContent<T>(GameObject uiPrefab = null)
				where T : SideScreenContent {
			AddSideScreenContentWithOrdering<T>(null, true, uiPrefab);
		}

		/// <summary>
		/// Adds the specified side screen content to the side screen list. The side screen
		/// behavior should be defined in a class inherited from SideScreenContent.
		/// 
		/// This method should be used in a postfix on DetailsScreen.OnPrefabInit.
		/// </summary>
		/// <typeparam name="T">The type of the controller that will determine how the side
		/// screen works. A new instance will be created and added as a component to the new
		/// side screen.</typeparam>
		/// <param name="targetClassName">The full name of the type of side screen to based to ordering 
		/// around. An example of how this method can be used is:
		/// `AddSideScreenContentWithOrdering&lt;MySideScreen&gt;(typeof(CapacityControlSideScreen).FullName);`
		/// `typeof(TargetedSideScreen).FullName` is the suggested value of this parameter.
		/// Side screens from other mods can be used with their qualified names, even if no
		/// reference to their type is available, but the target mod must have added their
		/// custom side screen to the list first.</param>
		/// <param name="insertBefore">Whether to insert the new screen before or after the
		/// target side screen in the list. Defaults to before (true).
		/// When inserting before the screen, if both are valid for a building then the side
		/// screen of type "T" will show below the one of type "fullName". When inserting after
		/// the screen, the reverse is true.</param>
		/// <param name="uiPrefab">The UI prefab to use. If null is passed, the UI should
		/// be created and added to the GameObject hosting the controller object in its
		/// constructor.</param>
		public static void AddSideScreenContentWithOrdering<T>(string targetClassName,
				bool insertBefore = true, GameObject uiPrefab = null)
				where T : SideScreenContent {
			var inst = DetailsScreen.Instance;
			if (inst == null)
				LogUIWarning("DetailsScreen is not yet initialized, try a postfix on DetailsScreen.OnPrefabInit");
			else {
				var screens = UIDetours.SIDE_SCREENS.Get(inst);
				var body = UIDetours.SS_CONTENT_BODY.Get(inst);
				string name = typeof(T).Name;
				if (body != null && screens != null) {
					// The ref normally contains a prefab which is instantiated
					var newScreen = new SideScreenRef();
					// Mimic the basic screens
					var rootObject = PUIElements.CreateUI(body, name);
					// Preserve the border by fitting the child
					rootObject.AddComponent<BoxLayoutGroup>().Params = new BoxLayoutParams() {
						Direction = PanelDirection.Vertical, Alignment = TextAnchor.
						UpperCenter, Margin = new RectOffset(1, 1, 0, 1)
					};
					var controller = rootObject.AddComponent<T>();
					if (uiPrefab != null) {
						// Add prefab if supplied
						UIDetours.SS_CONTENT_CONTAINER.Set(controller, uiPrefab);
						uiPrefab.transform.SetParent(rootObject.transform);
					}
					newScreen.name = name;
					// Offset is never used
					UIDetours.SS_OFFSET.Set(newScreen, Vector2.zero);
					UIDetours.SS_PREFAB.Set(newScreen, controller);
					UIDetours.SS_INSTANCE.Set(newScreen, controller);
					InsertSideScreenContent(screens, newScreen, targetClassName, insertBefore);
				}
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
				throw new ArgumentNullException(nameof(component));
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));
			var child = component.Build();
			child.SetParent(parent);
			if (index == -1)
				child.transform.SetAsLastSibling();
			else if (index >= 0)
				child.transform.SetSiblingIndex(index);
			return child;
		}

		/// <summary>
		/// Calculates the size of a single game object.
		/// </summary>
		/// <param name="obj">The object to calculate.</param>
		/// <param name="direction">The direction to calculate.</param>
		/// <param name="components">The components of this game object.</param>
		/// <returns>The object's minimum and preferred size.</returns>
		internal static LayoutSizes CalcSizes(GameObject obj, PanelDirection direction,
				IEnumerable<Component> components) {
			float min = 0.0f, preferred = 0.0f, flexible = 0.0f;
			int minPri = int.MinValue, prefPri = int.MinValue, flexPri = int.MinValue;
			var scale = obj.transform.localScale;
			// Find the correct scale direction
			float scaleFactor = Math.Abs(direction == PanelDirection.Horizontal ? scale.x :
				scale.y);
			bool ignore = false;
			foreach (var component in components) {
				if ((component as ILayoutIgnorer)?.ignoreLayout == true) {
					ignore = true;
					break;
				}
				if ((component as Behaviour)?.isActiveAndEnabled != false && component is
						ILayoutElement le) {
					int lp = le.layoutPriority;
					// Calculate must come first
					if (direction == PanelDirection.Horizontal) {
						le.CalculateLayoutInputHorizontal();
						PriValue(ref min, le.minWidth, lp, ref minPri);
						PriValue(ref preferred, le.preferredWidth, lp, ref prefPri);
						PriValue(ref flexible, le.flexibleWidth, lp, ref flexPri);
					} else { // if (direction == PanelDirection.Vertical)
						le.CalculateLayoutInputVertical();
						PriValue(ref min, le.minHeight, lp, ref minPri);
						PriValue(ref preferred, le.preferredHeight, lp, ref prefPri);
						PriValue(ref flexible, le.flexibleHeight, lp, ref flexPri);
					}
				}
			}
			return new LayoutSizes(obj, min * scaleFactor, Math.Max(min, preferred) *
				scaleFactor, flexible) { ignore = ignore };
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
					var t = item.transform.parent;
					result.Append("- ");
					result.Append(item.name);
					if (t != null) {
						item = t.gameObject;
						if (item != null)
							result.AppendLine();
					} else
						item = null;
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
		/// Derives a font style from an existing style. The font face is copied unchanged,
		/// but the other settings can be optionally modified.
		/// </summary>
		/// <param name="root">The style to use as a template.</param>
		/// <param name="size">The font size, or 0 to use the template size.</param>
		/// <param name="newColor">The font color, or null to use the template color.</param>
		/// <param name="style">The font style, or null to use the template style.</param>
		/// <returns>A copy of the root style with the specified parameters altered.</returns>
		public static TextStyleSetting DeriveStyle(this TextStyleSetting root, int size = 0,
				Color? newColor = null, FontStyles? style = null) {
			if (root == null)
				throw new ArgumentNullException(nameof(root));
			var newStyle = ScriptableObject.CreateInstance<TextStyleSetting>();
			newStyle.enableWordWrapping = root.enableWordWrapping;
			newStyle.style = (style == null) ? root.style : (FontStyles)style;
			newStyle.fontSize = (size > 0) ? size : root.fontSize;
			newStyle.sdfFont = root.sdfFont;
			newStyle.textColor = newColor ?? root.textColor;
			return newStyle;
		}

		/// <summary>
		/// A debug function used to forcefully re-layout a UI.
		/// </summary>
		/// <param name="uiElement">The UI to layout</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject ForceLayoutRebuild(GameObject uiElement) {
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			var rt = uiElement.rectTransform();
			if (rt != null)
				LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
			return uiElement;
		}

		/// <summary>
		/// Retrieves the estimated width of a single string character (uses 'm' as the
		/// standard estimation character) in the given text style.
		/// </summary>
		/// <param name="style">The text style to use.</param>
		/// <returns>The width in pixels that should be allocated.</returns>
		public static float GetEmWidth(TextStyleSetting style) {
			float width = 0.0f;
			if (style == null)
				throw new ArgumentNullException(nameof(style));
			var font = style.sdfFont;
			// Use the em width
			if (font != null && font.characterDictionary.TryGetValue('m', out TMP_Glyph em)) {
				var info = font.fontInfo;
				float ptSize = style.fontSize / (info.PointSize * info.Scale);
				width = em.width * ptSize + style.fontSize * 0.01f * font.normalSpacingOffset;
			}
			return width;
		}

		/// <summary>
		/// Retrieves the estimated height of one line of text in the given text style.
		/// </summary>
		/// <param name="style">The text style to use.</param>
		/// <returns>The height in pixels that should be allocated.</returns>
		public static float GetLineHeight(TextStyleSetting style) {
			float height = 0.0f;
			if (style == null)
				throw new ArgumentNullException(nameof(style));
			var font = style.sdfFont;
			if (font != null) {
				var info = font.fontInfo;
				height = info.LineHeight * style.fontSize / (info.Scale * info.PointSize);
			}
			return height;
		}

		/// <summary>
		/// Creates a string recursively describing the specified GameObject.
		/// </summary>
		/// <param name="root">The root GameObject hierarchy.</param>
		/// <param name="indent">The indentation to use.</param>
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
					Vector2 size = rt.rect.size, aMin = rt.anchorMin, aMax = rt.anchorMax,
						oMin = rt.offsetMin, oMax = rt.offsetMax, pivot = rt.pivot;
					result.Append(sol).AppendFormat(" Rect[Size=({0:F2},{1:F2}) Min=" +
						"({2:F2},{3:F2}) ", size.x, size.y, LayoutUtility.GetMinWidth(rt),
						LayoutUtility.GetMinHeight(rt));
					result.AppendFormat("Preferred=({0:F2},{1:F2}) Flexible=({2:F2}," +
						"{3:F2}) ", LayoutUtility.GetPreferredWidth(rt), LayoutUtility.
						GetPreferredHeight(rt), LayoutUtility.GetFlexibleWidth(rt),
						LayoutUtility.GetFlexibleHeight(rt));
					result.AppendFormat("Pivot=({4:F2},{5:F2}) AnchorMin=({0:F2},{1:F2}) " +
						"AnchorMax=({2:F2},{3:F2}) ", aMin.x, aMin.y, aMax.x, aMax.y, pivot.x,
						pivot.y);
					result.AppendFormat("OffsetMin=({0:F2},{1:F2}) OffsetMax=({2:F2}," +
						"{3:F2})]", oMin.x, oMin.y, oMax.x, oMax.y).AppendLine();
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
		/// Determines the size for a component on a particular axis.
		/// </summary>
		/// <param name="sizes">The declared sizes.</param>
		/// <param name="allocated">The space allocated.</param>
		/// <returns>The size that the component should be.</returns>
		internal static float GetProperSize(LayoutSizes sizes, float allocated) {
			float size = sizes.min, preferred = Math.Max(sizes.preferred, size);
			// Compute size: minimum guaranteed, then preferred, then flexible
			if (allocated > size)
				size = Math.Min(preferred, allocated);
			if (allocated > preferred && sizes.flexible > 0.0f)
				size = allocated;
			return size;
		}

		/// <summary>
		/// Gets the offset required for a component in its box.
		/// </summary>
		/// <param name="alignment">The alignment to use.</param>
		/// <param name="direction">The direction of layout.</param>
		/// <param name="delta">The remaining space.</param>
		/// <returns>The offset from the edge.</returns>
		internal static float GetOffset(TextAnchor alignment, PanelDirection direction,
				float delta) {
			float offset = 0.0f;
			// Based on alignment, offset component
			if (direction == PanelDirection.Horizontal)
				switch (alignment) {
				case TextAnchor.LowerCenter:
				case TextAnchor.MiddleCenter:
				case TextAnchor.UpperCenter:
					offset = delta * 0.5f;
					break;
				case TextAnchor.LowerRight:
				case TextAnchor.MiddleRight:
				case TextAnchor.UpperRight:
					offset = delta;
					break;
				}
			else
				switch (alignment) {
				case TextAnchor.MiddleLeft:
				case TextAnchor.MiddleCenter:
				case TextAnchor.MiddleRight:
					offset = delta * 0.5f;
					break;
				case TextAnchor.LowerLeft:
				case TextAnchor.LowerCenter:
				case TextAnchor.LowerRight:
					offset = delta;
					break;
				}
			return offset;
		}

		/// <summary>
		/// Retrieves the parent of the GameObject, or null if it does not have a parent.
		/// </summary>
		/// <param name="child">The child object.</param>
		/// <returns>The parent of that object, or null if it does not have a parent.</returns>
		public static GameObject GetParent(this GameObject child) {
			GameObject parent = null;
			if (child != null) {
				var newParent = child.transform.parent;
				GameObject go;
				// If parent is disposed, prevent crash
				if (newParent != null && (go = newParent.gameObject) != null)
					parent = go;
			}
			return parent;
		}

		/// <summary>
		/// Insets a child component from its parent, and assigns a fixed size to the parent
		/// equal to the provided size plus the insets.
		/// </summary>
		/// <param name="parent">The parent component.</param>
		/// <param name="child">The child to inset.</param>
		/// <param name="insets">The insets on each side.</param>
		/// <param name="prefSize">The minimum component size.</param>
		/// <returns>The parent component.</returns>
		internal static GameObject InsetChild(GameObject parent, GameObject child,
				Vector2 insets, Vector2 prefSize = default) {
			var rt = child.rectTransform();
			float horizontal = insets.x, vertical = insets.y, width = prefSize.x, height =
				prefSize.y;
			rt.offsetMax = new Vector2(-horizontal, -vertical);
			rt.offsetMin = insets;
			var layout = parent.AddOrGet<LayoutElement>();
			layout.minWidth = layout.preferredWidth = (width <= 0.0f ? LayoutUtility.
				GetPreferredWidth(rt) : width) + horizontal * 2.0f;
			layout.minHeight = layout.preferredHeight = (height <= 0.0f ? LayoutUtility.
				GetPreferredHeight(rt) : height) + vertical * 2.0f;
			return parent;
		}

		/// <summary>
		/// Inserts the side screen at the target location.
		/// </summary>
		/// <param name="screens">The current list of side screens.</param>
		/// <param name="newScreen">The screen to insert.</param>
		/// <param name="targetClassName">The target class name for locating the screen. If this
		/// class is not found, it will be added at the end regardless of insertBefore.</param>
		/// <param name="insertBefore">true to insert before that class, or false to insert after.</param>
		private static void InsertSideScreenContent(IList<SideScreenRef> screens,
				SideScreenRef newScreen, string targetClassName, bool insertBefore) {
			if (screens == null)
				throw new ArgumentNullException(nameof(screens));
			if (newScreen == null)
				throw new ArgumentNullException(nameof(newScreen));
			if (string.IsNullOrEmpty(targetClassName))
				// Add to end by default
				screens.Add(newScreen);
			else {
				int n = screens.Count;
				bool found = false;
				for (int i = 0; i < n; i++) {
					var screen = screens[i];
					var sideScreenPrefab = UIDetours.SS_PREFAB.Get(screen);
					if (sideScreenPrefab != null) {
						var contents = sideScreenPrefab.
							GetComponentsInChildren<SideScreenContent>();
						if (contents == null || contents.Length < 1)
							// Some naughty mod added a prefab with no side screen content!
							LogUIWarning("Could not find SideScreenContent on side screen: " +
								screen.name);
						else if (contents[0].GetType().FullName == targetClassName) {
							// Once the first matching screen is found, perform insertion
							if (insertBefore)
								screens.Insert(i, newScreen);
							else if (i >= n - 1)
								screens.Add(newScreen);
							else
								screens.Insert(i + 1, newScreen);
							found = true;
							break;
						}
					}
				}
				// Warn if no match found
				if (!found) {
					LogUIWarning("No side screen with class name {0} found!".F(
						targetClassName));
					screens.Add(newScreen);
				}
			}
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
		public static Sprite LoadSprite(string path, Vector4 border = default, bool log = true)
		{
			return LoadSprite(Assembly.GetCallingAssembly(), path, border, log);
		}

		/// <summary>
		/// Loads a sprite embedded in the specified assembly as a 9-slice sprite.
		/// 
		/// It may be encoded using PNG, DXT5, or JPG format.
		/// </summary>
		/// <param name="assembly">The assembly containing the image.</param>
		/// <param name="path">The fully qualified path to the image to load.</param>
		/// <param name="border">The sprite border.</param>
		/// <param name="log">true to log the load, or false otherwise.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		internal static Sprite LoadSprite(Assembly assembly, string path, Vector4 border =
				default, bool log = false) {
			// Open a stream to the image
			try {
				using (var stream = assembly.GetManifestResourceStream(path)) {
					if (stream == null)
						throw new ArgumentException("Could not load image: " + path);
					// If len > int.MaxValue we will not go to space today
					int len = (int)stream.Length;
					byte[] buffer = new byte[len];
					var texture = new Texture2D(2, 2);
					len = ReadAllBytes(stream, buffer);
					texture.LoadImage(buffer, false);
					// Create a sprite centered on the texture
					int width = texture.width, height = texture.height;
#if DEBUG
					log = true;
#endif
					if (log)
						LogUIDebug("Loaded sprite: {0} ({1:D}x{2:D}, {3:D} bytes)".F(path,
							width, height, len));
					// pivot is in RELATIVE coordinates!
					return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(
						0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect, border);
				}
			} catch (IOException e) {
				throw new ArgumentException("Could not load image: " + path, e);
			}
		}

		/// <summary>
		/// Loads a sprite from the file system as a 9-slice sprite.
		/// 
		/// It may be encoded using PNG, DXT5, or JPG format.
		/// </summary>
		/// <param name="path">The path to the image to load.</param>
		/// <param name="border">The sprite border.</param>
		/// <returns>The sprite thus loaded, or null if it could not be loaded.</returns>
		public static Sprite LoadSpriteFile(string path, Vector4 border = default) {
			Sprite sprite = null;
			// Open a stream to the image
			try {
				using (var stream = new FileStream(path, FileMode.Open)) {
					// If len > int.MaxValue we will not go to space today
					byte[] buffer = new byte[(int)stream.Length];
					var texture = new Texture2D(2, 2);
					// Load the texture from the stream
					ReadAllBytes(stream, buffer);
					texture.LoadImage(buffer, false);
					// Create a sprite centered on the texture
					int width = texture.width, height = texture.height;
					sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(
						0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect, border);
				}
			} catch (IOException e) {
#if DEBUG
				PUtil.LogExcWarn(e);
#endif
			}
			return sprite;
		}

		/// <summary>
		/// Loads a DDS sprite embedded in the specified assembly as a 9-slice sprite.
		/// 
		/// It must be encoded using the DXT5 format.
		/// </summary>
		/// <param name="assembly">The assembly containing the image.</param>
		/// <param name="path">The fully qualified path to the DDS image to load.</param>
		/// <param name="width">The desired width.</param>
		/// <param name="height">The desired height.</param>
		/// <param name="border">The sprite border.</param>
		/// <returns>The sprite thus loaded.</returns>
		/// <exception cref="ArgumentException">If the image could not be loaded.</exception>
		internal static Sprite LoadSpriteLegacy(Assembly assembly, string path, int width,
				int height, Vector4 border = default) {
			// Open a stream to the image
			try {
				using (var stream = assembly.GetManifestResourceStream(path)) {
					const int SKIP = 128;
					if (stream == null)
						throw new ArgumentException("Could not load image: " + path);
					// If len > int.MaxValue we will not go to space today, skip first 128
					// bytes of stream
					int len = (int)stream.Length - SKIP;
					if (len < 0)
						throw new ArgumentException("Image is too small: " + path);
					byte[] buffer = new byte[len];
					stream.Seek(SKIP, SeekOrigin.Begin);
					len = ReadAllBytes(stream, buffer);
					// Load the texture from the stream
					var texture = new Texture2D(width, height, TextureFormat.DXT5, false);
					texture.LoadRawTextureData(buffer);
					texture.Apply(true, true);
					// Create a sprite centered on the texture
					LogUIDebug("Loaded sprite: {0} ({1:D}x{2:D}, {3:D} bytes)".F(path,
						width, height, len));
					// pivot is in RELATIVE coordinates!
					return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(
						0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect, border);
				}
			} catch (IOException e) {
				throw new ArgumentException("Could not load image: " + path, e);
			}
		}

		/// <summary>
		/// Reads as much of the array as possible from a stream.
		/// </summary>
		/// <param name="stream">The stream to be read.</param>
		/// <param name="data">The location to store the data read.</param>
		/// <returns>The number of bytes actually read.</returns>
		private static int ReadAllBytes(Stream stream, byte[] data) {
			int offset = 0, len = data.Length, n;
			do {
				n = stream.Read(data, offset, len - offset);
				offset += n;
			} while (n > 0 && offset < len);
			return offset;
		}

		/// <summary>
		/// Logs a debug message encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogUIDebug(string message) {
			Debug.LogFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly().GetName().
				Name ?? "?", message);
		}

		/// <summary>
		/// Logs a warning encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogUIWarning(string message) {
			Debug.LogWarningFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly().
				GetName().Name ?? "?", message);
		}

		/// <summary>
		/// Aggregates layout values, replacing the value if a higher priority value is given
		/// and otherwise taking the largest value.
		/// </summary>
		/// <param name="value">The current value.</param>
		/// <param name="newValue">The candidate new value. No operation if this is less than zero.</param>
		/// <param name="newPri">The new value's layout priority.</param>
		/// <param name="pri">The current value's priority</param>
		private static void PriValue(ref float value, float newValue, int newPri, ref int pri)
		{
			int thisPri = pri;
			if (newValue >= 0.0f) {
				if (newPri > thisPri) {
					// Priority override?
					pri = newPri;
					value = newValue;
				} else if (newValue > value && newPri == thisPri)
					// Same priority and higher value?
					value = newValue;
			}
		}
		
		/// <summary>
		/// Sets a UI element's flexible size.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="flexSize">The flexible size as a ratio.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetFlexUISize(this GameObject uiElement, Vector2 flexSize) {
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			if (uiElement.TryGetComponent(out ISettableFlexSize fs)) {
				// Avoid duplicate LayoutElement on layouts
				fs.flexibleWidth = flexSize.x;
				fs.flexibleHeight = flexSize.y;
			} else {
				var le = uiElement.AddOrGet<LayoutElement>();
				le.flexibleWidth = flexSize.x;
				le.flexibleHeight = flexSize.y;
			}
			return uiElement;
		}

		/// <summary>
		/// Sets a UI element's minimum size.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="minSize">The minimum size in units.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetMinUISize(this GameObject uiElement, Vector2 minSize) {
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			float minX = minSize.x, minY = minSize.y;
			if (minX > 0.0f || minY > 0.0f) {
				var le = uiElement.AddOrGet<LayoutElement>();
				if (minX > 0.0f)
					le.minWidth = minX;
				if (minY > 0.0f)
					le.minHeight = minY;
			}
			return uiElement;
		}

		/// <summary>
		/// Immediately resizes a UI element. Uses the element's current anchors. If a
		/// dimension of the size is negative, the component will not be resized in that
		/// dimension.
		/// 
		/// If addLayout is true, a layout element is also added so that future auto layout
		/// calls will try to maintain that size. Do not set addLayout to true if either of
		/// the size dimensions are negative, as laying out components with a negative
		/// preferred size may cause unexpected behavior.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="size">The new element size.</param>
		/// <param name="addLayout">true to add a layout element with that size, or false
		/// otherwise.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetUISize(this GameObject uiElement, Vector2 size,
				bool addLayout = false) {
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			var transform = uiElement.rectTransform();
			float width = size.x, height = size.y;
			if (transform != null) {
				if (width >= 0.0f)
					transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
				if (height >= 0.0f)
					transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
			}
			if (addLayout) {
				var le = uiElement.AddOrGet<LayoutElement>();
				// Set minimum and preferred size
				le.minWidth = width;
				le.minHeight = height;
				le.preferredWidth = width;
				le.preferredHeight = height;
				le.flexibleHeight = 0.0f;
				le.flexibleWidth = 0.0f;
			}
			return uiElement;
		}
	}
}
