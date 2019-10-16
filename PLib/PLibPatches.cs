/*
 * Copyright 2019 Peter Han
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

using Harmony;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Lighting;
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using LightGridEmitter = LightGridManager.LightGridEmitter;
using IntHandle = HandleVector<int>.Handle;

namespace PeterHan.PLib {
	/// <summary>
	/// All patches for PLib are stored here and only applied once for all PLib mods loaded.
	/// </summary>
	sealed class PLibPatches {
		#region Patches

#pragma warning disable IDE0051 // Remove unused private members

		/// <summary>
		/// Applied to modify SteamUGCService to silence "Preview image load failed".
		/// </summary>
		private static IEnumerable<CodeInstruction> LoadPreviewImage_Transpile(
				IEnumerable<CodeInstruction> body) {
			const string BLACKLIST = "LogFormat";
			var returnBody = new List<CodeInstruction>(body);
			int n = returnBody.Count;
			// Look for "call Debug.LogFormat" and wipe it with NOP
			for (int i = 0; i < n; i++) {
				var instr = returnBody[i];
				if (instr.opcode.Name == "call" && (instr.operand as MethodBase)?.Name ==
						BLACKLIST && i > 3) {
					// Patch this instruction and the 3 before it (ldstr, ldc, newarr)
					for (int j = i - 3; j <= i; j++) {
						instr = returnBody[j];
						instr.opcode = OpCodes.Nop;
						instr.operand = null;
					}
					PRegistry.LogPatchDebug("No more preview image load failure ({0:D})".F(i));
				}
			}
			return returnBody;
		}

		/// <summary>
		/// Applied to Light2D to properly attribute lighting sources.
		/// </summary>
		private static bool AddToScenePartitioner_Prefix(Light2D __instance,
				ref IntHandle ___solidPartitionerEntry,
				ref IntHandle ___liquidPartitionerEntry) {
			var lm = PLightManager.Instance;
			var obj = __instance.gameObject;
			bool cont = true;
			if (lm != null && obj != null) {
				// Replace the whole method since the radius could use different algorithms
				lm.CallingObject = obj;
				cont = !PLightManager.AddScenePartitioner(__instance,
					ref ___solidPartitionerEntry, ref ___liquidPartitionerEntry);
			}
			return cont;
		}

		/// <summary>
		/// Applied to LightGridEmitter to unattribute lighting sources.
		/// </summary>
		private static void AddToGrid_Postfix() {
			var lm = PLightManager.Instance;
			if (lm != null)
				lm.CallingObject = null;
		}

		/// <summary>
		/// Applied to ModsScreen if mod options are registered, after BuildDisplay runs.
		/// </summary>
		private static void BuildDisplay_Postfix(object ___displayedMods) {
			POptions.BuildDisplay(___displayedMods);
		}

		/// <summary>
		/// Applied to KeyDef (constructor) to adjust array lengths if necessary.
		/// </summary>
		private static void CKeyDef_Postfix(KInputController.KeyDef __instance) {
			__instance.mActionFlags = PActionManager.ExtendFlags(__instance.mActionFlags,
				PActionManager.Instance.GetMaxAction());
		}

		/// <summary>
		/// Applied to LightGridEmitter to compute the lux values properly.
		/// </summary>
		private static bool ComputeLux_Prefix(LightGridEmitter __instance, int cell,
				LightGridEmitter.State ___state, ref int __result) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.GetBrightness(__instance, cell, ___state, out __result);
		}

		/// <summary>
		/// Applied to LightGridManager to properly preview a new light source.
		/// 
		/// RadiationGridManager.CreatePreview has no references so no sense in patching that
		/// yet.
		/// </summary>
		private static bool CreatePreview_Prefix(int origin_cell, float radius,
				LightShape shape, int lux) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.PreviewLight(origin_cell, radius, shape, lux);
		}

		/// <summary>
		/// Applied to BuildingTemplates to properly debug missing building anims.
		/// </summary>
		private static void CreateBuildingDef_Postfix(BuildingDef __result, string anim,
				string id) {
			var animFiles = __result?.AnimFiles;
			if (animFiles != null && animFiles.Length > 0 && animFiles[0] == null)
				Debug.LogWarningFormat("(when looking for KAnim named {0} on building {1})",
					anim, id);
		}

		/// <summary>
		/// Applied to DiscreteShadowCaster to handle lighting requests.
		/// </summary>
		private static bool GetVisibleCells_Prefix(int cell, List<int> visiblePoints,
				int range, LightShape shape) {
			bool exec = true;
			var lm = PLightManager.Instance;
			if (shape != LightShape.Circle && shape != LightShape.Cone && lm != null)
				// This is not a customer scenario
				exec = !lm.GetVisibleCells(cell, visiblePoints, range, shape);
			return exec;
		}

		/// <summary>
		/// Applied to Db to register PLib buildings.
		/// </summary>
		private static void Initialize_Prefix() {
			PBuilding.AddAllTechs();
		}

		/// <summary>
		/// Applied to KInputController to adjust array lengths if necessary.
		/// </summary>
		private static void IsActive_Prefix(ref bool[] ___mActionState) {
			___mActionState = PActionManager.ExtendFlags(___mActionState, PActionManager.
				Instance.GetMaxAction());
		}

		/// <summary>
		/// Applied to LightShapePreview to properly attribute lighting sources.
		/// </summary>
		private static void LightShapePreview_Update_Prefix(LightShapePreview __instance) {
			var lm = PLightManager.Instance;
			var obj = __instance.gameObject;
			if (lm != null && obj != null)
				lm.CallingObject = obj;
		}

		/// <summary>
		/// Applied to GeneratedBuildings to register PLib buildings.
		/// </summary>
		private static void LoadGeneratedBuildings_Prefix() {
			PBuilding.AddAllStrings();
		}

		/// <summary>
		/// Applied to Rotatable to rotate light previews if a visualizer is rotated.
		/// </summary>
		private static void OrientVisualizer_Postfix(Rotatable __instance) {
			var obj = __instance.gameObject;
			LightShapePreview preview;
			// Force regeneration on next Update()
			if (obj != null && (preview = obj.GetComponent<LightShapePreview>()) != null)
				Traverse.Create(preview).SetField("previousCell", -1);
		}

		/// <summary>
		/// Applied to KInputController to adjust array lengths if necessary.
		/// </summary>
		private static void QueueButtonEvent_Prefix(ref bool[] ___mActionState,
				KInputController.KeyDef key_def) {
			if (KInputManager.isFocused) {
				int max = PActionManager.Instance.GetMaxAction();
				key_def.mActionFlags = PActionManager.ExtendFlags(key_def.mActionFlags, max);
				___mActionState = PActionManager.ExtendFlags(___mActionState, max);
			}
		}

		/// <summary>
		/// Applied to LightGridEmitter to properly attribute lighting sources.
		/// </summary>
		private static void RefreshShapeAndPosition_Postfix(Light2D __instance) {
			var lm = PLightManager.Instance;
			var obj = __instance.gameObject;
			if (lm != null && obj != null)
				lm.CallingObject = obj;
		}

		/// <summary>
		/// Applied to LightGridEmitter to clean up the trash when it is removed from grid.
		/// </summary>
		private static void RemoveFromGrid_Postfix(LightGridEmitter __instance) {
			PLightManager.Instance?.DestroyLight(__instance);
		}

		/// <summary>
		/// Applied to GameInputMapping to update the action count if new actions are
		/// registered.
		/// </summary>
		private static void SetDefaultKeyBindings_Postfix() {
			PActionManager.Instance.UpdateMaxAction();
		}

		/// <summary>
		/// Applied to LightGridEmitter to update lit cells upon a lighting request.
		/// </summary>
		private static bool UpdateLitCells_Prefix(LightGridEmitter __instance,
				List<int> ___litCells, LightGridEmitter.State ___state) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.UpdateLitCells(__instance, ___state, ___litCells);
		}

		/// <summary>
		/// Applies all patches.
		/// </summary>
		/// <param name="instance">The Harmony instance to use when patching.</param>
		private static void PatchAll(HarmonyInstance instance) {
			if (instance == null)
				throw new ArgumentNullException("instance");

			// GameInputMapping
			instance.Patch(typeof(GameInputMapping), "SetDefaultKeyBindings", null,
				PatchMethod(nameof(SetDefaultKeyBindings_Postfix)));

			// KInputController
			instance.PatchConstructor(typeof(KInputController.KeyDef), new Type[] {
				typeof(KKeyCode), typeof(Modifier)
			}, null, PatchMethod(nameof(CKeyDef_Postfix)));
			instance.Patch(typeof(KInputController), "IsActive",
				PatchMethod(nameof(IsActive_Prefix)), null);
			instance.Patch(typeof(KInputController), "QueueButtonEvent",
				PatchMethod(nameof(QueueButtonEvent_Prefix)), null);

			if (PLightManager.InitInstance()) {
				// DiscreteShadowCaster
				instance.Patch(typeof(DiscreteShadowCaster), "GetVisibleCells",
					PatchMethod(nameof(GetVisibleCells_Prefix)), null);

				// Light2D
				instance.Patch(typeof(Light2D), "AddToScenePartitioner",
					PatchMethod(nameof(AddToScenePartitioner_Prefix)), null);
				instance.Patch(typeof(Light2D), "RefreshShapeAndPosition", null,
					PatchMethod(nameof(RefreshShapeAndPosition_Postfix)));

				// LightGridEmitter
				instance.Patch(typeof(LightGridEmitter), "AddToGrid", null,
					PatchMethod(nameof(AddToGrid_Postfix)));
				instance.Patch(typeof(LightGridEmitter), "ComputeLux",
					PatchMethod(nameof(ComputeLux_Prefix)), null);
				instance.Patch(typeof(LightGridEmitter), "RemoveFromGrid",
					null, PatchMethod(nameof(RemoveFromGrid_Postfix)));
				instance.Patch(typeof(LightGridEmitter), "UpdateLitCells",
					PatchMethod(nameof(UpdateLitCells_Prefix)), null);

				// LightGridManager
				instance.Patch(typeof(LightGridManager), "CreatePreview",
					PatchMethod(nameof(CreatePreview_Prefix)), null);

				// LightShapePreview
				instance.Patch(typeof(LightShapePreview), "Update",
					PatchMethod(nameof(LightShapePreview_Update_Prefix)), null);

				// Rotatable
				instance.Patch(typeof(Rotatable), "OrientVisualizer", null,
					PatchMethod(nameof(OrientVisualizer_Postfix)));
			}

			// PBuilding
			instance.Patch(typeof(BuildingTemplates), "CreateBuildingDef", null,
				PatchMethod(nameof(CreateBuildingDef_Postfix)));
			if (PBuilding.CheckBuildings()) {
				instance.Patch(typeof(Db), "Initialize",
					PatchMethod(nameof(Initialize_Prefix)), null);
				instance.Patch(typeof(GeneratedBuildings), "LoadGeneratedBuildings",
					PatchMethod(nameof(LoadGeneratedBuildings_Prefix)), null);
			}

			// ModsScreen
			POptions.Init();
			instance.Patch(typeof(ModsScreen), "BuildDisplay", null,
				PatchMethod(nameof(BuildDisplay_Postfix)));

			// SteamUGCService
			try {
				instance.PatchTranspile(typeof(SteamUGCService), "LoadPreviewImage",
					PatchMethod(nameof(LoadPreviewImage_Transpile)));
			} catch (TypeLoadException) {
				// Not a Steam install, ignoring
			}

			// Postload
			PUtil.ExecutePostload();
		}

#pragma warning restore IDE0051 // Remove unused private members

#endregion

#region Infrastructure

		/// <summary>
		/// Returns a patch method from this class. It must be static.
		/// </summary>
		/// <param name="name">The patch method name.</param>
		/// <returns>The matching method.</returns>
		private static HarmonyMethod PatchMethod(string name) {
			var method = typeof(PLibPatches).GetMethod(name, BindingFlags.
				NonPublic | BindingFlags.Static);
			if (method == null)
				PRegistry.LogPatchWarning("No PLibPatches method found: " + name);
			return new HarmonyMethod(method);
		}

		/// <summary>
		/// The version of PLib that these patches will represent.
		/// </summary>
		public string MyVersion { get; }

		public PLibPatches() {
			MyVersion = PVersion.VERSION;
		}

		/// <summary>
		/// Applies the patches for this version of PLib.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		public void Apply(HarmonyInstance instance) {
			PRegistry.LogPatchDebug("Using version " + MyVersion);
			try {
				PatchAll(instance);
			} catch (TypeLoadException e) {
				PUtil.LogException(e);
			} catch (TargetInvocationException e) {
				PUtil.LogException(e);
			}
			PActionManager.Instance.Init();
			PRegistry.LogPatchDebug("PLib patches applied");
		}

		public override bool Equals(object obj) {
			return obj is PLibPatches other && other.MyVersion == MyVersion;
		}

		public override int GetHashCode() {
			return MyVersion.GetHashCode();
		}

		public override string ToString() {
			return "PLibPatches version " + MyVersion;
		}

#endregion
	}
}
