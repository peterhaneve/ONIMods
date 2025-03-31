/*
 * Copyright 2024 Peter Han
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
using System;

#if DEBUG
using PeterHan.PLib.Core;
#endif

using KAnimBatchTextureCache = KAnimBatchGroup.KAnimBatchTextureCache;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to KAnimBatch to be a little smarter when deregistering anims about what to
	/// mark dirty.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Deregister))]
	public static class KAnimBatch_Deregister_Patch {
		private const int VERTICES = KBatchedAnimInstanceData.SIZE_IN_FLOATS;

		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before Deregister runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(KAnimConverter.IAnimConverter controller,
				KAnimBatch __instance) {
			var controllersToIndex = __instance.controllersToIdx;
			if (!App.IsExiting && controllersToIndex.TryGetValue(controller, out int index)) {
				var controllers = __instance.controllers;
				var dirtySet = __instance.dirtySet;
				var bs = __instance.batchset;
				var dataTex = __instance.dataTex;
				// All the other anims above it need to be marked dirty
				var data = dataTex.GetFloatDataPointer();
				int end = Math.Max(0, __instance.currentOffset - VERTICES), n = dirtySet.Count;
				controller.SetBatch(null);
				controllers.RemoveAt(index);
				controllersToIndex.Remove(controller);
				var dirty = ListPool<int, KAnimBatch>.Allocate();
				// Save every existing dirty index less than the deregistered one
				for (int i = 0; i < n; i++) {
					int dirtyIdx = dirtySet[i];
					if (dirtyIdx < index)
						dirty.Add(dirtyIdx);
				}
				dirtySet.Clear();
				dirtySet.AddRange(dirty);
				dirty.Recycle();
				n = controllers.Count;
				// Refresh the index mapping table and mark everything moved-down as dirty
				for (int i = index; i < n; i++) {
					controllersToIndex[controllers[i]] = i;
					dirtySet.Add(i);
				}
				bs.SetDirty();
				__instance.needsWrite = true;
				// Invalidate the data beyond the end
				for (int i = 0; i < VERTICES; i++)
					data[end + i] = -1f;
				dataTex.Apply();
				__instance.currentOffset = end;
				// If this was the last item, destroy the texture
				if (n <= 0) {
					bs.RemoveBatch(__instance);
					__instance.Clear();
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to tame some data structure abuse when registering kanims.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Register))]
	public static class KAnimBatch_Register_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before Register runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(KAnimConverter.IAnimConverter controller,
				KAnimBatch __instance, ref bool __result) {
			var batch = controller.GetBatch();
			if (batch != __instance) {
				var dirtySet = __instance.dirtySet;
				var controllers = __instance.controllers;
				var controllersToIndex = __instance.controllersToIdx;
				// Create the texture if it is null
				var tex = __instance.dataTex;
				if (tex == null || !__instance.isSetup)
					__instance.Init();
				// If already present [how is this possible?], just mark it dirty
				if (controllersToIndex.TryGetValue(controller, out int index)) {
					if (!dirtySet.Contains(index))
						dirtySet.Add(index);
				} else {
					int n = controllers.Count;
					controllers.Add(controller);
					dirtySet.Add(n);
					controllersToIndex.Add(controller, n);
					// Allocate additional spots in the texture
					__instance.currentOffset += KBatchedAnimInstanceData.SIZE_IN_FLOATS;
				}
				__instance.batchset.SetDirty();
				__instance.needsWrite = true;
				batch?.Deregister(controller);
				controller.SetBatch(__instance);
			} else {
#if DEBUG
				PUtil.LogDebug("Registered a controller to its existing batch!");
#endif
			}
			__result = true;
			return false;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to optimize dirty management slightly.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.UpdateDirty))]
	public static class KAnimBatch_UpdateDirtyFull_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before UpdateDirty runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref int __result, KAnimBatch __instance) {
			int updated = 0;
			if (__instance.needsWrite) {
				// Create the texture if it is null
				var dataTex = __instance.dataTex;
				if (dataTex == null || !__instance.isSetup) {
					__instance.Init();
					dataTex = __instance.dataTex;
				}
				if (__instance.dirtySet.Count > 0)
					updated = UpdateDirtySet(__instance);
				if (updated == 0)
					dataTex.Apply();
				// Update those mesh renderers too
				else if (FastTrackOptions.Instance.MeshRendererOptions !=
						FastTrackOptions.MeshRendererSettings.None)
					KAnimMeshRendererPatches.UpdateMaterialProperties(__instance);
			}
			__result = updated;
			return false;
		}

		/// <summary>
		/// Sets up the override texture if necessary.
		/// </summary>
		/// <param name="instance">The batch to override.</param>
		/// <param name="overrideTex">The current override texture.</param>
		/// <returns>The new override texture.</returns>
		private static KAnimBatchTextureCache.Entry SetupOverride(KAnimBatch instance,
				KAnimBatchTextureCache.Entry overrideTex) {
			if (overrideTex == null) {
				var bg = instance.group;
				var properties = instance.matProperties;
				var size = KAnimBatchGroup.GetBestTextureSize(bg.data.
					maxSymbolFrameInstancesPerbuild * bg.maxGroupSize *
					SymbolOverrideInfoGpuData.FLOATS_PER_SYMBOL_OVERRIDE_INFO);
				overrideTex = bg.CreateTexture("SymbolOverrideInfoTex", size.x, size.y,
					KAnimBatch.ShaderProperty_symbolOverrideInfoTex, KAnimBatch.
					ShaderProperty_SYMBOL_OVERRIDE_INFO_TEXTURE_SIZE);
				overrideTex.SetTextureAndSize(properties);
				properties.SetFloat(KAnimBatch.ShaderProperty_SUPPORTS_SYMBOL_OVERRIDING, 1f);
				instance.symbolOverrideInfoTex = overrideTex;
			}
			return overrideTex;
		}
		
		/// <summary>
		/// Updates all dirty override textures.
		/// </summary>
		/// <param name="instance">The instance to update.</param>
		/// <returns>The number of textures updated in this way.</returns>
		private static int UpdateDirtySet(KAnimBatch instance) {
			bool symbolDirty = false, overrideDirty = false;
			int updated = 0;
			var overrideTex = instance.symbolOverrideInfoTex;
			var symbolTex = instance.symbolInstanceTex;
			var dataTex = instance.dataTex;
			var dirtySet = instance.dirtySet;
			var controllers = instance.controllers;
			foreach (int index in dirtySet) {
				var controller = controllers[index];
				if (controller is UnityEngine.Object obj && obj != null) {
					// Update the textures; they are different over 90% of the time, so
					// almost no gain from checking if actually dirty
					instance.WriteBatchedAnimInstanceData(index, controller, dataTex.
						GetDataPointer());
					symbolDirty |= instance.WriteSymbolInstanceData(index, controller,
						symbolTex.GetDataPointer());
					if (controller.ApplySymbolOverrides()) {
						overrideTex = SetupOverride(instance, overrideTex);
						overrideDirty |= instance.WriteSymbolOverrideInfoTex(index, controller,
							overrideTex.GetDataPointer());
					}
					updated++;
				}
			}
			if (updated > 0) {
				// Write any dirty textures
				dirtySet.Clear();
				instance.needsWrite = false;
				dataTex.Apply();
				if (symbolDirty)
					symbolTex.Apply();
				if (overrideDirty)
					overrideTex.Apply();
			} else
				PUtil.LogWarning("No textures were updated");
			return updated;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to update the mesh renderer properties after the anim is updated.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.UpdateDirty))]
	public static class KAnimBatch_UpdateDirtyLite_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.MeshRendererOptions != FastTrackOptions.MeshRendererSettings.
				None && !options.AnimOpts;
		}

		/// <summary>
		/// Applied after UpdateDirty runs.
		/// </summary>
		internal static void Postfix(int __result, KAnimBatch __instance) {
			if (__result > 0)
				KAnimMeshRendererPatches.UpdateMaterialProperties(__instance);
		}
	}
}
