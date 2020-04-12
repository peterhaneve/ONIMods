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

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BrightnessDict = System.Collections.Generic.IDictionary<int, float>;
using IntHandle = HandleVector<int>.Handle;
using LightGridEmitter = LightGridManager.LightGridEmitter;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Manages lighting. Instantiated only by the latest PLib version.
	/// </summary>
	internal sealed class PLightManager {
		/// <summary>
		/// Adds a light's scene change partitioner to the specified scene layer.
		/// </summary>
		private static readonly MethodInfo ADD_TO_LAYER = typeof(Light2D).GetMethodSafe(
			"AddToLayer", false, typeof(Vector2I), typeof(int), typeof(int),
			typeof(ScenePartitionerLayer));

		/// <summary>
		/// Retrieves the origin of a light.
		/// </summary>
		private static readonly PropertyInfo ORIGIN = typeof(Light2D).GetPropertySafe<int>(
			"origin", false);

		/// <summary>
		/// If true, enables the smooth light falloff mode even on vanilla lights.
		/// </summary>
		internal static bool ForceSmoothLight { get; set; } = false;

		/// <summary>
		/// The only instance of PLightManager.
		/// </summary>
		internal static PLightManager Instance { get; private set; }

		/// <summary>
		/// Replaces the scene partitioner method to register lights for tile changes in
		/// their active radius.
		/// </summary>
		/// <param name="instance">The light to register.</param>
		/// <param name="solidPart">The solid partitioner registered.</param>
		/// <param name="liquidPart">The liquid partitioner registered.</param>
		/// <returns>true if registered, or false if not.</returns>
		internal static bool AddScenePartitioner(Light2D instance, ref IntHandle solidPart,
				ref IntHandle liquidPart) {
			bool handled = false;
			var shape = instance.shape;
			int rad = (int)instance.Range;
			// Avoid interfering with vanilla lights
			if (shape != LightShape.Cone && shape != LightShape.Circle && ORIGIN?.GetValue(
					instance, null) is int cell && rad > 0 && Grid.IsValidCell(cell)) {
				var origin = Grid.CellToXY(cell);
				var minCoords = new Vector2I(origin.x - rad, origin.y - rad);
				// Better safe than sorry, check whole possible radius
				var gsp = GameScenePartitioner.Instance;
				solidPart = AddToLayer(instance, minCoords, rad, gsp.solidChangedLayer);
				liquidPart = AddToLayer(instance, minCoords, rad, gsp.liquidChangedLayer);
				handled = true;
			}
			return handled;
		}

		/// <summary>
		/// Adds a light's scene change partitioner to a layer.
		/// </summary>
		/// <param name="instance">The light to add.</param>
		/// <param name="minCoords">The coordinates of the upper left corner.</param>
		/// <param name="rad">The light "radius" (square).</param>
		/// <param name="layer">The layer to add it on.</param>
		/// <returns>A handle to the change partitioner, or InvalidHandle if it could not be
		/// added.</returns>
		private static IntHandle AddToLayer(Light2D instance, Vector2I minCoords, int rad,
				ScenePartitionerLayer layer) {
			var handle = IntHandle.InvalidHandle;
			if (ADD_TO_LAYER?.Invoke(instance, new object[] { minCoords, 2 * rad, 2 * rad,
					layer }) is IntHandle newHandle)
				handle = newHandle;
			return handle;
		}

		/// <summary>
		/// Creates and initializes the lighting manager instance.
		/// </summary>
		/// <returns>true if the lighting manager was initialized and has something to do,
		/// or false otherwise.</returns>
		internal static bool InitInstance() {
			bool patch = false;
			lock (PSharedData.GetLock(PRegistry.KEY_LIGHTING_LOCK)) {
				// Only run if any lights were registered
				var list = PSharedData.GetData<IList<object>>(PRegistry.KEY_LIGHTING_TABLE);
				if (list != null) {
					Instance = new PLightManager();
					Instance.Init(list);
					patch = true;
				}
			}
			// Initialize anyways if smooth lighting is forced on
			if (!patch && ForceSmoothLight) {
				Instance = new PLightManager();
				patch = true;
			}
			return patch;
		}

		/// <summary>
		/// Converts a PLightShape into this mod's namespace.
		/// </summary>
		/// <param name="otherShape">The shape from the shared data.</param>
		/// <returns>An equivalent shape in this mod's namespace.</returns>
		private static PLightShape LightToInstance(object otherShape) {
			var shape = otherShape as PLightShape;
			if (shape == null) {
				var otherType = otherShape.GetType();
				// Retrieve properties via reflection
				var propID = otherType.GetPropertySafe<int>(nameof(PLightShape.ShapeID),
					false);
				var propName = otherType.GetPropertySafe<string>(nameof(PLightShape.
					Identifier), false);
				var fillLight = otherType.CreateDelegate<FillLightFunc>(nameof(PLightShape.
					FillLight), otherShape, typeof(GameObject), typeof(int), typeof(int),
					typeof(BrightnessDict));
				try {
					// Retrieve the ID, handler, and identifier
					if (fillLight == null)
						PUtil.LogWarning("PLightSource handler has invalid method signature!");
					else if (propID.GetValue(otherShape, null) is int id && id > 0) {
						shape = new PLightShape(id, (propName.GetValue(otherShape, null) as
							string) ?? ("LightShape" + id), new CrossModLightWrapper(
							fillLight).CastLight);
					} else
						// Some invalid object got in there somehow
						PUtil.LogWarning("Found light shape {0} with bad ID!".F(otherShape));
				} catch (TargetInvocationException e) {
					PUtil.LogWarning("Exception when retrieving light shape of type " +
						otherType.AssemblyQualifiedName);
					PUtil.LogExcWarn(e);
				}
			}
			return shape;
		}

		// The delegate type covering calls to FillLight from other mods.
		private delegate void FillLightFunc(GameObject source, int cell, int range,
			BrightnessDict brightness);

		/// <summary>
		/// The game object which last requested lighting calculations.
		/// </summary>
		internal GameObject CallingObject { get; set; }

		/// <summary>
		/// The light brightness set by the last lighting brightness request.
		/// </summary>
		private readonly IDictionary<LightGridEmitter, CacheEntry> brightCache;

		/// <summary>
		/// The lighting shapes available, all in this mod's namespace.
		/// </summary>
		private readonly IList<PLightShape> shapes;

		private PLightManager() {
			if (Instance != null)
				PUtil.LogError("Multiple PLightManager created!");
			else
				PUtil.LogDebug("Created PLightManager");
			// Needs to be thread safe! Unfortunately ConcurrentDictionary is unavailable in
			// Unity .NET so we have to use locks
			brightCache = new Dictionary<LightGridEmitter, CacheEntry>(128);
			CallingObject = null;
			Instance = this;
			shapes = new List<PLightShape>(16);
		}

		/// <summary>
		/// Ends a call to lighting update initiated by CreateLight.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		internal void DestroyLight(LightGridEmitter source) {
			if (source != null)
				lock (brightCache) {
					brightCache.Remove(source);
				}
		}

		/// <summary>
		/// Gets the brightness at a given cell for the specified light source.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		/// <param name="location">The location to check.</param>
		/// <param name="state">The lighting state.</param>
		/// <param name="result">The brightness there.</param>
		/// <returns>true if that brightness is valid, or false otherwise.</returns>
		internal bool GetBrightness(LightGridEmitter source, int location,
				LightGridEmitter.State state, out int result) {
			bool valid;
			CacheEntry cacheEntry;
			var shape = state.shape;
			if (shape != LightShape.Cone && shape != LightShape.Circle) {
				lock (brightCache) {
					// Shared access to the cache
					valid = brightCache.TryGetValue(source, out cacheEntry);
				}
				if (valid) {
					valid = cacheEntry.Intensity.TryGetValue(location, out float ratio);
					if (valid)
						result = Mathf.RoundToInt(cacheEntry.BaseLux * ratio);
					else {
#if DEBUG
						PUtil.LogDebug("GetBrightness for invalid cell at {0:D}".F(location));
#endif
						result = 0;
					}
				} else {
#if DEBUG
					PUtil.LogDebug("GetBrightness for invalid emitter at {0:D}".F(location));
#endif
					result = 0;
				}
			} else if (ForceSmoothLight) {
				// Use smooth light even for vanilla Cone and Circle
				result = Mathf.RoundToInt(state.intensity * PLightShape.GetSmoothFalloff(
					state.falloffRate, location, state.origin));
				valid = true;
			} else {
				// Stock
				result = 0;
				valid = false;
			}
			return valid;
		}

		/// <summary>
		/// Handles a lighting system call. Not intended to be used - exists as a fallback.
		/// </summary>
		/// <param name="cell">The origin cell.</param>
		/// <param name="visiblePoints">The location where lit points will be stored.</param>
		/// <param name="range">The light radius.</param>
		/// <param name="shape">The light shape.</param>
		/// <returns>true if the lighting was handled, or false otherwise.</returns>
		internal bool GetVisibleCells(int cell, IList<int> visiblePoints, int range,
				LightShape shape) {
			int index = shape - LightShape.Cone - 1;
			bool handled = false;
			if (index >= 0 && index < shapes.Count) {
				var ps = shapes[index];
				// Do what we can, this only is reachable through methods we have patched
				var lux = DictionaryPool<int, float, PLightManager>.Allocate();
#if DEBUG
				PUtil.LogWarning("Unpatched call to GetVisibleCells; use LightGridEmitter." +
					"UpdateLitCells instead.");
#endif
				ps.FillLight(CallingObject, cell, range, lux);
				// Intensity does not matter
				foreach (var point in lux)
					visiblePoints.Add(point.Key);
				lux.Recycle();
				handled = true;
			}
			return handled;
		}

		/// <summary>
		/// Fills in this light manager from the shared light shape list. Invoked after all
		/// mods have loaded.
		/// </summary>
		/// <param name="lightShapes">The shapes from the shared data.</param>
		private void Init(IList<object> lightShapes) {
			int i = 0;
			foreach (var light in lightShapes)
				// Should only have instances of PLightShape from this mod or other mods
				if (light != null && light.GetType().Name == typeof(PLightShape).Name) {
					var ls = LightToInstance(light);
					if (ls != null) {
						// Verify that the light goes into the right slot
						int sid = ls.ShapeID;
						if (sid != ++i)
							PUtil.LogWarning("Light shape {0} has bad ID {1:D}!".F(ls, sid));
						shapes.Add(ls);
					}
				} else
					// Moe must clean it!
					PUtil.LogError("Foreign contaminant in PLightManager: " + (light == null ?
						"null" : light.GetType().FullName));
		}

		/// <summary>
		/// Creates the preview for a given light.
		/// 
		/// RadiationGridManager.CreatePreview has no references so no sense in patching that
		/// yet.
		/// </summary>
		/// <param name="origin">The starting cell.</param>
		/// <param name="radius">The light radius.</param>
		/// <param name="shape">The light shape.</param>
		/// <param name="lux">The base brightness in lux.</param>
		/// <returns>true if the lighting was handled, or false otherwise.</returns>
		internal bool PreviewLight(int origin, float radius, LightShape shape, int lux) {
			bool handled = false;
			if (shape != LightShape.Circle && shape != LightShape.Cone) {
				var cells = DictionaryPool<int, float, PLightManager>.Allocate();
				// Replicate the logic of the original one...
				int index = shape - LightShape.Cone - 1;
				if (index < shapes.Count) {
					// Found handler!
					shapes[index].FillLight(CallingObject, origin, (int)radius, cells);
					foreach (var pair in cells) {
						int cell = pair.Key;
						if (Grid.IsValidCell(cell)) {
							// Allow any fraction, not just linear falloff
							int lightValue = Mathf.RoundToInt(lux * pair.Value);
							LightGridManager.previewLightCells.Add(new Tuple<int, int>(cell,
								lightValue));
							LightGridManager.previewLux[cell] = lightValue;
						}
					}
					CallingObject = null;
					handled = true;
				}
				cells.Recycle();
			}
			return handled;
		}

		/// <summary>
		/// Updates the lit cells list.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		/// <param name="state">The light emitter state.</param>
		/// <param name="litCells">The location where lit cells will be placed.</param>
		/// <returns>true if the lighting was handled, or false otherwise.</returns>
		internal bool UpdateLitCells(LightGridEmitter source, LightGridEmitter.State state,
				IList<int> litCells) {
			bool handled = false;
			int index;
			if (source == null)
				throw new ArgumentNullException("source");
			if ((index = state.shape - LightShape.Cone - 1) >= 0 && index < shapes.Count &&
					litCells != null) {
				var ps = shapes[index];
				CacheEntry cacheEntry;
				lock (brightCache) {
					// Look up in cache, in a thread safe way
					if (!brightCache.TryGetValue(source, out cacheEntry)) {
						cacheEntry = new CacheEntry(CallingObject, state.intensity);
						brightCache.Add(source, cacheEntry);
					}
				}
				var brightness = cacheEntry.Intensity;
				// Proper owner found
				brightness.Clear();
				ps.FillLight(cacheEntry.Owner, state.origin, (int)state.radius, brightness);
				foreach (var point in cacheEntry.Intensity)
					litCells.Add(point.Key);
				handled = true;
			}
			return handled;
		}

		/// <summary>
		/// A cache entry in the light brightness cache.
		/// </summary>
		private sealed class CacheEntry {
			/// <summary>
			/// The base intensity in lux.
			/// </summary>
			internal int BaseLux { get; }

			/// <summary>
			/// The relative brightness per cell.
			/// </summary>
			internal BrightnessDict Intensity { get; }

			/// <summary>
			/// The owner which initiated the lighting call.
			/// </summary>
			internal GameObject Owner { get; }

			internal CacheEntry(GameObject owner, int baseLux) {
				BaseLux = baseLux;
				// Do not use the pool because these might last a long time and be numerous
				Intensity = new Dictionary<int, float>(64);
				Owner = owner;
			}

			public override string ToString() {
				return "Lighting Cache Entry for " + Owner?.name;
			}
		}

		/// <summary>
		/// Wraps a lighting system call from another mod's namespace.
		/// </summary>
		private sealed class CrossModLightWrapper {
			/// <summary>
			/// The method to call when lighting system handling is requested.
			/// </summary>
			private readonly FillLightFunc other;

			internal CrossModLightWrapper(FillLightFunc other) {
				if (other == null)
					throw new ArgumentNullException("other");
				this.other = other;
			}

			/// <summary>
			/// Handles lighting from another mod.
			/// </summary>
			/// <param name="source">The source game object.</param>
			/// <param name="args">The lighting arguments.</param>
			internal void CastLight(GameObject source, LightingArgs args) {
				other?.Invoke(source, args.SourceCell, args.Range, args.Brightness);
			}
		}
	}
}
