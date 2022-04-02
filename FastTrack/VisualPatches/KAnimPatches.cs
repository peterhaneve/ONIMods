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
using PeterHan.PLib.Detours;
using System;

using KAnimBatchTextureCache = KAnimBatchGroup.KAnimBatchTextureCache;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to KAnimBatch to... actually clear the dirty flag when it updates.
	/// Unfortunately most anims are marked dirty every frame anyways.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), "ClearDirty")]
	public static class KAnimBatch_ClearDirty_Patch {
		internal static bool Prepare() => !FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied after ClearDirty runs.
		/// </summary>
		internal static void Postfix(ref bool ___needsWrite) {
			___needsWrite = false;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to intelligently resize the arrays and avoid just marking the
	/// whole thing dirty when something is deregistered.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Deregister))]
	public static class KAnimBatch_Deregister_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before Deregister runs.
		/// </summary>
		internal static bool Prefix(KAnimConverter.IAnimConverter controller,
				KAnimBatch __instance) {
			const int VERTICES = KBatchedAnimInstanceData.SIZE_IN_FLOATS;
			if (!App.IsExiting) {
				var controllers = __instance.controllers;
				int index = controllers.IndexOf(controller);
				if (index >= 0) {
					var dirtySet = __instance.dirtySet;
					var controllersToIndex = __instance.controllersToIdx;
					// All the other anims above it need to be marked dirty
					float[] data = __instance.dataTex.floats;
					int end = Math.Max(0, __instance.currentOffset - VERTICES), n;
					controller.SetBatch(null);
					controllers.RemoveAt(index);
					if (!controllersToIndex.Remove(controller))
						PUtil.LogWarning("Deregistering " + controller.GetName() +
							" but not found in the index table!");
					n = controllers.Count;
					dirtySet.Clear();
					// Refresh the index mapping table
					for (int i = index; i < n; i++) {
						controllersToIndex[controllers[i]] = i;
						if (!dirtySet.Contains(i))
							dirtySet.Add(i);
					}
					__instance.batchset.SetDirty();
					__instance.needsWrite = true;
					// Invalidate the data beyond the end
					for (int i = 0; i < VERTICES; i++)
						data[end + i] = -1f;
					__instance.currentOffset = end;
					// If this was the last item, destroy the texture
					if (n <= 0) {
						__instance.batchset.RemoveBatch(__instance);
						__instance.DestroyTex();
					}
				} else
					PUtil.LogWarning("Deregistering " + controller.GetName() +
						" but it is not in this batch!");
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to tame some data structure abuse when registering kanims.
	/// Target time is 590 us/call
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Register))]
	public static class KAnimBatch_Register_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before Register runs.
		/// </summary>
		internal static bool Prefix(KAnimConverter.IAnimConverter controller,
				KAnimBatch __instance, ref bool __result) {
			var batch = controller.GetBatch();
			if (batch != __instance) {
				var dirtySet = __instance.dirtySet;
				var controllers = __instance.controllers;
				var controllersToIndex = __instance.controllersToIdx;
				// Create the texture if it is null
				var tex = __instance.dataTex;
				if (tex == null || tex.floats.Length < 1)
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
				if (batch != null)
					batch.Deregister(controller);
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
	/// Applied to KAnimBatch to centralize the dirty management in a proper hash set.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.UpdateDirty))]
	public static class KAnimBatch_UpdateDirtyFull_Patch {
		/// <summary>
		/// Publicizer cannot find this property's setter!
		/// </summary>
		private static readonly IDetouredField<KAnimBatch, KAnimBatchTextureCache.Entry>
			OVERRIDE_TEX = PDetours.DetourField<KAnimBatch, KAnimBatchTextureCache.Entry>(
			nameof(KAnimBatch.symbolOverrideInfoTex));

		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

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
				overrideTex = bg.CreateTexture("SymbolOverrideInfoTex", KAnimBatchGroup.
					GetBestTextureSize(bg.data.maxSymbolFrameInstancesPerbuild * bg.
					maxGroupSize * SymbolOverrideInfoGpuData.FLOATS_PER_SYMBOL_OVERRIDE_INFO),
					KAnimBatch.ShaderProperty_symbolOverrideInfoTex, KAnimBatch.
					ShaderProperty_SYMBOL_OVERRIDE_INFO_TEXTURE_SIZE);
				overrideTex.SetTextureAndSize(properties);
				properties.SetFloat(KAnimBatch.ShaderProperty_SUPPORTS_SYMBOL_OVERRIDING, 1f);
				OVERRIDE_TEX.Set(instance, overrideTex);
			}
			return overrideTex;
		}

		/// <summary>
		/// Applied before UpdateDirty runs.
		/// </summary>
		internal static bool Prefix(ref int __result, KAnimBatch __instance) {
			int writtenLastFrame = 0;
			if (__instance.needsWrite) {
				bool symbolDirty = false, overrideDirty = false;
				var controllers = __instance.controllers;
				var dirtySet = __instance.dirtySet;
				// Create the texture if it is null
				var tex = __instance.dataTex;
				if (tex == null || tex.floats.Length == 0) {
					__instance.Init();
					tex = __instance.dataTex;
				}
				var overrideTex = __instance.symbolOverrideInfoTex;
				foreach (int index in dirtySet) {
					var converter = controllers[index];
					if (converter is UnityEngine.Object obj && obj != null) {
						// Update the textures
						__instance.WriteBatchedAnimInstanceData(index, converter);
						symbolDirty |= __instance.WriteSymbolInstanceData(index, converter);
						if (converter.ApplySymbolOverrides()) {
							overrideTex = SetupOverride(__instance, overrideTex);
							overrideDirty |= __instance.WriteSymbolOverrideInfoTex(index,
								converter);
						}
						writtenLastFrame++;
					}
				}
				dirtySet.Clear();
				__instance.needsWrite = false;
				// Write any dirty textures
				tex.LoadRawTextureData();
				tex.Apply();
				if (symbolDirty) {
					var symbolTex = __instance.symbolInstanceTex;
					symbolTex.LoadRawTextureData();
					symbolTex.Apply();
				}
				if (overrideDirty) {
					overrideTex.LoadRawTextureData();
					overrideTex.Apply();
				}
				// Update those mesh renderers too
				if (writtenLastFrame > 0 && FastTrackOptions.Instance.MeshRendererOptions !=
						FastTrackOptions.MeshRendererSettings.None)
					KAnimMeshRendererPatches.UpdateMaterialProperties(__instance);
			}
			__result = writtenLastFrame;
			return false;
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
