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
using KMod;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.Metrics {
	/// <summary>
	/// Speeds up mod loading by compiling the list of blamable methods in the background.
	/// </summary>
	public static class LoadModPatches {
		/// <summary>
		/// Set to 1 when the thread compiling the mod list starts.
		/// </summary>
		private static volatile int compilingList;

		/// <summary>
		/// Compiles the patched method list from Harmony.
		/// </summary>
		internal static void CompileModPatches() {
			var mods = Global.Instance.modManager?.mods;
			if (mods != null) {
				var methodsByID = new Dictionary<string, LoadedModData>();
				// Index the mods list by Harmony ID
				foreach (var mod in mods) {
					var data = mod.loaded_mod_data;
					var h = data?.harmony;
					if (h != null) {
						// Remove anything that was loaded before us
						data.patched_methods?.Clear();
						data.patched_methods = new HashSet<MethodBase>();
						if (!string.IsNullOrEmpty(h.Id))
							methodsByID[h.Id] = data;
					}
				}
				// Match up all patched methods to the mod itself
				foreach (var method in Harmony.GetAllPatchedMethods()) {
					var patchInfo = Harmony.GetPatchInfo(method);
					var owners = patchInfo?.Owners;
					if (owners != null)
						foreach (string owner in owners)
							if (methodsByID.TryGetValue(owner, out LoadedModData data))
								data.patched_methods.Add(method);
				}
			}
		}

		/// <summary>
		/// Applied to DLLLoader to fix some very slow method search code. Although this method
		/// is on the call stack when patched, it will apply to the next and future
		/// invocations.
		/// </summary>
		[HarmonyPatch(typeof(DLLLoader), nameof(DLLLoader.LoadDLLs))]
		internal static class LoadDLLs_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ModLoadOpts;

			/// <summary>
			/// Transpiles LoadDLLs to remove a slow patched_methods populate, it will be done in
			/// the background after mods load.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var start = typeof(LoadedModData).GetMethodSafe(nameof(LoadedModData.harmony),
					false);
				var end = typeof(LoadedModData).GetFieldSafe(nameof(LoadedModData.
					patched_methods), false);
				// To find the LAST use of a method, streaming enumerables cannot be used
				var method = new List<CodeInstruction>(instructions);
				int n = method.Count, si = -1;
				bool patched = false;
				if (start != null && end != null)
					for (int i = n - 1; i > 0 && !patched; i--) {
						var instr = method[i];
						if (instr.Is(OpCodes.Stfld, end))
							si = i;
						else if (si > i + 1 && instr.Is(OpCodes.Ldfld, start)) {
							var endOp = method[si];
							// Pop off the instance that went into get_harmony, and the
							// instance that will go into get_patched_methods
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
							endOp.opcode = OpCodes.Pop;
							endOp.operand = null;
							method.RemoveRange(i + 1, si - i);
#if DEBUG
							PUtil.LogDebug("Patched DLLLoader.LoadDLLs");
#endif
							patched = true;
						}
					}
				if (!patched)
					PUtil.LogWarning("Unable to patch DLLLoader.LoadDLLs");
				return method;
			}
		}

		/// <summary>
		/// Applied to Mod to start the indexing during mod postload.
		/// </summary>
		[HarmonyPatch(typeof(Mod), nameof(Mod.PostLoad))]
		internal static class PostLoad_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ModLoadOpts;

			/// <summary>
			/// Applied before PostLoad runs.
			/// </summary>
			[HarmonyPriority(Priority.High)]
			internal static void Prefix() {
				if (Interlocked.Exchange(ref compilingList, 1) == 0) {
					var compileThread = new Thread(CompileModPatches) {
						Priority = ThreadPriority.BelowNormal, IsBackground = true,
						Name = "Blame Other Mods for Failures"
					};
					Util.ApplyInvariantCultureToThread(compileThread);
					compileThread.Start();
				}
			}
		}
	}
}
