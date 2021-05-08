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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// A tool mode used in custom tool menus. Shown in the options in the bottom right.
	/// </summary>
	public sealed class PToolMode {
		/// <summary>
		/// Adds tool icons to the registry in ToolMenu.OnPrefabInit, if necessary.
		/// </summary>
		/// <param name="target">The icon list where the icons will be added.</param>
		internal static void AddAllToolIcons(ICollection<Sprite> target) {
			// TODO Vanilla/DLC code
			lock (PSharedData.GetLock(PRegistry.KEY_TOOLICONS_LOCK)) {
				var table = PSharedData.GetData<IList<Sprite>>(PRegistry.KEY_TOOLICONS_LIST);
				if (table != null)
					foreach (var sprite in table)
						if (sprite != null)
							target?.Add(sprite);
			}
		}

		/// <summary>
		/// Adds tool icons to the Assets registry of sprites, for use in later versions of
		/// the DLC.
		/// </summary>
		internal static void AddAllToolIconsToAssets() {
			lock (PSharedData.GetLock(PRegistry.KEY_TOOLICONS_LOCK)) {
				var table = PSharedData.GetData<IList<Sprite>>(PRegistry.KEY_TOOLICONS_LIST);
				if (table != null)
					foreach (var sprite in table)
						if (sprite != null)
							Assets.Sprites.Add(sprite.name, sprite);
			}
		}

		/// <summary>
		/// Reports true if and only if custom tool icons have been registered for display.
		/// </summary>
		/// <returns>true if tool icons need to be loaded (not related to whether they
		/// need to be patched in!), or false if none are registered</returns>
		internal static bool HasToolIcons {
			get {
				bool icons = false;
				lock (PSharedData.GetLock(PRegistry.KEY_TOOLICONS_LOCK)) {
					var tbl = PSharedData.GetData<IList<Sprite>>(PRegistry.KEY_TOOLICONS_LIST);
					icons = tbl != null && tbl.Count > 0;
				}
				return icons;
			}
		}

		/// <summary>
		/// Sets up tool options in the tool parameter menu.
		/// </summary>
		/// <param name="menu">The menu to configure.</param>
		/// <param name="options">The available modes.</param>
		/// <returns>A dictionary which is updated in real time to contain the actual state of each mode.</returns>
		public static IDictionary<string, ToolParameterMenu.ToggleState> PopulateMenu(
				ToolParameterMenu menu, ICollection<PToolMode> options) {
			if (options == null)
				throw new ArgumentNullException("options");
			var kOpt = new Dictionary<string, ToolParameterMenu.ToggleState>(options.Count);
			// Add to Klei format, yes it loses the order but it means less of a mess
			foreach (var option in options) {
				string key = option.Key;
				if (!string.IsNullOrEmpty(option.Title))
					Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + key, option.Title);
				kOpt.Add(key, option.State);
			}
			menu.PopulateMenu(kOpt);
			return kOpt;
		}

		/// <summary>
		/// Registers a tool with the game. It still must be added to a tool collection to be
		/// visible.
		/// </summary>
		/// <typeparam name="T">The tool type to register.</typeparam>
		/// <param name="controller">The player controller which will be its parent; consider
		/// using in a postfix on PlayerController.OnPrefabInit.</param>
		public static void RegisterTool<T>(PlayerController controller) where T : InterfaceTool
		{
			if (controller == null)
				throw new ArgumentNullException("controller");
			// Create list so that new tool can be appended at the end
			var interfaceTools = ListPool<InterfaceTool, PlayerController>.Allocate();
			interfaceTools.AddRange(controller.tools);
			var newTool = new UnityEngine.GameObject(typeof(T).Name);
			newTool.AddComponent<T>();
			// Reparent tool to the player controller, then enable/disable to load it
			newTool.transform.SetParent(controller.gameObject.transform);
			newTool.gameObject.SetActive(true);
			newTool.gameObject.SetActive(false);
			// Add tool to tool list
			interfaceTools.Add(newTool.GetComponent<InterfaceTool>());
			controller.tools = interfaceTools.ToArray();
			interfaceTools.Recycle();
		}

		/// <summary>
		/// Adds a tool icon to the registry. In early DLC versions and the vanilla game, the
		/// tool menu had its own sprite list which was searched for tool icons, which was
		/// eventually removed.
		/// 
		/// For both versions of the game, the sprite's name field must match the name used
		/// in the tool collection's icon name declaration.
		/// 
		/// This method must be used in OnLoad after PLib is initialized.
		/// </summary>
		/// <param name="icon">The tool icon which will be used.</param>
		public static void RegisterToolIcon(Sprite icon) {
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using AddToolIcon!");
			}
			lock (PSharedData.GetLock(PRegistry.KEY_TOOLICONS_LOCK)) {
				var table = PSharedData.GetData<IList<Sprite>>(PRegistry.KEY_TOOLICONS_LIST);
				if (table == null)
					PSharedData.PutData(PRegistry.KEY_TOOLICONS_LIST, table =
						new List<Sprite>(8));
				if (icon != null) {
					table.Add(icon);
#if DEBUG
					PUtil.LogDebug("Added icon " + icon.name + " to tool menu");
#endif
				}
			}
		}

		/// <summary>
		/// A unique key used to identify this mode.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// The current state of this tool mode.
		/// </summary>
		public ToolParameterMenu.ToggleState State { get; }

		/// <summary>
		/// The title displayed on-screen for this mode.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// Creates a new tool mode entry.
		/// </summary>
		/// <param name="key">The key which identifies this tool mode.</param>
		/// <param name="title">The title to be displayed.</param>
		/// <param name="state">The initial state, default Off.</param>
		public PToolMode(string key, string title, ToolParameterMenu.ToggleState state =
				ToolParameterMenu.ToggleState.Off) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			Key = key;
			State = state;
			Title = title;
		}

		public override bool Equals(object obj) {
			return obj is PToolMode other && other.Key == Key;
		}

		public override int GetHashCode() {
			return Key.GetHashCode();
		}

		public override string ToString() {
			return "{0} ({1})".F(Key, Title);
		}
	}
}
