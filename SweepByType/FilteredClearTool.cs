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
using PeterHan.PLib.UI;
using UnityEngine;

namespace PeterHan.SweepByType {
	/// <summary>
	/// A version of ClearTool (sweep) that allows filtering.
	/// </summary>
	public sealed class FilteredClearTool : DragTool {
		/// <summary>
		/// The singleton instance of this tool.
		/// </summary>
		public static FilteredClearTool Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			var inst = Instance;
			if (inst != null) {
				Destroy(inst);
				Instance = null;
			}
		}

		/// <summary>
		/// The types to sweep.
		/// </summary>
		private HashSetPool<Tag, FilteredClearTool>.PooledHashSet cachedTypes;

		/// <summary>
		/// Allows selection of the type to sweep.
		/// </summary>
		internal TypeSelectControl TypeSelect { get; private set; }

		internal FilteredClearTool() {
			cachedTypes = null;
			TypeSelect = null;
		}

		/// <summary>
		/// Destroys the cached list after a drag completes.
		/// </summary>
		private void DoneDrag() {
			if (cachedTypes != null) {
				cachedTypes.Recycle();
				cachedTypes = null;
			}
		}

		/// <summary>
		/// Marks a debris item to be swept if it matches the filters.
		/// </summary>
		/// <param name="content">The item to sweep.</param>
		/// <param name="priority">The priority to set the sweep errand.</param>
		private void MarkForClear(GameObject content, PrioritySetting priority) {
			var cc = content.GetComponent<Clearable>();
			if (cc != null && cc.isClearable && cachedTypes.Contains(content.PrefabID())) {
				// Parameter is whether to force, not remove sweep errand!
				cc.MarkForClear(false);
				var pr = content.GetComponent<Prioritizable>();
				if (pr != null)
					pr.SetMasterPriority(priority);
			}
		}

		protected override void OnActivateTool() {
			var menu = ToolMenu.Instance;
			base.OnActivateTool();
			// Update only on tool activation to improve performance
			if (TypeSelect != null) {
				var root = TypeSelect.RootPanel;
				TypeSelect.Update();
				PUIElements.SetParent(root, menu.gameObject);
				root.transform.SetAsFirstSibling();
			}
			menu.PriorityScreen.Show(true);
		}

		protected override void OnCleanUp() {
			base.OnCleanUp();
			// Clean up everything needed
			if (TypeSelect != null) {
				Destroy(TypeSelect.RootPanel);
				TypeSelect = null;
			}
			DoneDrag();
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			var menu = ToolMenu.Instance;
			// Unparent but do not dispose
			if (TypeSelect != null)
				PUIElements.SetParent(TypeSelect.RootPanel, null);
			menu.PriorityScreen.Show(false);
			base.OnDeactivateTool(newTool);
		}

		protected override void OnDragComplete(Vector3 cursorDown, Vector3 cursorUp) {
			DoneDrag();
		}

		protected override void OnDragTool(int cell, int distFromOrigin) {
			var gameObject = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (gameObject != null && TypeSelect != null) {
				// Linked list of debris in layer 3
				var objectListNode = gameObject.GetComponent<Pickupable>().objectLayerListItem;
				var priority = ToolMenu.Instance.PriorityScreen.GetLastSelectedPriority();
				if (cachedTypes == null) {
					// Build the list
					cachedTypes = HashSetPool<Tag, FilteredClearTool>.Allocate();
					TypeSelect.AddTypesToSweep(cachedTypes);
				}
				while (objectListNode != null) {
					var content = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					// Ignore Duplicants
					if (content != null && content.GetComponent<MinionIdentity>() == null)
						MarkForClear(content, priority);
				}
			}
		}

		protected override void OnPrefabInit() {
			var us = Traverse.Create(this);
			base.OnPrefabInit();
			var inst = ClearTool.Instance;
			Instance = this;
			interceptNumberKeysForPriority = true;
			populateHitsList = true;
			// Get the cursor from the existing sweep tool
			if (inst != null) {
				var trSweep = Traverse.Create(inst);
				gameObject.AddComponent<FilteredClearHover>();
				cursor = trSweep.GetField<Texture2D>("cursor");
				us.SetField("boxCursor", cursor);
				// Get the area visualizer from the sweep tool
				var avTemplate = trSweep.GetField<GameObject>("areaVisualizer");
				if (avTemplate != null) {
					var areaVisualizer = Util.KInstantiate(avTemplate, gameObject,
						"FilteredClearToolAreaVisualizer");
					areaVisualizer.SetActive(false);
					areaVisualizerSpriteRenderer = areaVisualizer.GetComponent<
						SpriteRenderer>();
					// The visualizer is private so we need to set it with reflection
					us.SetField("areaVisualizer", areaVisualizer);
					us.SetField("areaVisualizerTextPrefab", trSweep.GetField<GameObject>(
						"areaVisualizerTextPrefab"));
				}
				visualizer = Util.KInstantiate(trSweep.GetField<GameObject>("visualizer"),
					gameObject, "FilteredClearToolSprite");
				visualizer.SetActive(false);
			}
			// Allow icons to be disabled
			TypeSelect = new TypeSelectControl(SweepByTypePatches.Options?.DisableIcons ??
				false);
		}
	}
}
