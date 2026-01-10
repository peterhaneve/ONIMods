/*
 * Copyright 2026 Peter Han
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using BrightnessDict = System.Collections.Generic.IDictionary<int, float>;
using LightGridEmitter = LightGridManager.LightGridEmitter;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Manages lighting. Instantiated only by the latest PLib version.
	/// </summary>
	public sealed class PLightManager : PForwardedComponent {
		/// <summary>
		/// Implemented by classes which want to handle lighting calls.
		/// </summary>
		/// <param name="args">The parameters to use for lighting, and the location to
		/// store results. See the LightingArgs class documentation for details.</param>
		public delegate void CastLightDelegate(LightingArgs args);

		/// <summary>
		/// A singleton empty list instance for default values.
		/// </summary>
		private static readonly List<object> EMPTY_SHAPES = new List<object>(1);

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// If true, enables the smooth light falloff mode even on vanilla lights.
		/// </summary>
		internal static bool ForceSmoothLight { get; set; }

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PLightManager Instance { get; private set; }

		/// <summary>
		/// Calculates the brightness falloff as it would be in the stock game.
		/// </summary>
		/// <param name="falloffRate">The falloff rate to use.</param>
		/// <param name="cell">The cell where falloff is being computed.</param>
		/// <param name="origin">The light origin cell.</param>
		/// <returns>The brightness at that location from 0 to 1.</returns>
		public static float GetDefaultFalloff(float falloffRate, int cell, int origin) {
			return 1.0f / Math.Max(1.0f, Mathf.RoundToInt(falloffRate * Math.Max(Grid.
				GetCellDistance(origin, cell), 1)));
		}

		/// <summary>
		/// Calculates the brightness falloff similar to the default falloff, but far smoother.
		/// Slightly heavier on computation however.
		/// </summary>
		/// <param name="falloffRate">The falloff rate to use.</param>
		/// <param name="cell">The cell where falloff is being computed.</param>
		/// <param name="origin">The light origin cell.</param>
		/// <returns>The brightness at that location from 0 to 1.</returns>
		public static float GetSmoothFalloff(float falloffRate, int cell, int origin) {
			Vector2I newCell = Grid.CellToXY(cell), start = Grid.CellToXY(origin);
			return 1.0f / Math.Max(1.0f, falloffRate * PUtil.Distance(start.X, start.Y,
				newCell.X, newCell.Y));
		}

		/// <summary>
		/// Gets the raycasting shape to use for the given light.
		/// </summary>
		/// <param name="light">The light which is being drawn.</param>
		/// <returns>The shape to use for its rays.</returns>
		internal static LightShape LightShapeToRayShape(Light2D light) {
			var shape = light.shape;
			if (shape != LightShape.Cone && shape != LightShape.Circle)
				shape = Instance.GetRayShape(shape);
			return shape;
		}

		/// <summary>
		/// Logs a message encountered by the PLib lighting system.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogLightingDebug(string message) {
			Debug.LogFormat("[PLibLighting] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered by the PLib lighting system.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogLightingWarning(string message) {
			Debug.LogWarningFormat("[PLibLighting] {0}", message);
		}

		public override Version Version => VERSION;

		/// <summary>
		/// The light brightness set by the last lighting brightness request.
		/// </summary>
		private readonly ConcurrentDictionary<LightGridEmitter, CacheEntry> brightCache;

		/// <summary>
		/// The last object that requested a preview. Only one preview can be requested at a
		/// time, so no need for thread safety.
		/// </summary>
		internal GameObject PreviewObject { get; set; }

		/// <summary>
		/// The lighting shapes available, all in this mod's namespace.
		/// </summary>
		private readonly IList<ILightShape> shapes;

		/// <summary>
		/// Creates a lighting manager to register PLib lighting.
		/// </summary>
		public PLightManager() {
			// Needs to be thread safe!
			brightCache = new ConcurrentDictionary<LightGridEmitter, CacheEntry>(2, 128);
			PreviewObject = null;
			shapes = new List<ILightShape>(16);
		}

		/// <summary>
		/// Adds a light to the lookup table.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		/// <param name="owner">The light's owning game object.</param>
		internal void AddLight(LightGridEmitter source, GameObject owner) {
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			// The default equality comparer will be used; since each Light2D is supposed
			// to have exactly one LightGridEmitter, this should be fine
			brightCache.TryAdd(source, new CacheEntry(owner));
		}

		public override void Bootstrap(Harmony plibInstance) {
			SetSharedData(new List<object>(16));
		}

		/// <summary>
		/// Ends a call to lighting update initiated by CreateLight.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		internal void DestroyLight(LightGridEmitter source) {
			if (source != null)
				brightCache.TryRemove(source, out _);
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
			var shape = state.shape;
			if (shape != LightShape.Cone && shape != LightShape.Circle) {
				valid = brightCache.TryGetValue(source, out CacheEntry cacheEntry);
				if (valid) {
					valid = cacheEntry.Intensity.TryGetValue(location, out float ratio);
					if (valid)
						result = Mathf.RoundToInt(cacheEntry.BaseLux * ratio);
					else {
#if DEBUG
						LogLightingDebug("GetBrightness for invalid cell at {0:D}".F(location));
#endif
						result = 0;
					}
				} else {
#if DEBUG
					LogLightingDebug("GetBrightness for invalid emitter at {0:D}".F(location));
#endif
					result = 0;
				}
			} else if (ForceSmoothLight) {
				// Use smooth light even for vanilla Cone and Circle
				result = Mathf.RoundToInt(state.intensity * GetSmoothFalloff(state.falloffRate,
					location, state.origin));
				valid = true;
			} else {
				// Stock
				result = 0;
				valid = false;
			}
			return valid;
		}

		/// <summary>
		/// Checks to see if a light has specified one of the built-in ray options to cast
		/// the little yellow rays around it.
		/// </summary>
		/// <param name="shape">The light shape to check.</param>
		/// <returns>The light shape to use for ray casting, or the original shape if it is
		/// a stock shape or a light shape not known to PLib Lighting.</returns>
		internal LightShape GetRayShape(LightShape shape) {
			int index = shape - LightShape.Cone - 1;
			ILightShape ps;
			if (index >= 0 && index < shapes.Count && (ps = shapes[index]) != null) {
				var newShape = ps.RayMode;
				if (newShape >= LightShape.Circle)
					shape = newShape;
			}
			return shape;
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			shapes.Clear();
			foreach (var light in GetSharedData(EMPTY_SHAPES)) {
				var ls = PRemoteLightWrapper.LightToInstance(light);
				shapes.Add(ls);
				if (ls == null)
					// Moe must clean it!
					LogLightingWarning("Foreign contaminant in PLightManager!");
			}

			LightingPatches.ApplyPatches(plibInstance);
		}

		/// <summary>
		/// Creates the preview for a given light.
		/// </summary>
		/// <param name="origin">The starting cell.</param>
		/// <param name="radius">The light radius.</param>
		/// <param name="shape">The light shape.</param>
		/// <param name="lux">The base brightness in lux.</param>
		/// <returns>true if the lighting was handled, or false otherwise.</returns>
		internal bool PreviewLight(int origin, float radius, LightShape shape, int lux) {
			bool handled = false;
			var owner = PreviewObject;
			int index = shape - LightShape.Cone - 1;
			if (index >= 0 && index < shapes.Count && owner != null) {
				var cells = DictionaryPool<int, float, PLightManager>.Allocate();
				// Found handler!
				shapes[index]?.FillLight(new LightingArgs(owner, origin,
					(int)radius, cells));
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
				PreviewObject = null;
				handled = true;
				cells.Recycle();
			}
			return handled;
		}

		/// <summary>
		/// Registers a light shape handler.
		/// </summary>
		/// <param name="identifier">A unique identifier for this shape. If another mod has
		/// already registered that identifier, the previous mod will take precedence.</param>
		/// <param name="handler">The handler for that shape.</param>
		/// <param name="rayMode">The type of visual rays that are displayed from the light.</param>
		/// <returns>The light shape which can be used.</returns>
		public ILightShape Register(string identifier, CastLightDelegate handler,
				LightShape rayMode = (LightShape)(-1)) {
			if (string.IsNullOrEmpty(identifier))
				throw new ArgumentNullException(nameof(identifier));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));
			ILightShape lightShape = null;
			RegisterForForwarding();
			// Try to find a match for this identifier
			var registered = GetSharedData(EMPTY_SHAPES);
			int n = registered.Count;
			foreach (var obj in registered) {
				var light = PRemoteLightWrapper.LightToInstance(obj);
				// Might be from another assembly so the types may or may not be compatible
				if (light != null && light.Identifier == identifier) {
					LogLightingDebug("Found existing light shape: " + identifier + " from " +
						(obj.GetType().Assembly.GetNameSafe() ?? "?"));
					lightShape = light;
					break;
				}
			}
			if (lightShape == null) {
				// Not currently existing
				lightShape = new PLightShape(n + 1, identifier, handler, rayMode);
				LogLightingDebug("Registered new light shape: " + identifier);
				registered.Add(lightShape);
			}
			return lightShape;
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
			int index = state.shape - LightShape.Cone - 1;
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (index >= 0 && index < shapes.Count && litCells != null && brightCache.
					TryGetValue(source, out CacheEntry entry)) {
				var ps = shapes[index];
				var brightness = entry.Intensity;
				// Proper owner found
				brightness.Clear();
				entry.BaseLux = state.intensity;
				ps.FillLight(new LightingArgs(entry.Owner, state.origin, (int)state.
					radius, brightness));
				foreach (var point in brightness)
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
			internal int BaseLux { get; set; }

			/// <summary>
			/// The relative brightness per cell.
			/// </summary>
			internal BrightnessDict Intensity { get; }

			/// <summary>
			/// The owner which initiated the lighting call.
			/// </summary>
			internal GameObject Owner { get; }

			internal CacheEntry(GameObject owner) {
				// Do not use the pool because these might last a long time and be numerous
				Intensity = new Dictionary<int, float>(64);
				Owner = owner;
			}

			public override string ToString() {
				return "Lighting Cache Entry for " + (Owner == null ? "" : Owner.name);
			}
		}
	}
}
