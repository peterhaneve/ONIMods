/*
 * Copyright 2026 Peter Han, Nick Weedon
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
using PeterHan.PLib.Detours;
using PeterHan.PLib.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.ResearchQueue {
	/// <summary>
	/// The research queue, shown as a tab in the Research screen's left sidebar beside the
	/// category tree. Rows can be dragged to reorder the queue; a move carries its chain
	/// (see QueueReorder). Hovering a row shows its progress per research type.
	/// </summary>
	internal sealed class ResearchQueuePanel : MonoBehaviour {
		private const float ROW_MIN_HEIGHT = 28.0f;

		private const float ICON_SIZE = 24.0f;

		private const int ICONS_PER_LINE = 9;

		/// <summary>
		/// Dragging within this distance of the list's top or bottom edge scrolls it, at
		/// this speed (UI units per second).
		/// </summary>
		private const float SCROLL_EDGE = 32.0f, SCROLL_SPEED = 500.0f;

		private static readonly Color ROW_COLOR = new Color(0.24f, 0.26f, 0.34f, 1.0f);

		private static readonly Color ROW_ACTIVE_COLOR = new Color(0.30f, 0.38f, 0.52f, 1.0f);

		private static readonly Color ROW_CHAIN_COLOR = new Color(0.55f, 0.42f, 0.18f, 1.0f);

		private static readonly Color INDICATOR_COLOR = new Color(1.0f, 0.85f, 0.3f, 1.0f);

		private static readonly IDetouredField<ResearchScreenSideBar, GameObject> PROJECTS =
			PDetours.DetourField<ResearchScreenSideBar, GameObject>("projectsContainer");

		private static readonly IDetouredField<ResearchScreenSideBar, ResearchScreen>
			SIDEBAR_SCREEN = PDetours.DetourField<ResearchScreenSideBar, ResearchScreen>(
			"researchScreen");

		/// <summary>
		/// The panel for the currently spawned Research screen.
		/// </summary>
		internal static ResearchQueuePanel Instance { get; private set; }

		/// <summary>
		/// The selected tab, kept across screen instances (true = the queue).
		/// </summary>
		private static bool queueTabSelected;

		/// <summary>
		/// Creates the tab strip and the queue list in the Research screen's sidebar. The
		/// category tree's scroll view is moved into a new host that takes its place, below
		/// the tab strip; the queue list shares the same area.
		/// </summary>
		internal static void Create(ResearchScreenSideBar sideBar) {
			var screen = SIDEBAR_SCREEN.Get(sideBar);
			var projects = PROJECTS.Get(sideBar);
			if (projects == null) {
				PUtil.LogWarning("Research sidebar has no projects container; queue tab disabled");
				return;
			}
			var scroll = projects.GetComponentInParent<ScrollRect>(true);
			var treeRoot = (scroll != null) ? scroll.gameObject : projects;
			var treeRT = treeRoot.rectTransform();
			var parent = treeRT.parent;
			// Snapshot the tree's layout so the host can stand in for it
			float minW = LayoutUtility.GetMinWidth(treeRT), minH = LayoutUtility.GetMinHeight(
				treeRT), prefW = LayoutUtility.GetPreferredWidth(treeRT), prefH =
				LayoutUtility.GetPreferredHeight(treeRT), flexW = LayoutUtility.
				GetFlexibleWidth(treeRT), flexH = LayoutUtility.GetFlexibleHeight(treeRT);
			bool ignore = treeRoot.TryGetComponent(out LayoutElement treeLE) && treeLE.
				ignoreLayout;
			var host = new GameObject("ResearchQueueTabHost", typeof(RectTransform));
			host.layer = treeRoot.layer;
			var hostRT = host.rectTransform();
			hostRT.SetParent(parent, false);
			hostRT.SetSiblingIndex(treeRT.GetSiblingIndex());
			hostRT.anchorMin = treeRT.anchorMin;
			hostRT.anchorMax = treeRT.anchorMax;
			hostRT.pivot = treeRT.pivot;
			hostRT.anchoredPosition = treeRT.anchoredPosition;
			hostRT.sizeDelta = treeRT.sizeDelta;
			hostRT.localScale = treeRT.localScale;
			var hostLE = host.AddComponent<LayoutElement>();
			hostLE.minWidth = minW;
			hostLE.minHeight = minH;
			hostLE.preferredWidth = prefW;
			hostLE.preferredHeight = prefH;
			hostLE.flexibleWidth = flexW;
			hostLE.flexibleHeight = flexH;
			hostLE.ignoreLayout = ignore;
			var vlg = host.AddComponent<VerticalLayoutGroup>();
			vlg.childControlWidth = true;
			vlg.childControlHeight = true;
			vlg.childForceExpandWidth = true;
			vlg.childForceExpandHeight = false;
			vlg.spacing = 4.0f;
			vlg.padding = new RectOffset(0, 0, 4, 0);
			// Tab strip
			GameObject treeTab = null, queueTab = null;
			ResearchQueuePanel panel = null;
			var tabs = new PPanel("ResearchQueueTabs") {
				Direction = PanelDirection.Horizontal, Alignment = TextAnchor.MiddleCenter,
				Spacing = 4, FlexSize = Vector2.right, Margin = new RectOffset(4, 4, 0, 0)
			}.AddChild(new PButton("TreeTab") {
				Text = ResearchQueueStrings.TreeTab, FlexSize = Vector2.right,
				ToolTip = ResearchQueueStrings.TreeTabTooltip,
				OnClick = _ => panel?.SelectTab(false)
			}.AddOnRealize(obj => treeTab = obj)).AddChild(new PButton("QueueTab") {
				Text = ResearchQueueStrings.QueueTab, FlexSize = Vector2.right,
				ToolTip = ResearchQueueStrings.QueueTabTooltip,
				OnClick = _ => panel?.SelectTab(true)
			}.AddOnRealize(obj => queueTab = obj)).Build();
			tabs.transform.SetParent(hostRT, false);
			var tabsLE = tabs.AddOrGet<LayoutElement>();
			tabsLE.flexibleHeight = 0.0f;
			tabsLE.minHeight = ROW_MIN_HEIGHT;
			// Content area shared by the tree and the queue
			var content = new GameObject("ResearchQueueTabContent", typeof(RectTransform),
				typeof(LayoutElement));
			content.layer = treeRoot.layer;
			var contentLE = content.GetComponent<LayoutElement>();
			contentLE.flexibleWidth = 1.0f;
			contentLE.flexibleHeight = 1.0f;
			var contentRT = content.rectTransform();
			contentRT.SetParent(hostRT, false);
			treeRT.SetParent(contentRT, false);
			Stretch(treeRT);
			// A scroll bar that sits beside the tree rather than inside it must hide with it
			GameObject treeBar = null;
			if (scroll != null && scroll.verticalScrollbar != null && !scroll.
					verticalScrollbar.transform.IsChildOf(treeRT))
				treeBar = scroll.verticalScrollbar.gameObject;
			// Queue list
			GameObject rowsObj = null;
			var rowsPanel = new PPanel("Rows") {
				Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperLeft,
				Spacing = 2, FlexSize = Vector2.right, Margin = new RectOffset(2, 2, 2, 2),
				// A background makes the whole list area receive scroll events
				BackColor = PUITuning.Colors.DialogDarkBackground
			}.AddOnRealize(obj => rowsObj = obj);
			var root = new PPanel("ResearchQueuePanel") {
				Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperCenter,
				Margin = new RectOffset(2, 2, 2, 2), FlexSize = Vector2.one,
				// Must stay dynamic: a locked layout is sized once at build time (with an
				// empty list) and never re-laid out when the anchors resize it
				BackColor = PUITuning.Colors.DialogDarkBackground, DynamicSize = true
			}.AddChild(new PScrollPane("Scroll") {
				Child = rowsPanel, ScrollHorizontal = false, ScrollVertical = true,
				AlwaysShowVertical = true, TrackSize = 8.0f, FlexSize = Vector2.one,
				BackColor = PUITuning.Colors.DialogDarkBackground
			});
			var go = root.Build();
			var rootRT = go.rectTransform();
			rootRT.SetParent(contentRT, false);
			Stretch(rootRT);
			go.AddComponent<DragSink>();
			panel = go.AddComponent<ResearchQueuePanel>();
			panel.screen = screen;
			panel.rowsParent = rowsObj;
			panel.treeRoot = treeRoot;
			panel.treeBar = treeBar;
			panel.treeTab = treeTab;
			panel.queueTab = queueTab;
			panel.queueTabText = queueTab?.GetComponentInChildren<LocText>();
			panel.Initialize();
			// ResearchScreen.OnSpawn ends with Show(false)
			panel.OnScreenShown(sideBar.IsScreenActive());
		}

		private static void Stretch(RectTransform rt) {
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			rt.localScale = Vector3.one;
		}

		private ResearchScreen screen;

		/// <summary>
		/// The category tree's scroll view, and its scroll bar if that lives outside it.
		/// </summary>
		private GameObject treeRoot, treeBar;

		private GameObject treeTab, queueTab;

		private LocText queueTabText;

		/// <summary>
		/// Whether the Research screen is showing.
		/// </summary>
		private bool screenShown;

		/// <summary>
		/// The container holding the rows.
		/// </summary>
		private GameObject rowsParent;

		/// <summary>
		/// The rows, in queue order.
		/// </summary>
		private readonly List<QueueRow> rows = new List<QueueRow>();

		/// <summary>
		/// Shown, centred over the list, when the queue is empty.
		/// </summary>
		private GameObject emptyState;

		/// <summary>
		/// Drawn between rows at the drop position while dragging.
		/// </summary>
		private RectTransform indicator;

		/// <summary>
		/// The translucent copy of the dragged row that follows the cursor.
		/// </summary>
		private RectTransform ghost;

		/// <summary>
		/// The progress popup shown while a row is hovered.
		/// </summary>
		private ProgressPopup popup;

		/// <summary>
		/// The queue index of the row being dragged, or -1.
		/// </summary>
		private int dragFrom = -1;

		/// <summary>
		/// The current drop slot ("insert before row n", 0 .. rows.Count).
		/// </summary>
		private int dragSlot = -1;

		/// <summary>
		/// Set when the queue changed during a drag; the rows are rebuilt on drop.
		/// </summary>
		private bool dirty;

		/// <summary>
		/// The list's scroll view, and the pointer data of the drag in progress.
		/// </summary>
		private ScrollRect scroll;

		private PointerEventData lastDrag;

		private int activeChangedHandle = -1, completeHandle = -1, pointsHandle = -1;

		internal bool Dragging => dragFrom >= 0;

		private void Initialize() {
			var research = Research.Instance;
			if (research != null) {
				activeChangedHandle = research.gameObject.Subscribe((int)GameHashes.
					ActiveResearchChanged, OnQueueChanged);
				pointsHandle = research.gameObject.Subscribe((int)GameHashes.
					ResearchPointsChanged, OnPointsChanged);
			}
			if (Game.Instance != null)
				completeHandle = Game.Instance.gameObject.Subscribe((int)GameHashes.
					ResearchComplete, OnQueueChanged);
			// Drop position indicator, outside the layout
			var indObj = new GameObject("DropIndicator", typeof(RectTransform), typeof(
				Image), typeof(LayoutElement));
			indObj.GetComponent<LayoutElement>().ignoreLayout = true;
			var img = indObj.GetComponent<Image>();
			img.color = INDICATOR_COLOR;
			img.raycastTarget = false;
			indicator = indObj.rectTransform();
			indicator.SetParent(rowsParent.transform, false);
			indicator.anchorMin = new Vector2(0.0f, 1.0f);
			indicator.anchorMax = new Vector2(1.0f, 1.0f);
			indicator.pivot = new Vector2(0.5f, 0.5f);
			indicator.sizeDelta = new Vector2(0.0f, 3.0f);
			indObj.SetActive(false);
			emptyState = CreateEmptyState();
			popup = new ProgressPopup(screen != null ? screen.transform : transform.parent);
			scroll = rowsParent.GetComponentInParent<ScrollRect>(true);
			Refresh();
			// Only once fully built, so the patches never see a half-made panel
			Instance = this;
		}

		private void OnDestroy() {
			var research = Research.Instance;
			if (research != null) {
				research.gameObject.Unsubscribe(activeChangedHandle);
				research.gameObject.Unsubscribe(pointsHandle);
			}
			if (Game.Instance != null)
				Game.Instance.gameObject.Unsubscribe(completeHandle);
			popup?.Destroy();
			if (Instance == this)
				Instance = null;
		}

		/// <summary>
		/// Called when the Research screen shows or hides. The screen hides with a canvas
		/// group, which does not stop the queue list's own canvas from taking raycasts, so
		/// the list is deactivated as well.
		/// </summary>
		internal void OnScreenShown(bool show) {
			bool wasActive = gameObject.activeSelf;
			screenShown = show;
			if (!show)
				popup?.Hide();
			// Activating the list rebuilds it (OnEnable)
			ApplyTab();
			if (show && wasActive)
				Refresh();
		}

		/// <summary>
		/// Shows the queue (true) or the category tree (false).
		/// </summary>
		internal void SelectTab(bool queue) {
			if (queueTabSelected != queue) {
				queueTabSelected = queue;
				popup?.Hide();
			}
			ApplyTab();
		}

		private void ApplyTab() {
			bool queue = queueTabSelected;
			gameObject.SetActive(queue && screenShown);
			if (treeRoot != null)
				treeRoot.SetActive(!queue);
			if (treeBar != null)
				treeBar.SetActive(!queue);
			SetTabStyle(treeTab, !queue);
			SetTabStyle(queueTab, queue);
		}

		private static void SetTabStyle(GameObject tab, bool selected) {
			if (tab != null && tab.TryGetComponent(out KImage bg)) {
				bg.colorStyleSetting = selected ? PUITuning.Colors.ButtonPinkStyle :
					PUITuning.Colors.ButtonBlueStyle;
				bg.ApplyColorStyleSetting();
			}
		}

		// The event handlers run inside the game's own triggers (e.g. SetActiveResearch),
		// which have no exception handling: never let the list break research selection

		private void OnQueueChanged(object _) {
			try {
				if (Dragging)
					dirty = true;
				else
					Refresh();
			} catch (System.Exception e) {
				PUtil.LogException(e);
			}
		}

		private void OnPointsChanged(object _) {
			try {
				popup?.UpdateProgress();
			} catch (System.Exception e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Rebuilds the rows from the current research queue.
		/// </summary>
		internal void Refresh() {
			// Rebuilding the rows ends any drag in progress
			if (Dragging)
				CancelDrag();
			dirty = false;
			var queue = ResearchQueuePatches.GetQueue();
			int n = queue?.Count ?? 0;
			if (queueTabText != null)
			{
				// LocString.ToString() looks up its (unregistered) key and gives "MISSING.";
				// the implicit string conversion gives the text itself
				string tabName = ResearchQueueStrings.QueueTab;
				queueTabText.SetText(n > 0 ? string.Format(ResearchQueueStrings.
					QueueTabCount, tabName, n) : tabName);
			}
			// Rows are only built while the list is showing
			if (!isActiveAndEnabled)
				return;
			popup?.Hide();
			// Destroy is deferred to the end of the frame: deactivate first so the layout
			// drops the old rows immediately
			foreach (var row in rows)
				if (row != null) {
					row.gameObject.SetActive(false);
					Destroy(row.gameObject);
				}
			rows.Clear();
			for (int i = 0; i < n; i++)
				rows.Add(CreateRow(queue[i], i));
			if (emptyState != null)
				emptyState.SetActive(n == 0);
			indicator.SetAsLastSibling();
			ResetRowColors();
			LayoutRebuilder.MarkLayoutForRebuild(rowsParent.rectTransform());
		}

		private void OnEnable() {
			// Rows were skipped while the list was hidden
			if (rowsParent != null)
				Refresh();
		}

		private void OnDisable() {
			// A hidden list gets no end-drag event (screen closed or tab switched mid-drag)
			if (Dragging)
				CancelDrag();
		}

		/// <summary>
		/// While dragging, scrolls the list when the pointer is near its top or bottom edge,
		/// and keeps the drop slot current when the list scrolls under a still pointer.
		/// </summary>
		private void Update() {
			if (!Dragging || lastDrag == null)
				return;
			if (scroll != null && scroll.content != null) {
				var view = scroll.viewport != null ? scroll.viewport : scroll.transform as
					RectTransform;
				if (RectTransformUtility.ScreenPointToLocalPointInRectangle(view, lastDrag.
						position, lastDrag.pressEventCamera, out Vector2 p)) {
					var r = view.rect;
					float dir = (p.y > r.yMax - SCROLL_EDGE) ? 1.0f : ((p.y < r.yMin +
						SCROLL_EDGE) ? -1.0f : 0.0f);
					float overflow = scroll.content.rect.height - r.height;
					if (dir != 0.0f && overflow > 0.0f)
						scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.
							verticalNormalizedPosition + dir * SCROLL_SPEED * Time.
							unscaledDeltaTime / overflow);
				}
			}
			Drag(lastDrag);
		}

		private static readonly Color EMPTY_TITLE_COLOR = new Color(0.63f, 0.66f, 0.72f);

		private static readonly Color EMPTY_HINT_COLOR = new Color(0.54f, 0.57f, 0.64f);

		private static readonly Color KEY_TEXT_COLOR = new Color(0.88f, 0.63f, 0.19f);

		private static readonly Color KEY_BORDER_COLOR = new Color(0.49f, 0.52f, 0.59f);

		private static readonly Color KEY_FACE_COLOR = new Color(0.21f, 0.24f, 0.28f);

		/// <summary>
		/// Builds the empty-queue message: a faded research icon, a title and the controls
		/// shown as key caps, centred over the list (a little above the middle).
		/// </summary>
		private GameObject CreateEmptyState() {
			var hintStyle = PUITuning.Fonts.UILightStyle.DeriveStyle(size: 14, newColor:
				EMPTY_HINT_COLOR);
			var panel = new PPanel("EmptyQueue") {
				Direction = PanelDirection.Vertical, Alignment = TextAnchor.MiddleCenter,
				Spacing = 10, FlexSize = Vector2.one, DynamicSize = true
			};
			var types = Research.Instance?.researchTypes?.Types;
			var icon = (types != null && types.Count > 0) ? types[0].sprite : null;
			if (icon != null)
				panel.AddChild(new PLabel("Icon") {
					Sprite = icon, SpriteSize = new Vector2(46.0f, 46.0f),
					MaintainSpriteAspect = true, SpriteTint = new Color(1.0f, 1.0f, 1.0f,
					0.35f)
				});
			panel.AddChild(new PLabel("Title") {
				Text = ResearchQueueStrings.EmptyTitle, TextAlignment = TextAnchor.MiddleCenter,
				TextStyle = PUITuning.Fonts.UILightStyle.DeriveStyle(size: 22, newColor:
				EMPTY_TITLE_COLOR), Margin = new RectOffset(0, 0, 0, 4)
			});
			panel.AddChild(new PPanel("ClickHint") {
				Direction = PanelDirection.Horizontal, Alignment = TextAnchor.MiddleCenter,
				Spacing = 5
			}.AddChild(KeyCap(ResearchQueueStrings.KeyClick)).AddChild(new PLabel("Text") {
				Text = ResearchQueueStrings.EmptyClickHint, TextStyle = hintStyle
			}));
			panel.AddChild(new PPanel("ShiftHint") {
				Direction = PanelDirection.Horizontal, Alignment = TextAnchor.MiddleCenter,
				Spacing = 5
			}.AddChild(KeyCap(ResearchQueueStrings.KeyShift)).AddChild(new PLabel("Plus") {
				Text = "+", TextStyle = hintStyle
			}).AddChild(KeyCap(ResearchQueueStrings.KeyClick)).AddChild(new PLabel("Text") {
				Text = ResearchQueueStrings.EmptyShiftHint, TextStyle = hintStyle
			}));
			var obj = panel.AddTo(gameObject);
			// Over the list, outside its layout; the bottom inset lifts it above centre
			obj.AddOrGet<LayoutElement>().ignoreLayout = true;
			var rt = obj.rectTransform();
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.offsetMin = new Vector2(8.0f, 80.0f);
			rt.offsetMax = new Vector2(-16.0f, 0.0f);
			foreach (var g in obj.GetComponentsInChildren<Graphic>(true))
				g.raycastTarget = false;
			obj.transform.SetAsLastSibling();
			obj.SetActive(false);
			return obj;
		}

		/// <summary>
		/// A key cap: amber text on a dark face with a lighter edge, thicker at the bottom.
		/// </summary>
		private static PPanel KeyCap(string key) {
			return new PPanel("Key_" + key) {
				BackColor = KEY_BORDER_COLOR, Margin = new RectOffset(1, 1, 1, 2)
			}.AddChild(new PLabel("Face") {
				Text = key, BackColor = KEY_FACE_COLOR, Margin = new RectOffset(5, 5, 1, 1),
				TextStyle = PUITuning.Fonts.UILightStyle.DeriveStyle(size: 13, newColor:
				KEY_TEXT_COLOR)
			});
		}

		private QueueRow CreateRow(TechInstance inst, int index) {
			LocText numberText = null;
			var tech = inst.tech;
			var top = new PPanel("Top") {
				Direction = PanelDirection.Horizontal, Alignment = TextAnchor.MiddleLeft,
				Spacing = 4, FlexSize = Vector2.right
			}.AddChild(new PLabel("Number") {
				TextStyle = PUITuning.Fonts.UILightStyle, TextAlignment = TextAnchor.
				MiddleRight, Text = "00."
			}.AddOnRealize(obj => numberText = obj.GetComponentInChildren<LocText>())).
			AddChild(new PLabel("Name") {
				TextStyle = PUITuning.Fonts.UILightStyle, TextAlignment = TextAnchor.
				MiddleLeft, FlexSize = Vector2.right, Text = tech.Name
			}).AddChild(new PButton("Remove") {
				Text = "X", ToolTip = ResearchQueueStrings.RemoveTooltip,
				OnClick = _ => ResearchQueuePatches.DequeueTech(tech)
			}.SetKleiPinkStyle());
			var rowPanel = new PPanel("Row_" + tech.Id) {
				Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperLeft,
				Spacing = 2, FlexSize = Vector2.right, Margin = new RectOffset(4, 2, 2, 3),
				BackColor = ROW_COLOR
			}.AddChild(top);
			// Unlocked items, in lines of a fixed number of icons
			PPanel line = null;
			int count = 0;
			foreach (var item in tech.unlockedItems) {
				if (item == null || !Game.IsCorrectDlcActiveForCurrentSave(item))
					continue;
				Sprite sprite;
				try {
					sprite = item.UISprite();
				} catch (System.Exception) {
					sprite = null;
				}
				if (sprite == null)
					continue;
				if (count % ICONS_PER_LINE == 0) {
					line = new PPanel("Icons" + (count / ICONS_PER_LINE)) {
						Direction = PanelDirection.Horizontal, Alignment = TextAnchor.
						MiddleLeft, Spacing = 2, FlexSize = Vector2.right,
						Margin = new RectOffset(18, 0, 0, 0)
					};
					rowPanel.AddChild(line);
				}
				line.AddChild(new PLabel("Icon_" + item.Id) {
					Sprite = sprite, SpriteSize = new Vector2(ICON_SIZE, ICON_SIZE),
					MaintainSpriteAspect = true, ToolTip = item.Name
				});
				count++;
			}
			var rowObj = rowPanel.AddTo(rowsParent);
			rowObj.SetMinUISize(new Vector2(0.0f, ROW_MIN_HEIGHT));
			PUIElements.SetAnchors(rowObj, PUIAnchoring.Stretch, PUIAnchoring.Stretch);
			var row = rowObj.AddComponent<QueueRow>();
			row.Init(this, inst, index, numberText);
			return row;
		}

		private void ResetRowColors() {
			for (int i = 0; i < rows.Count; i++)
				rows[i].SetColor(i == 0 ? ROW_ACTIVE_COLOR : ROW_COLOR);
		}

		internal void ZoomTo(Tech tech) {
			if (screen != null && tech != null && !Dragging)
				screen.ZoomToTech(tech.Id);
		}

		internal void ShowPopup(QueueRow row) {
			if (!Dragging && row != null && row.Inst != null)
				popup?.Show(row.Inst, row.transform as RectTransform, transform as
					RectTransform);
		}

		internal void HidePopup(QueueRow row) {
			popup?.Hide(row?.Inst);
		}

		internal void BeginDrag(QueueRow row, PointerEventData data) {
			popup?.Hide();
			try {
				StartDrag(row, data);
			} catch (System.Exception e) {
				// Never leave the panel stuck mid-drag
				PUtil.LogException(e);
				CancelDrag();
			}
		}

		private void CancelDrag() {
			if (dragFrom >= 0 && dragFrom < rows.Count)
				rows[dragFrom].SetDragSource(false);
			dragFrom = -1;
			dragSlot = -1;
			lastDrag = null;
			if (ghost != null) {
				Destroy(ghost.gameObject);
				ghost = null;
			}
			indicator.gameObject.SetActive(false);
			ResetRowColors();
		}

		private void StartDrag(QueueRow row, PointerEventData data) {
			if (ghost != null)
				Destroy(ghost.gameObject);
			dragFrom = row.Index;
			dragSlot = row.Index;
			// Ghost: a copy of the row that ignores layout and raycasts
			var ghostObj = Instantiate(row.gameObject, transform, false);
			var copy = ghostObj.GetComponent<QueueRow>();
			if (copy != null)
				Destroy(copy);
			var le = ghostObj.GetComponent<LayoutElement>() ?? ghostObj.AddComponent<
				LayoutElement>();
			le.ignoreLayout = true;
			// The row already carries a CanvasGroup (QueueRow.Init), so the copy does too
			var cg = ghostObj.AddOrGet<CanvasGroup>();
			cg.alpha = 0.75f;
			cg.blocksRaycasts = false;
			ghost = ghostObj.rectTransform();
			var src = row.transform as RectTransform;
			ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
			ghost.pivot = new Vector2(0.5f, 0.5f);
			ghost.sizeDelta = src.rect.size;
			row.SetDragSource(true);
			Drag(data);
		}

		internal void Drag(PointerEventData data) {
			if (!Dragging)
				return;
			lastDrag = data;
			var rt = transform as RectTransform;
			var cam = data.pressEventCamera;
			if (ghost != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,
					data.position, cam, out Vector2 local))
				ghost.localPosition = new Vector2(0.0f, local.y);
			// Drop slot = number of rows whose centre is above the pointer
			int slot = 0;
			for (int i = 0; i < rows.Count; i++) {
				var rowRT = rows[i].transform as RectTransform;
				var centre = RectTransformUtility.WorldToScreenPoint(cam, rowRT.TransformPoint(
					rowRT.rect.center));
				if (centre.y > data.position.y)
					slot = i + 1;
			}
			dragSlot = slot;
			UpdateIndicator(slot);
			// Tint the rows that would move with the dragged one
			var techs = CurrentTechs();
			var chain = QueueReorder.GetChain(techs, dragFrom, QueueReorder.SlotToIndex(
				dragFrom, slot), PrerequisitesOf);
			for (int i = 0; i < rows.Count; i++)
				rows[i].SetColor(chain.Contains(techs[i]) ? ROW_CHAIN_COLOR : (i == 0 ?
					ROW_ACTIVE_COLOR : ROW_COLOR));
		}

		private void UpdateIndicator(int slot) {
			bool show = slot != dragFrom && slot != dragFrom + 1 && rows.Count > 1;
			indicator.gameObject.SetActive(show);
			if (show) {
				float y;
				var parentRT = rowsParent.transform as RectTransform;
				if (slot < rows.Count) {
					var r = rows[slot].transform as RectTransform;
					y = parentRT.InverseTransformPoint(r.TransformPoint(new Vector2(0.0f, r.
						rect.yMax))).y;
				} else {
					var r = rows[rows.Count - 1].transform as RectTransform;
					y = parentRT.InverseTransformPoint(r.TransformPoint(new Vector2(0.0f, r.
						rect.yMin))).y;
				}
				// Anchored to the top edge of the rows container
				indicator.anchoredPosition = new Vector2(0.0f, y - parentRT.rect.yMax);
			}
		}

		internal void EndDrag(PointerEventData data) {
			if (!Dragging)
				return;
			int from = dragFrom, slot = dragSlot;
			bool inside = RectTransformUtility.RectangleContainsScreenPoint(transform as
				RectTransform, data.position, data.pressEventCamera);
			if (from < rows.Count)
				rows[from].SetDragSource(false);
			dragFrom = -1;
			dragSlot = -1;
			lastDrag = null;
			if (ghost != null) {
				Destroy(ghost.gameObject);
				ghost = null;
			}
			indicator.gameObject.SetActive(false);
			ResetRowColors();
			if (inside && slot >= 0 && !dirty) {
				int to = QueueReorder.SlotToIndex(from, slot);
				if (to != from)
					// Fires ActiveResearchChanged, which rebuilds the rows
					ResearchQueuePatches.ApplyQueueOrder(QueueReorder.Move(CurrentTechs(),
						from, to, PrerequisitesOf));
			}
			if (dirty)
				Refresh();
		}

		private List<Tech> CurrentTechs() {
			var techs = new List<Tech>(rows.Count);
			foreach (var row in rows)
				techs.Add(row.Tech);
			return techs;
		}

		private static IEnumerable<Tech> PrerequisitesOf(Tech tech) {
			return tech.requiredTech;
		}

		/// <summary>
		/// Swallows drags that start on the list background so they do not pan the tree.
		/// </summary>
		private sealed class DragSink : MonoBehaviour, IBeginDragHandler, IDragHandler,
				IEndDragHandler {
			public void OnBeginDrag(PointerEventData eventData) { }

			public void OnDrag(PointerEventData eventData) { }

			public void OnEndDrag(PointerEventData eventData) { }
		}
	}

	/// <summary>
	/// A popup beside the queue list showing one tech's progress per research type. It
	/// takes no raycasts, so it never steals the hover from the row under it.
	/// </summary>
	internal sealed class ProgressPopup {
		private const float BAR_WIDTH = 140.0f, BAR_HEIGHT = 12.0f, GAP = 8.0f;

		private static readonly Color BAR_BACK = new Color(0.12f, 0.12f, 0.16f, 1.0f);

		private readonly Transform parent;

		private GameObject root;

		private TechInstance shown;

		private readonly List<Bar> bars = new List<Bar>();

		internal ProgressPopup(Transform parent) {
			this.parent = parent;
		}

		internal void Destroy() {
			if (root != null)
				Object.Destroy(root);
			root = null;
			shown = null;
			bars.Clear();
		}

		/// <summary>
		/// Hides the popup, or only if it is showing the given tech.
		/// </summary>
		internal void Hide(TechInstance inst = null) {
			if (inst == null || inst == shown)
				Destroy();
		}

		internal void Show(TechInstance inst, RectTransform row, RectTransform list) {
			if (inst == shown && root != null)
				return;
			Destroy();
			var tech = inst.tech;
			var types = Research.Instance?.researchTypes?.Types;
			var panel = new PPanel("ResearchQueueProgress") {
				Direction = PanelDirection.Vertical, Alignment = TextAnchor.UpperLeft,
				Spacing = 4, Margin = new RectOffset(8, 8, 6, 6), DynamicSize = false,
				BackColor = PUITuning.Colors.DialogDarkBackground
			}.AddChild(new PLabel("Title") {
				Text = tech.Name, TextStyle = PUITuning.Fonts.UILightStyle.DeriveStyle(
				style: TMPro.FontStyles.Bold), TextAlignment = TextAnchor.MiddleLeft
			});
			if (types != null)
				foreach (var type in types) {
					if (!tech.costsByResearchTypeID.TryGetValue(type.id, out float cost) ||
							cost <= 0.0f)
						continue;
					var bar = new Bar(type.id, cost);
					bars.Add(bar);
					panel.AddChild(new PPanel("Type_" + type.id) {
						Direction = PanelDirection.Horizontal, Alignment = TextAnchor.
						MiddleLeft, Spacing = 6
					}.AddChild(new PLabel("Icon") {
						Sprite = type.sprite, SpriteSize = new Vector2(20.0f, 20.0f),
						MaintainSpriteAspect = true
					}).AddChild(new PPanel("Bar") {
						BackColor = BAR_BACK, Margin = new RectOffset(0, 0, 0, 0)
					}.AddOnRealize(obj => bar.Realize(obj, type.color))).AddChild(new PLabel(
							"Points") {
						Text = "00000/00000", TextStyle = PUITuning.Fonts.UILightStyle,
						TextAlignment = TextAnchor.MiddleLeft
					}.AddOnRealize(obj => bar.label = obj.GetComponentInChildren<LocText>())));
				}
			panel.AddChild(new PLabel("Hint") {
				Text = ResearchQueueStrings.RowHint, TextStyle = PUITuning.Fonts.UILightStyle.
				DeriveStyle(size: 12), TextAlignment = TextAnchor.MiddleLeft
			});
			root = panel.Build();
			// Above everything, and invisible to raycasts
			var cg = root.AddOrGet<CanvasGroup>();
			cg.blocksRaycasts = false;
			cg.interactable = false;
			foreach (var g in root.GetComponentsInChildren<Graphic>(true))
				g.raycastTarget = false;
			var rt = root.rectTransform();
			rt.SetParent(parent, false);
			rt.SetAsLastSibling();
			var canvas = root.AddOrGet<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = 100;
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.0f, 1.0f);
			// Top left of the popup at the list's right edge, level with the row's top
			var rowCorners = new Vector3[4];
			var listCorners = new Vector3[4];
			row.GetWorldCorners(rowCorners);
			list.GetWorldCorners(listCorners);
			rt.position = new Vector3(listCorners[2].x, rowCorners[1].y, rowCorners[1].z);
			rt.anchoredPosition += new Vector2(GAP, 0.0f);
			// Keep it on screen: rows near the bottom of a long list would push it off
			if (parent is RectTransform parentRT) {
				var popCorners = new Vector3[4];
				var screenCorners = new Vector3[4];
				rt.GetWorldCorners(popCorners);
				parentRT.GetWorldCorners(screenCorners);
				float below = screenCorners[0].y - popCorners[0].y;
				if (below > 0.0f)
					rt.position += new Vector3(0.0f, below, 0.0f);
			}
			shown = inst;
			UpdateProgress();
		}

		internal void UpdateProgress() {
			if (root == null || shown == null)
				return;
			var tech = shown.tech;
			bool complete = tech.IsComplete();
			var points = shown.progressInventory?.PointsByTypeID;
			foreach (var bar in bars) {
				float have = 0.0f;
				if (complete)
					have = bar.cost;
				else if (points != null)
					points.TryGetValue(bar.typeID, out have);
				bar.Set(have);
			}
		}

		private sealed class Bar {
			internal readonly string typeID;

			internal readonly float cost;

			internal LocText label;

			private RectTransform fill;

			internal Bar(string typeID, float cost) {
				this.typeID = typeID;
				this.cost = cost;
			}

			internal void Realize(GameObject obj, Color color) {
				obj.SetMinUISize(new Vector2(BAR_WIDTH, BAR_HEIGHT));
				var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image),
					typeof(LayoutElement));
				fillObj.GetComponent<LayoutElement>().ignoreLayout = true;
				fillObj.GetComponent<Image>().color = color;
				fill = fillObj.rectTransform();
				fill.SetParent(obj.transform, false);
				fill.anchorMin = Vector2.zero;
				fill.anchorMax = new Vector2(0.0f, 1.0f);
				fill.pivot = new Vector2(0.0f, 0.5f);
				fill.offsetMin = Vector2.zero;
				fill.offsetMax = Vector2.zero;
			}

			internal void Set(float have) {
				float frac = cost > 0.0f ? Mathf.Clamp01(have / cost) : 1.0f;
				if (fill != null)
					fill.anchorMax = new Vector2(frac, 1.0f);
				if (label != null)
					label.SetText(string.Format("{0:0}/{1:0}", have, cost));
			}
		}
	}

	/// <summary>
	/// One row of the queue list. Handles drag, click and hover for its tech.
	/// </summary>
	internal sealed class QueueRow : MonoBehaviour, IBeginDragHandler, IDragHandler,
			IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
		internal int Index { get; private set; }

		internal Tech Tech => Inst?.tech;

		internal TechInstance Inst { get; private set; }

		private ResearchQueuePanel panel;

		private LocText numberText;

		private Image background;

		private CanvasGroup canvasGroup;

		internal void Init(ResearchQueuePanel panel, TechInstance inst, int index,
				LocText numberText) {
			this.panel = panel;
			Inst = inst;
			Index = index;
			this.numberText = numberText;
			background = GetComponent<Image>();
			canvasGroup = gameObject.AddOrGet<CanvasGroup>();
			if (numberText != null)
				numberText.SetText((index + 1) + ".");
		}

		internal void SetColor(Color color) {
			if (background != null)
				background.color = color;
		}

		internal void SetDragSource(bool dragging) {
			canvasGroup.alpha = dragging ? 0.35f : 1.0f;
		}

		private void OnDisable() {
			panel?.HidePopup(this);
		}

		public void OnBeginDrag(PointerEventData eventData) {
			if (eventData.button == PointerEventData.InputButton.Left)
				panel.BeginDrag(this, eventData);
		}

		public void OnDrag(PointerEventData eventData) {
			panel.Drag(eventData);
		}

		public void OnEndDrag(PointerEventData eventData) {
			panel.EndDrag(eventData);
		}

		public void OnPointerClick(PointerEventData eventData) {
			if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
				panel.ZoomTo(Tech);
		}

		public void OnPointerEnter(PointerEventData eventData) {
			if (!eventData.dragging)
				panel.ShowPopup(this);
		}

		public void OnPointerExit(PointerEventData eventData) {
			panel.HidePopup(this);
		}
	}
}
