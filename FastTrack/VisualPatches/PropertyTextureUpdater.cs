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
using System.Reflection;
using System.Threading;
using UnityEngine;

using SimProperty = PropertyTextures.Property;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Queues up the expensive PropertyTexture updates on a background thread.
	/// </summary>
	public sealed class PropertyTextureUpdater {
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
			Instance?.DisposeAll();
			Instance = null;
		}

		/// <summary>
		/// Gets the visible range of cells to update property textures.
		/// </summary>
		/// <param name="min">The minimum cell coordinates.</param>
		/// <param name="max">The maximum cell coordinates.</param>
		private static void GetVisibleCellRange(out Vector2I min, out Vector2I max) {
			int width = Grid.WidthInCells, height = Grid.HeightInCells;
			Grid.GetVisibleExtents(out int xMin, out int yMin, out int xMax, out int yMax);
			min = new Vector2I(Mathf.Clamp(xMin - TEXTURE_RESOLUTION, 0, width - 1),
				Mathf.Clamp(yMin - TEXTURE_RESOLUTION, 0, height - 1));
			max = new Vector2I(Mathf.Clamp(xMax + TEXTURE_RESOLUTION, 0, width - 1),
				Mathf.Clamp(yMax + TEXTURE_RESOLUTION, 0, height - 1));
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
			lastWorldID = ClusterManager.INVALID_WORLD_IDX;
			nextPropertyIndex = 0;
			onComplete = new AutoResetEvent(false);
			outstanding = 0;
			running = new List<TextureWorkItemCollection>(64);
			Instance = this;
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
				// Constant-conditions update... but it is pretty fast
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
				Shader.SetGlobalFloat(tIDFogOfWarScale, PropertyTextures.FogOfWarScale);
				constantsDirty = false;
				lastWorldID = id;
			}
		}

		/// <summary>
		/// Disposes of all the running tasks.
		/// </summary>
		internal void DisposeAll() {
			foreach (var task in running)
				task.Dispose();
			running.Clear();
		}

		/// <summary>
		/// Called when one texture finishes updating.
		/// </summary>
		private void FinishOne() {
			if (Interlocked.Decrement(ref outstanding) <= 0)
				onComplete.Set();
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
		}

		/// <summary>
		/// Finishes a texture update by updating the lerpers. They are pretty fast as they
		/// are mostly implemented in Unity.
		/// </summary>
		/// <param name="instance">The property textures to finish updating.</param>
		internal void FinishUpdate(PropertyTextures instance) {
			var lerpers = instance.lerpers;
			int n = lerpers.Length;
			// Wait out the update
			if (outstanding > 0)
				onComplete.WaitOne(Timeout.Infinite);
			DisposeAll();
			if (lerpers != null)
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
				throw new ArgumentException("For property: " + property);
			}
			if (updater == null)
				throw new InvalidOperationException("Missing texture updater: " + property);
			else {
				var task = new TextureWorkItemCollection(this, buffer, min, max, updater);
				running.Add(task);
				AsyncJobManager.Instance.Run(new AsyncJobManager.Work(task, null,
					TextureWorkItemCollection.Finish));
			}
		}

		/// <summary>
		/// Called every frame to update property textures. This version starts in Update()
		/// right after the Sim would have been updated, and finishes up in LateUpdate.
		/// </summary>
		internal void StartUpdate() {
			var inst = PropertyTextures.instance;
			if (Grid.IsInitialized() && !Game.Instance.IsLoading() && inst != null) {
				var buffers = inst.textureBuffers;
				bool timelapse = GameUtil.IsCapturingTimeLapse() || constantsDirty;
				int n = allProperties.Count, update = nextPropertyIndex;
				ConstantParamsUpdate();
				GetVisibleCellRange(out Vector2I min, out Vector2I max);
				// Page through the textures to update, once per frame
				do {
					update = (update + 1) % n;
				} while (allProperties[update].UpdateEveryFrame);
				nextPropertyIndex = update;
				outstanding = 0;
				onComplete.Reset();
				for (int i = 0; i < n; i++) {
					var properties = allProperties[i];
					if ((update == i || properties.UpdateEveryFrame || timelapse) &&
							UpdateProperty(properties.PropertyIndex, buffers, min, max))
						Interlocked.Increment(ref outstanding);
				}
			}
		}

		/// <summary>
		/// Updates a single property texture, possibly asynchronously.
		/// </summary>
		/// <param name="property">The property to update.</param>
		/// <param name="buffers">The texture buffers for locally generated textures.</param>
		/// <param name="min">The minimum cell coordinates to update.</param>
		/// <param name="max">The maximum cell coordinates to update.</param>
		private bool UpdateProperty(SimProperty property, TextureBuffer[] buffers,
				Vector2I min, Vector2I max) {
			bool started = false;
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
				if (p < buffers.Length) {
					StartTextureUpdate(property, buffers[p], min, max);
					started = true;
				}
				break;
			}
			return started;
		}

		/// <summary>
		/// Updates texture data from the Sim. This is not as free as it looks, it costs about
		/// 150 us to load and apply a Sim texture.
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
		private sealed class TextureWorkItemCollection : IWorkItemCollection, IDisposable {
			/// <summary>
			/// When a job completes, unlocks the updating region.
			/// </summary>
			/// <param name="result">The result of the update.</param>
			internal static void Finish(AsyncJobManager.Work result) {
				if (result.Jobs is TextureWorkItemCollection collection)
					collection.Finish();
			}

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

			/// <summary>
			/// Marks this texture update as completed.
			/// </summary>
			private void Finish() {
				parent.FinishOne();
			}

			public void InternalDoWorkItem(int index) {
				int regionMin = index * TEXTURE_RESOLUTION + yMin;
				int regionMax = Math.Min(regionMin + TEXTURE_RESOLUTION - 1, yMax);
				updateTexture.Invoke(region, xMin, regionMin, xMax, regionMax);
			}
		}
	}

	/// <summary>
	/// Applied to Game to start property texture updates.
	/// </summary>
	[HarmonyPatch(typeof(Game), "Update")]
	[HarmonyPriority(Priority.Low)]
	public static class Game_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceTileUpdates;

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix() {
			try {
				PropertyTextureUpdater.Instance?.StartUpdate();
			} catch (Exception e) {
				PUtil.LogError(e);
			}
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
		internal static void Postfix() {
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
