using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ShowRange {
	internal sealed class LightRangeVisualizer : KMonoBehaviour {
		private const string ANIM_NAME = "transferarmgrid_kanim";
		private static readonly HashedString[] PRE_ANIMS = { "grid_pre", "grid_loop" };
		private static readonly HashedString POST_ANIM = "grid_pst";
		private static readonly Color VIS_COLOR = new Color(1f, 1f, 0.4f, 1f);

		private const int MAX_POOL_SIZE = 128;
		private const int SELECTION_CHANGED_EVENT = -1503271301;

		private static int terrainVersion = 0;

		internal static void IncrementTerrainVersion() {
			terrainVersion++;
		}

		internal int range;
		internal LightShape shape = LightShape.Circle;
		internal int width = 4;
		internal DiscreteShadowCaster.Direction direction =
			DiscreteShadowCaster.Direction.South;
		internal Vector2 offset = Vector2.zero;

		// 是否在 OnSpawn 后立即启用（BuildingPreview 为 true；其它为 false）
		internal bool defaultEnabled;

		// 是否已经订阅选中事件（避免重复订阅）
		private bool selectionSubscribed;

		private readonly Dictionary<int, KBatchedAnimController> cells =
			new Dictionary<int, KBatchedAnimController>();

		private readonly HashSet<int> cachedNewCells = new HashSet<int>();
		private readonly List<int> buffer = new List<int>();
		private readonly List<int> toRemove = new List<int>();

		private int lastOrigin = Grid.InvalidCell;
		private int lastTerrainVersion = -1;

		private readonly Stack<KBatchedAnimController> effectPool =
			new Stack<KBatchedAnimController>();

		internal void CopyFrom(Light2D light, Transform root) {
			if (light == null)
				return;
			range = Mathf.CeilToInt(light.Range);
			shape = light.shape;
			width = light.Width;
			direction = light.LightDirection;
			offset = light.Offset;
			lastTerrainVersion = -1;
		}

		// 用 OnSpawn 而不是 instantiateFn 来订阅事件。
		// instantiateFn 触发的时机早于 KMonoBehaviour.InitializeComponent，
		// 此时 KMonoBehaviour.obj 还没赋值，调用 Subscribe 会空引用崩溃。
		// OnSpawn 在 Awake → Start 之后被调用，此时 obj 已就绪，可以安全订阅。
		protected override void OnSpawn() {
			base.OnSpawn();
			if (!selectionSubscribed) {
				selectionSubscribed = true;
				Subscribe(SELECTION_CHANGED_EVENT, OnSelectionChanged);
			}

			// defaultEnabled=true（BuildingPreview）：保持 enabled=true
			// defaultEnabled=false（BuildingComplete / UnderConstruction）：
			//   enabled = KSelectable.IsSelected
			if (defaultEnabled) {
				enabled = true;
			} else {
				var ks = GetComponent<KSelectable>();
				enabled = ks != null && ks.IsSelected;
			}
		}

		private void OnSelectionChanged(object data) {
			var boxed = data as Boxed<bool>;
			if (boxed != null)
				enabled = boxed.value;
		}

		// 双保险：
		// 1. enabled 由 OnSpawn / OnSelectionChanged 精确控制（大开关）
		// 2. Update 里再检查一次是否真的该 active（防止 instantiateFn 到 OnSpawn
		//    之间一帧的错显，那段时间 enabled 还是 Unity 默认的 true）
		private void Update() {
			bool active = defaultEnabled;
			if (!active) {
				var ks = GetComponent<KSelectable>();
				if (ks != null && ks.IsSelected)
					active = true;
			}
			if (!active) {
				if (cells.Count > 0)
					ClearAll();
				return;
			}

			int origin = Grid.PosToCell(transform.GetPosition() + (Vector3)offset);
			if (range <= 0 || !Grid.IsValidCell(origin)) {
				if (cells.Count > 0)
					ClearAll();
				return;
			}

			// 只在 origin 或地形版本变化时重算 GetVisibleCells 并重建 cells 集合。
			if (origin != lastOrigin || terrainVersion != lastTerrainVersion) {
				lastOrigin = origin;
				lastTerrainVersion = terrainVersion;

				buffer.Clear();
				DiscreteShadowCaster.GetVisibleCells(origin, buffer, range, width,
					direction, shape, true);

				cachedNewCells.Clear();
				foreach (int c in buffer)
					if (Grid.IsValidCell(c))
						cachedNewCells.Add(c);

				toRemove.Clear();
				foreach (var kv in cells)
					if (!cachedNewCells.Contains(kv.Key))
						toRemove.Add(kv.Key);
				foreach (int c in toRemove) {
					ReturnEffect(cells[c]);
					cells.Remove(c);
				}

				foreach (int c in cachedNewCells) {
					if (cells.ContainsKey(c))
						continue;
					var ctrl = GetEffect(c);
					if (ctrl == null)
						continue;
					cells[c] = ctrl;
				}
			}
		}

		private KBatchedAnimController GetEffect(int cell) {
			var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront);
			KBatchedAnimController ctrl = null;
			while (effectPool.Count > 0 && ctrl == null)
				ctrl = effectPool.Pop();

			if (ctrl != null) {
				ctrl.transform.SetPosition(pos);
				ctrl.gameObject.SetActive(true);
			} else {
				ctrl = FXHelpers.CreateEffect(ANIM_NAME, pos, null, false,
					Grid.SceneLayer.FXFront, true);
				if (ctrl == null)
					return null;
				ctrl.destroyOnAnimComplete = false;
				ctrl.visibilityType = KAnimControllerBase.VisibilityType.Always;
				ctrl.gameObject.SetActive(true);
			}
			ctrl.Play(PRE_ANIMS, KAnim.PlayMode.Loop);
			ctrl.TintColour = VIS_COLOR;
			return ctrl;
		}

		private void ReturnEffect(KBatchedAnimController ctrl) {
			if (ctrl == null)
				return;
			ctrl.gameObject.SetActive(false);
			if (effectPool.Count < MAX_POOL_SIZE)
				effectPool.Push(ctrl);
			else
				Object.Destroy(ctrl.gameObject);
		}

		private void ClearAll() {
			foreach (var kv in cells)
				ReturnEffect(kv.Value);
			cells.Clear();
			cachedNewCells.Clear();
			lastOrigin = Grid.InvalidCell;
			lastTerrainVersion = -1;
		}

		protected override void OnDisable() {
			base.OnDisable();
			ClearAll();
		}

		protected override void OnCleanUp() {
			if (selectionSubscribed) {
				Unsubscribe(SELECTION_CHANGED_EVENT, OnSelectionChanged);
				selectionSubscribed = false;
			}
			ClearAll();
			while (effectPool.Count > 0) {
				var ctrl = effectPool.Pop();
				if (ctrl != null)
					Object.Destroy(ctrl.gameObject);
			}
			base.OnCleanUp();
		}

		internal static void EnsureOn(GameObject target, BuildingDef def,
				bool defaultEnabled) {
			if (target == null || def == null || def.BuildingComplete == null)
				return;
			var light = def.BuildingComplete.GetComponentInChildren<Light2D>();
			if (light == null)
				return;

			int range = Mathf.CeilToInt(light.Range);
			var shape = light.shape;
			int width = light.Width;
			var direction = light.LightDirection;
			var offset = light.Offset;

			if (target.TryGetComponent(out KPrefabID prefabID)) {
				prefabID.instantiateFn += (obj) => {
					var vis = obj.GetComponent<LightRangeVisualizer>();
					if (vis == null)
						vis = obj.AddComponent<LightRangeVisualizer>();
					vis.range = range;
					vis.shape = shape;
					vis.width = width;
					vis.direction = direction;
					vis.offset = offset;
					vis.defaultEnabled = defaultEnabled;
					// 刻意不在这里设置 enabled：
					// 若立即设 enabled = false，Unity 不会调用 Start → OnSpawn，
					// 订阅就永远不会发生。让 Unity 走完生命周期；Update 里的
					// active 检查会防止 OnSpawn 之前的错显。
				};
			}
		}
	}

	public static class LightRangeVisualizerPatches {
		[HarmonyPatch(typeof(Light2D), "OnSpawn")]
		public static class Light2D_OnSpawn_Patch {
			internal static void Postfix(Light2D __instance) {
				var go = __instance.gameObject;
				if (go == null)
					return;
				var building = go.GetComponentInParent<Building>();
				var root = building != null ? building.gameObject : go;

				var vis = root.GetComponent<LightRangeVisualizer>();
				if (vis == null)
					vis = root.AddComponent<LightRangeVisualizer>();
				// 通过 Light2D.OnSpawn 路径添加的 visualizer 走"选中才显示"模式
				vis.defaultEnabled = false;
				vis.CopyFrom(__instance, root.transform);
			}
		}

		[HarmonyPatch(typeof(GameScenePartitioner), "OnSpawn")]
		public static class GameScenePartitioner_OnSpawn_Patch {
			internal static void Postfix(GameScenePartitioner __instance) {
				if (LightRangeVisualizer_TerrainListenerFlag.registered)
					return;
				LightRangeVisualizer_TerrainListenerFlag.registered = true;
				__instance.AddGlobalLayerListener(__instance.solidChangedLayer,
					OnTerrainChanged);
				__instance.AddGlobalLayerListener(__instance.liquidChangedLayer,
					OnTerrainChanged);
			}

			private static void OnTerrainChanged(int cell, object data) {
				LightRangeVisualizer.IncrementTerrainVersion();
			}
		}

		[HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.
			LoadGeneratedBuildings))]
		public static class GeneratedBuildings_LoadGeneratedBuildings_Patch {
			internal static void Postfix() {
				foreach (var def in Assets.BuildingDefs) {
					if (def == null || def.BuildingComplete == null)
						continue;
					var light = def.BuildingComplete.GetComponentInChildren<Light2D>();
					if (light == null)
						continue;
					if (def.BuildingPreview != null)
						LightRangeVisualizer.EnsureOn(def.BuildingPreview, def, true);
					if (def.BuildingUnderConstruction != null)
						LightRangeVisualizer.EnsureOn(def.BuildingUnderConstruction, def, false);
					LightRangeVisualizer.EnsureOn(def.BuildingComplete, def, false);
				}
			}
		}
	}

	internal static class LightRangeVisualizer_TerrainListenerFlag {
		internal static bool registered;
	}
}