/*
 * Copyright 2022 Peter Han
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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using SimProperty = PropertyTextures.Property;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Queues up the expensive PropertyTexture updates on a background thread.
	/// </summary>
	public sealed class PropertyTextureUpdater : IDisposable {
		/// <summary>
		/// A delegate which can update property textures. Uses reverse patches pulled from
		/// the original PropertyTextures class.
		/// </summary>
		/// <param name="texture_region">The region to update.</param>
		/// <param name="x0">The bottom left x coordinate.</param>
		/// <param name="y0">The bottom left y coordinate.</param>
		/// <param name="x1">The top right x coordinate.</param>
		/// <param name="y1">The top right y coordinate.</param>
		private delegate void UpdateTexture(TextureRegion texture_region, int x0, int y0,
			int x1, int y1);

		/// <summary>
		/// Stores the singleton instance of this class.
		/// </summary>
		internal static PropertyTextureUpdater Instance { get; private set; }

		/// <summary>
		/// The property types to turn off every-frame rendering.
		/// </summary>
		private static readonly IList<SimProperty> REDUCE_PROPERTIES = new List<SimProperty> {
			SimProperty.SolidDigAmount, SimProperty.SolidLiquidGasMass
		};

		/// <summary>
		/// The block size used for updates.
		/// </summary>
		private const int TEXTURE_RESOLUTION = 16;

		/// <summary>
		/// Updaters for each of the static texture update methods in PropertyTextures.
		/// </summary>
		private static readonly UpdateTexture UPDATE_DANGER = UpdaterFor("UpdateDanger");

		private static readonly UpdateTexture UPDATE_FALLING_SAND =
			UpdaterFor("UpdateFallingSolidChange");

		private static readonly UpdateTexture UPDATE_FOG_OF_WAR = UpdaterFor("UpdateFogOfWar");

		private static readonly UpdateTexture UPDATE_GAS = UpdaterFor("UpdateGasColour");

		private static readonly UpdateTexture UPDATE_DIGGING =
			UpdaterFor("UpdateSolidDigAmount");

		private static readonly UpdateTexture UPDATE_LIGHT = UpdaterFor("UpdateWorldLight");

		private static readonly UpdateTexture UPDATE_MASS =
			UpdaterFor("UpdateSolidLiquidGasMass");

		private static readonly UpdateTexture UPDATE_PRESSURE = UpdaterFor("UpdatePressure");

		private static readonly UpdateTexture UPDATE_RADIATION = UpdaterFor("UpdateRadiation");

		private static readonly UpdateTexture UPDATE_STATE_CHANGE =
			UpdaterFor("UpdateStateChange");

		private static readonly UpdateTexture UPDATE_TEMPERATURE =
			UpdaterFor("UpdateTemperature");

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void CreateInstance() {
			if (Instance == null)
				Instance = new PropertyTextureUpdater();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// Creates a texture updater delegate for a method in PropertyTextures.
		/// </summary>
		/// <param name="method">The method name to delegate.</param>
		/// <returns>A static delegate to call that method quickly.</returns>
		private static UpdateTexture UpdaterFor(string method) {
			return typeof(PropertyTextures).CreateStaticDelegate<UpdateTexture>(method,
				typeof(TextureRegion), typeof(int), typeof(int), typeof(int), typeof(int));
		}

		/// <summary>
		/// A type punned list of the texture properties used by PropertyTextures.
		/// </summary>
		private readonly IList<TextureProperties> allProperties;

		/// <summary>
		/// True if the "constant" parameters need to be updated (asteroid specific).
		/// </summary>
		private bool constantsDirty;

		/// <summary>
		/// The external textures from PropertyTextures.
		/// </summary>
		private Texture2D[] external;

		/// <summary>
		/// Forces a start update in the LateUpdate pass for one frame only. Used to stop a
		/// black screen on initialize.
		/// </summary>
		private bool forceLateUpdate;

		/// <summary>
		/// The last coordinates of the upper right visible cell.
		/// </summary>
		private Vector2I lastViewMax;

		/// <summary>
		/// The last coordinates of the bottom left visible cell.
		/// </summary>
		private Vector2I lastViewMin;

		/// <summary>
		/// The ID of the last active world.
		/// </summary>
		private int lastWorldID;

		/// <summary>
		/// The next property index to update for those not run every frame.
		/// </summary>
		private int nextPropertyIndex;

		/// <summary>
		/// Triggered when all texture updates complete.
		/// </summary>
		private readonly EventWaitHandle onComplete;

		/// <summary>
		/// How many property texture updates are still outstanding.
		/// </summary>
		private volatile int outstanding;

		/// <summary>
		/// Stores a list of texture work items to clean up on the main thread when done.
		/// </summary>
		private readonly IList<TextureWorkItemCollection> running;

		/// <summary>
		/// References IDs for updating shader texture properties.
		/// </summary>
		private readonly int tIDClusterWorldSize;
		private readonly int tIDFogOfWarScale;
		private readonly int tIDTopBorderHeight;
		private readonly int tIDWorldSize;

		private PropertyTextureUpdater() {
			var tIDPropTexWsToCs = Shader.PropertyToID("_PropTexWsToCs");
			var tIDPropTexCsToWs = Shader.PropertyToID("_PropTexCsToWs");
			tIDClusterWorldSize = Shader.PropertyToID("_ClusterWorldSizeInfo");
			tIDFogOfWarScale = Shader.PropertyToID("_FogOfWarScale");
			tIDTopBorderHeight = Shader.PropertyToID("_TopBorderHeight");
			tIDWorldSize = Shader.PropertyToID("_WorldSizeInfo");
			Shader.SetGlobalVector(tIDPropTexWsToCs, new Vector4(0f, 0f, 1f, 1f));
			Shader.SetGlobalVector(tIDPropTexCsToWs, new Vector4(0f, 0f, 1f, 1f));
			allProperties = new List<TextureProperties>(16);
			constantsDirty = true;
			external = null;
			forceLateUpdate = true;
			lastViewMax = new Vector2I(Grid.InvalidCell, Grid.InvalidCell);
			lastViewMin = new Vector2I(Grid.InvalidCell, Grid.InvalidCell);
			lastWorldID = ClusterManager.INVALID_WORLD_IDX;
			nextPropertyIndex = 0;
			onComplete = new AutoResetEvent(false);
			outstanding = 0;
			running = new List<TextureWorkItemCollection>(64);
		}

		/// <summary>
		/// Updates the shader parameters that do not change often.
		/// </summary>
		private void ConstantParamsUpdate() {
			var inst = ClusterManager.Instance;
			int id = inst.activeWorldId;
			if (constantsDirty || id != lastWorldID) {
				float w = Grid.WidthInCells, h = Grid.HeightInCells;
				var activeWorld = inst.GetWorld(id);
				Vector4 clusterSize;
				var worldOffset = activeWorld.WorldOffset;
				var worldSize = activeWorld.WorldSize;
				Shader.SetGlobalVector(tIDWorldSize, new Vector4(w, h, 1.0f / w, 1.0f / h));
				if (DlcManager.IsPureVanilla() || (CameraController.Instance != null &&
						CameraController.Instance.ignoreClusterFX))
					clusterSize = new Vector4(w, h, 0f, 0f);
				else
					clusterSize = new Vector4(worldSize.x, worldSize.y, 1.0f / (worldSize.x +
						worldOffset.x), 1.0f / (worldSize.y + worldOffset.y));
				Shader.SetGlobalVector(tIDClusterWorldSize, clusterSize);
				Shader.SetGlobalFloat(tIDTopBorderHeight, activeWorld.FullyEnclosedBorder ?
					0f : Grid.TopBorderHeight);
				constantsDirty = false;
				lastWorldID = id;
			}
			// This one could be updated even if constants are the same
			Shader.SetGlobalFloat(tIDFogOfWarScale, PropertyTextures.FogOfWarScale);
		}

		public void Dispose() {
			DisposeAll();
			onComplete.Dispose();
		}

		/// <summary>
		/// Disposes of all the running tasks.
		/// </summary>
		private void DisposeAll() {
			foreach (var task in running)
				task.Dispose();
			running.Clear();
			outstanding = 0;
		}

		/// <summary>
		/// Called when one texture finishes updating.
		/// </summary>
		private void FinishOne() {
			if (Interlocked.Decrement(ref outstanding) <= 0)
				onComplete.Set();
		}

		/// <summary>
		/// Gets the visible range of cells to update property textures.
		/// </summary>
		/// <param name="min">The minimum cell coordinates.</param>
		/// <param name="max">The maximum cell coordinates.</param>
		/// <returns>true if the viewport changed since the last call, or false otherwise.</returns>
		private bool GetVisibleCellRange(out Vector2I min, out Vector2I max) {
			int width = Grid.WidthInCells, height = Grid.HeightInCells;
			bool changed;
			Grid.GetVisibleExtents(out int xMin, out int yMin, out int xMax, out int yMax);
			min = new Vector2I(Mathf.Clamp(xMin - TEXTURE_RESOLUTION, 0, width - 1),
				Mathf.Clamp(yMin - TEXTURE_RESOLUTION, 0, height - 1));
			max = new Vector2I(Mathf.Clamp(xMax + TEXTURE_RESOLUTION, 0, width - 1),
				Mathf.Clamp(yMax + TEXTURE_RESOLUTION, 0, height - 1));
			changed = xMin != lastViewMin.x || xMax != lastViewMax.x || yMin != lastViewMin.
				y || yMax != lastViewMax.y;
			lastViewMin.x = xMin; lastViewMin.y = yMin;
			lastViewMax.x = xMax; lastViewMax.y = yMax;
			return changed;
		}

		/// <summary>
		/// Initializes the external arrays after a PropertyTexture reset.
		/// </summary>
		/// <param name="allTextureProperties">The texture properties to use.</param>
		/// <param name="externalTextures">The external textures from Sim.</param>
		internal void Init(IList<KTextureProperties> allTextureProperties,
				Texture2D[] externalTextures) {
			external = externalTextures;
			allProperties.Clear();
			if (allTextureProperties != null) {
				int n = allTextureProperties.Count;
				// Copy to the allProperties array, as the Klei version will not be updated
				// again until the next reset (if any)
				for (int i = 0; i < n; i++) {
					var property = allTextureProperties[i];
					if (REDUCE_PROPERTIES.Contains(property.simProperty))
						property.updateEveryFrame = false;
					allProperties.Add(new TextureProperties(ref property));
				}
			}
			constantsDirty = true;
			lastWorldID = ClusterManager.INVALID_WORLD_IDX;
		}

		/// <summary>
		/// Finishes a texture update by updating the lerpers. They are pretty fast as they
		/// are mostly implemented in Unity.
		/// </summary>
		/// <param name="instance">The property textures to finish updating.</param>
		internal void FinishUpdate(PropertyTextures instance) {
			var lerpers = instance.lerpers;
			if (forceLateUpdate && outstanding < 1) {
				StartUpdate();
				forceLateUpdate = false;
			}
			// Wait out the update
			if (outstanding > 0)
				onComplete.WaitOne(FastTrackMod.MAX_TIMEOUT);
			DisposeAll();
			if (lerpers != null && !FullScreenDialogPatches.DialogVisible) {
				int n = lerpers.Length;
				for (int i = 0; i < n; i++) {
					var lerper = lerpers[i];
					if (lerper != null) {
						if (Time.timeScale == 0f)
							// Handle paused case
							lerper.LongUpdate(Time.unscaledDeltaTime);
						Shader.SetGlobalTexture(allProperties[i].PropertyName, lerper.
							Update());
					}
				}
			}
		}

		/// <summary>
		/// Starts a multithreaded texture update. Uses the asynchronous job manager.
		/// </summary>
		/// <param name="property">The property to update.</param>
		/// <param name="buffer">The texture where the updates will be stored.</param>
		/// <param name="min">The minimum cell coordinates to update.</param>
		/// <param name="max">The maximum cell coordinates to update.</param>
		private void StartTextureUpdate(SimProperty property, TextureBuffer buffer,
				Vector2I min, Vector2I max) {
			UpdateTexture updater;
			switch (property) {
			case SimProperty.StateChange:
				updater = UPDATE_STATE_CHANGE;
				break;
			case SimProperty.GasPressure:
				updater = UPDATE_PRESSURE;
				break;
			case SimProperty.GasColour:
				updater = UPDATE_GAS;
				break;
			case SimProperty.GasDanger:
				updater = UPDATE_DANGER;
				break;
			case SimProperty.FogOfWar:
				updater = UPDATE_FOG_OF_WAR;
				break;
			case SimProperty.SolidDigAmount:
				updater = UPDATE_DIGGING;
				break;
			case SimProperty.SolidLiquidGasMass:
				updater = UPDATE_MASS;
				break;
			case SimProperty.WorldLight:
				updater = UPDATE_LIGHT;
				break;
			case SimProperty.Temperature:
				updater = UPDATE_TEMPERATURE;
				break;
			case SimProperty.FallingSolid:
				updater = UPDATE_FALLING_SAND;
				break;
			case SimProperty.Radiation:
				updater = UPDATE_RADIATION;
				break;
			default:
				throw new ArgumentException("No updater for property: " + property);
			}
			if (updater == null)
				throw new InvalidOperationException("Missing texture updater: " + property);
			else
				running.Add(new TextureWorkItemCollection(this, buffer, min, max, updater));
		}

		/// <summary>
		/// Called every frame to update property textures. This version starts in Update()
		/// right after the Sim would have been updated, and finishes up in LateUpdate.
		/// </summary>
		internal void StartUpdate() {
			var inst = PropertyTextures.instance;
			if (Grid.IsInitialized() && !Game.Instance.IsLoading() && inst != null &&
					(!FullScreenDialogPatches.DialogVisible || forceLateUpdate)) {
				var buffers = inst.textureBuffers;
				bool timelapse = GameUtil.IsCapturingTimeLapse() || constantsDirty;
				int n = allProperties.Count, update = nextPropertyIndex;
				ConstantParamsUpdate();
				bool redrawFlashy = GetVisibleCellRange(out Vector2I min, out Vector2I max);
				// Page through the textures to update, once per frame
				do {
					update = (update + 1) % n;
				} while (allProperties[update].UpdateEveryFrame);
				nextPropertyIndex = update;
				running.Clear();
				for (int i = 0; i < n; i++) {
					var properties = allProperties[i];
					if (update == i || properties.UpdateEveryFrame || timelapse ||
							(redrawFlashy && i == (int)SimProperty.SolidDigAmount))
						UpdateProperty(properties.PropertyIndex, buffers, min, max);
				}
				// Start them all at once
				onComplete.Reset();
				outstanding = running.Count;
				foreach (var task in running)
					AsyncJobManager.Instance.Run(task);
			} else
				outstanding = 0;
		}

		/// <summary>
		/// Updates a single property texture, possibly asynchronously.
		/// </summary>
		/// <param name="property">The property to update.</param>
		/// <param name="buffers">The texture buffers for locally generated textures.</param>
		/// <param name="min">The minimum cell coordinates to update.</param>
		/// <param name="max">The maximum cell coordinates to update.</param>
		private void UpdateProperty(SimProperty property, TextureBuffer[] buffers,
				Vector2I min, Vector2I max) {
			int cells = Grid.CellCount, p = (int)property;
			switch (property) {
			case SimProperty.Flow:
				UpdateSimProperty(p, PropertyTextures.externalFlowTex, 8 * cells);
				break;
			case SimProperty.Liquid:
				UpdateSimProperty(p, PropertyTextures.externalLiquidTex, 4 * cells);
				break;
			case SimProperty.ExposedToSunlight:
				UpdateSimProperty(p, PropertyTextures.externalExposedToSunlight, cells);
				break;
			default:
				if (p < buffers.Length)
					StartTextureUpdate(property, buffers[p], min, max);
				break;
			}
		}

		/// <summary>
		/// Updates texture data from the Sim.
		/// </summary>
		/// <param name="index">The property texture index to update.</param>
		/// <param name="simTexture">The texture data from the Sim.</param>
		/// <param name="size">The number of bytes to load.</param>
		private void UpdateSimProperty(int index, IntPtr simTexture, int size) {
			if (index < external.Length) {
				var texture = external[index];
				texture.LoadRawTextureData(simTexture, size);
				texture.Apply();
			}
		}

		/// <summary>
		/// Generates work items for updating a texture.
		/// </summary>
		private sealed class TextureWorkItemCollection : AsyncJobManager.IWork, IDisposable,
				IWorkItemCollection {
			/// <summary>
			/// The parent to notify when it finishes.
			/// </summary>
			private readonly PropertyTextureUpdater parent;

			/// <summary>
			/// The texture region to update.
			/// </summary>
			private readonly TextureRegion region;

			/// <summary>
			/// The callback to update a texture region.
			/// </summary>
			private readonly UpdateTexture updateTexture;

			/// <summary>
			/// The upper right X coordinate.
			/// </summary>
			private readonly int xMax;

			/// <summary>
			/// The lower left X coordinate.
			/// </summary>
			private readonly int xMin;

			/// <summary>
			/// The upper right Y coordinate.
			/// </summary>
			private readonly int yMax;

			/// <summary>
			/// The lower left Y coordinate.
			/// </summary>
			private readonly int yMin;

			/// <summary>
			/// The number of sub-jobs in this work request.
			/// </summary>
			public int Count { get; private set; }

			public IWorkItemCollection Jobs => this;

			public TextureWorkItemCollection(PropertyTextureUpdater instance,
					TextureBuffer buffer, Vector2I min, Vector2I max, UpdateTexture updater) {
				parent = instance ?? throw new ArgumentNullException(nameof(instance));
				xMin = min.x;
				xMax = max.x;
				yMin = min.y;
				yMax = max.y;
				if (xMax < xMin || yMax < yMin)
					throw new ArgumentOutOfRangeException(nameof(max));
				if (buffer == null)
					throw new ArgumentNullException(nameof(buffer));
				region = buffer.Lock(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
				Count = 1 + (yMax - yMin) / TEXTURE_RESOLUTION;
				updateTexture = updater ?? throw new ArgumentNullException(nameof(updater));
			}

			public void Dispose() {
				region.Unlock();
			}

			public void InternalDoWorkItem(int index) {
				int regionMin = index * TEXTURE_RESOLUTION + yMin;
				int regionMax = Math.Min(regionMin + TEXTURE_RESOLUTION - 1, yMax);
				updateTexture.Invoke(region, xMin, regionMin, xMax, regionMax);
			}

			public void TriggerAbort() {
				// Sadly unlocking probably will throw as it happens on a background thread
				parent.FinishOne();
			}

			public void TriggerComplete() {
				parent.FinishOne();
			}

			public void TriggerStart() { }
		}
	}

	/// <summary>
	/// Applied to PropertyTextures to replace LateUpdate with the finishing touches of
	/// PropertyTextureUpdater.
	/// </summary>
	[HarmonyPatch(typeof(PropertyTextures), "LateUpdate")]
	public static class PropertyTextures_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceTileUpdates;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static bool Prefix(PropertyTextures __instance) {
			var inst = PropertyTextureUpdater.Instance;
			try {
				if (inst != null)
					inst.FinishUpdate(__instance);
			} catch (Exception e) {
				PUtil.LogError(e);
			}
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to PropertyTextures.
	/// </summary>
	[HarmonyPatch(typeof(PropertyTextures), "OnReset")]
	public static class PropertyTextures_OnReset_Patch {
		/// <summary>
		/// Applied after OnReset runs.
		/// </summary>
		internal static void Postfix(Texture2D[] ___externallyUpdatedTextures,
				IList<KTextureProperties> ___allTextureProperties) {
			PropertyTextureUpdater.Instance?.Init(___allTextureProperties,
				___externallyUpdatedTextures);
		}
	}

	/// <summary>
	/// Applied to PropertyTextures to initialize our instance with its field values.
	/// </summary>
	[HarmonyPatch(typeof(PropertyTextures), "OnSpawn")]
	public static class PropertyTextures_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceTileUpdates;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(PropertyTextures __instance) {
			PropertyTextureUpdater.CreateInstance();
		}
	}

	/// <summary>
	/// A cleaner and safer version of KTextureProperties to be used locally.
	/// </summary>
	internal sealed class TextureProperties {
		/// <summary>
		/// The texture property name.
		/// </summary>
		public string PropertyName { get; }

		/// <summary>
		/// The property rendered by this texture.
		/// </summary>
		public SimProperty PropertyIndex { get; }

		/// <summary>
		/// true if the texture should be updated every frame.
		/// </summary>
		public bool UpdateEveryFrame { get; }

		/// <summary>
		/// true if the texture is updated by the Sim.
		/// </summary>
		public bool UpdateExternally { get; }

		public TextureProperties(ref KTextureProperties kProps) {
			PropertyName = kProps.texturePropertyName;
			PropertyIndex = kProps.simProperty;
			UpdateEveryFrame = kProps.updateEveryFrame;
			UpdateExternally = kProps.updatedExternally;
		}

		public override string ToString() {
			return "TextureProperties[PropertyName={0}]".F(PropertyName);
		}
	}

	/// <summary>
	/// This struct's layout matches the private struct with this name in PropertyTextures.
	/// Harmony allows a substitution in ___ arguments and it works!
	/// </summary>
#pragma warning disable CS0649
	internal struct KTextureProperties {
		public string name;

		public SimProperty simProperty;

		public TextureFormat textureFormat;

		public FilterMode filterMode;

		public bool updateEveryFrame;

		public bool updatedExternally;

		public bool blend;

		public float blendSpeed;

		public string texturePropertyName;
	}
#pragma warning restore CS0649
}
