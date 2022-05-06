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

using RunLoader = AsyncLoadManager<IGlobalAsyncLoader>.RunLoader;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.Metrics {
	/// <summary>
	/// Groups shared loading patches.
	/// </summary>
	public static class AsyncLoadPatches {
		/// <summary>
		/// Warms up the game hash cache. This is run every load for some reason...
		/// </summary>
		private static void LoadGameHashes() {
			var cache = HashCache.Get();
			foreach (object gh in Enum.GetValues(typeof(GameHashes)))
				if (gh is GameHashes hash)
					cache.Add((int)hash, hash.ToString());
			foreach (object uh in Enum.GetValues(typeof(UtilHashes)))
				if (uh is UtilHashes hash)
					cache.Add((int)hash, hash.ToString());
			foreach (object ih in Enum.GetValues(typeof(UIHashes)))
				if (ih is UIHashes hash)
					cache.Add((int)hash, hash.ToString());
		}

		/// <summary>
		/// Reduce the time that async loaders take to index, by only indexing the game
		/// assemblies and not stuff like UnityEngine.
		/// </summary>
		private static void QuickAsyncLoad() {
			var collectedLoaders = new List<AsyncLoader>();
			var loaderFor = AsyncLoadManager<IGlobalAsyncLoader>.loaders;
			// Only index the game assemblies
			var assemblies = new Assembly[] { typeof(Game).Assembly, typeof(KAnim).Assembly };
			int n = assemblies.Length;
			loaderFor.Clear();
			for (int i = 0; i < n; i++)
				foreach (var type in assemblies[i].GetTypes())
					if (!type.IsAbstract && typeof(IGlobalAsyncLoader).IsAssignableFrom(type))
					{
						var loadInstance = Activator.CreateInstance(type) as AsyncLoader;
						collectedLoaders.Add(loadInstance);
						loaderFor[type] = loadInstance;
						loadInstance.CollectLoaders(collectedLoaders);
					}
#if DEBUG
			PUtil.LogDebug("Async loading {0:D} types".F(collectedLoaders.Count));
#endif
			// Run them all in parallel (base game does it too!)
			if (loaderFor.Count > 0) {
				var jobs = new WorkItemCollection<RunLoader, object>();
				jobs.Reset(null);
				foreach (var loader in collectedLoaders)
					jobs.Add(new RunLoader { loader = loader });
				GlobalJobManager.Run(jobs);
			}
			collectedLoaders.Clear();
			LoadGameHashes();
		}

		/// <summary>
		/// Applied to AsyncLoadManager to run some async loaders without scanning every type
		/// in the app domain.
		/// </summary>
		[HarmonyPatch]
		internal class Run_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

			/// <summary>
			/// Target a specific generic instance.
			/// </summary>
			internal static MethodBase TargetMethod() {
				return typeof(AsyncLoadManager<IGlobalAsyncLoader>).GetMethodSafe(nameof(
					AsyncLoadManager<IGlobalAsyncLoader>.Run), true, PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Applied before Run runs.
			/// </summary>
			internal static bool Prefix() {
				QuickAsyncLoad();
				return false;
			}
		}
	}

	/// <summary>
	/// Applied to Game to remove some extra work done on every load.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.LoadEventHashes))]
	public static class Game_LoadEventHashes_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

		/// <summary>
		/// Applied before LoadEventHashes runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
