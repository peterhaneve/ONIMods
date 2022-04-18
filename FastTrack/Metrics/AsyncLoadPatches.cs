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
		/// Triggered when the anims are loaded.
		/// </summary>
		private static readonly EventWaitHandle animsDone = new AutoResetEvent(false);

		/// <summary>
		/// Loads the anims on a background thread.
		/// </summary>
		private static void AsyncLoadAnims() {
#if DEBUG
			PUtil.LogDebug("Loading anims on thread " + Thread.CurrentThread.ManagedThreadId);
#endif
			try {
				KAnimGroupFile.LoadAll();
				KAnimBatchManager.Instance().CompleteInit();
			} catch (Exception e) {
				DebugUtil.LogException(Assets.instance, e.Message, e);
			}
			animsDone.Set();
			LoadGameHashes();
		}

		/// <summary>
		/// Loads the codex on a background thread.
		/// </summary>
		private static void AsyncLoadCodex() {
			var loadCodexTask = new Thread(LoadCodex) {
				Name = "Read and Parse Codex", IsBackground = true, Priority = ThreadPriority.
				BelowNormal, CurrentCulture = Thread.CurrentThread.CurrentCulture
			};
#if DEBUG
			PUtil.LogDebug("Async loading codex cache");
#endif
			loadCodexTask.Start();
		}

		/// <summary>
		/// Loads up the anims in a thread.
		/// </summary>
		private static void LoadAnims() {
			var loadAnimsTask = new Thread(AsyncLoadAnims) {
				Name = "Load and Parse Anims", IsBackground = true, Priority =
				ThreadPriority.Normal, CurrentCulture = Thread.CurrentThread.CurrentCulture
			};
			animsDone.Reset();
			Thread.MemoryBarrier();
			loadAnimsTask.Start();
		}

		/// <summary>
		/// Loads the codex on a background thread, logging any errors.
		/// </summary>
		private static void LoadCodex() {
			try {
				CodexCache.CodexCacheInit();
			} catch (Exception e) {
				DebugUtil.LogException(Global.Instance, e.Message, e);
			}
		}

		/// <summary>
		/// Warms up the game hash cache. This is run every load for some reason...
		/// </summary>
		private static void LoadGameHashes() {
			foreach (object gh in Enum.GetValues(typeof(GameHashes))) {
				if (gh is GameHashes hash)
					HashCache.Get().Add((int)hash, hash.ToString());
			}
			foreach (object uh in Enum.GetValues(typeof(UtilHashes))) {
				if (uh is UtilHashes hash)
					HashCache.Get().Add((int)hash, hash.ToString());
			}
			foreach (object ih in Enum.GetValues(typeof(UIHashes))) {
				if (ih is UIHashes hash)
					HashCache.Get().Add((int)hash, hash.ToString());
			}
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
		}

		/// <summary>
		/// Applied to Assets to load anims on the background. This runs before the second
		/// patch, so do not wait for the substance list hookup as that happens later.
		/// </summary>
		[HarmonyPatch(typeof(Assets), nameof(Assets.LoadAnims))]
		internal static class LoadAnims_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

			/// <summary>
			/// Transpiles LoadAnims to start up anim loading in the background.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				return PPatchTools.ReplaceMethodCall(instructions, new Dictionary<MethodInfo,
						MethodInfo>() {
					{
						typeof(KAnimGroupFile).GetMethodSafe(nameof(KAnimGroupFile.LoadAll),
							true),
						typeof(AsyncLoadPatches).GetMethodSafe(nameof(LoadAnims), true)
					},
					{
						typeof(KAnimBatchManager).GetMethodSafe(nameof(KAnimBatchManager.
							CompleteInit), false),
						null
					}
				});
			}
		}

		/// <summary>
		/// Applied to ManagementMenu to start loading the codex cache in the background to
		/// speed up game loads.
		/// </summary>
		[HarmonyPatch(typeof(ManagementMenu), nameof(ManagementMenu.OnPrefabInit))]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

			/// <summary>
			/// Transpiles OnPrefabInit to remove slow method calls.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				return PPatchTools.ReplaceMethodCall(instructions, typeof(CodexCache).
					GetMethodSafe(nameof(CodexCache.CodexCacheInit), true),
					typeof(AsyncLoadPatches).GetMethodSafe(nameof(AsyncLoadCodex), true));
			}
		}

		/// <summary>
		/// Applied to AsyncLoadManager to run some async loaders without scanning every type
		/// in the app domain.
		/// </summary>
		[HarmonyPatch]
		internal class Run_Patch {
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

		/// <summary>
		/// Applied to Assets to join up the anim loading task after elements load.
		/// </summary>
		[HarmonyPatch(typeof(Assets), nameof(Assets.SubstanceListHookup))]
		internal static class SubstanceListHookup_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.LoadOpts;

			/// <summary>
			/// Applied after SubstanceListHookup runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static void Postfix() {
				if (!animsDone.WaitOne())
					PUtil.LogWarning("Anim loading did not complete within the timeout!");
#if DEBUG
				else
					PUtil.LogDebug("Anim loading complete");
#endif
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
