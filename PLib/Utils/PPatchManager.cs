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

using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;

using PostLoadHandler = System.Action<Harmony.HarmonyInstance>;
using PrivateRunList = System.Collections.Generic.ICollection<PeterHan.PLib.IPatchMethodInstance>;
using SharedRunList = System.Collections.Generic.ICollection<System.Action<uint>>;

namespace PeterHan.PLib {
	/// <summary>
	/// Manages patches that PLib will conditionally apply.
	/// </summary>
	internal static class PPatchManager {
		/// <summary>
		/// The base flags to use when matching instance or static methods.
		/// </summary>
		internal const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.DeclaredOnly;

		/// <summary>
		/// The flags to use when matching instance and static methods.
		/// </summary>
		internal const BindingFlags FLAGS_EITHER = FLAGS | BindingFlags.Static | BindingFlags.
			Instance;

		/// <summary>
		/// The Harmony instance used for patches of type Immediate.
		/// </summary>
		private static HarmonyInstance immediateInstance = null;

		/// <summary>
		/// Cached copy of the master patch dictionary.
		/// </summary>
		private static IDictionary<uint, SharedRunList> master = null;

		/// <summary>
		/// Patches and delegates to be run at specific points in the runtime. Put the kibosh
		/// on patching Db.Initialize()!
		/// </summary>
		private static IDictionary<uint, PrivateRunList> patches = null;

		/// <summary>
		/// Schedules a patch method instance to be run.
		/// </summary>
		/// <param name="when">When to run the patch.</param>
		/// <param name="instance">The patch method instance to run.</param>
		internal static void AddHandler(uint when, IPatchMethodInstance instance) {
			if (when == RunAt.Immediately)
				// Now now now!
				instance.Run(GetImmediateInstance());
			else
				lock (PSharedData.GetLock(PRegistry.KEY_POSTLOAD_LOCK)) {
					InitMaster();
					if (patches == null)
						patches = new Dictionary<uint, PrivateRunList>(8);
					if (!patches.TryGetValue(when, out PrivateRunList atTime)) {
						patches.Add(when, atTime = new List<IPatchMethodInstance>(16));
						// Register our mod in the master list
						if (!master.TryGetValue(when, out SharedRunList existing))
							master.Add(when, existing = new List<Action<uint>>(8));
						existing.Add(RunThisMod);
					}
					atTime.Add(instance);
				}
		}

		/// <summary>
		/// Checks to see if the conditions for a method running are met.
		/// </summary>
		/// <param name="assemblyName">The assembly name that must be present, or null if none is required.</param>
		/// <param name="typeName">The type full name that must be present, or null if none is required.</param>
		/// <param name="requiredType">The type that was required, if typeName was not null or empty.</param>
		/// <returns>true if the requirements are met, or false otherwise.</returns>
		internal static bool CheckConditions(string assemblyName, string typeName,
				out Type requiredType) {
			bool ok = false, emptyType = string.IsNullOrEmpty(typeName);
			if (string.IsNullOrEmpty(assemblyName)) {
				if (emptyType) {
					requiredType = null;
					ok = true;
				} else {
					requiredType = PPatchTools.GetTypeSafe(typeName);
					ok = requiredType != null;
				}
			} else if (emptyType) {
				requiredType = null;
				// Search for assembly only, by name
				foreach (var candidate in AppDomain.CurrentDomain.GetAssemblies())
					if (candidate.GetName().Name == assemblyName) {
						ok = true;
						break;
					}
			} else {
				requiredType = PPatchTools.GetTypeSafe(typeName, assemblyName);
				ok = requiredType != null;
			}
			return ok;
		}

		/// <summary>
		/// Executes all legacy post-load handlers.
		/// </summary>
		internal static void ExecuteLegacyPostload() {
			IList<PostLoadHandler> postload = null;
			lock (PSharedData.GetLock(PRegistry.KEY_POSTLOAD_LOCK)) {
				// Get list holding postload information
				var list = PSharedData.GetData<IList<PostLoadHandler>>(PRegistry.
					KEY_POSTLOAD_TABLE);
				if (list != null)
					postload = new List<PostLoadHandler>(list);
			}
			// If there were any, run them
			if (postload != null) {
				var hInst = HarmonyInstance.Create("PLib.PostLoad");
				PRegistry.LogPatchDebug("Executing {0:D} legacy post-load handler(s)".F(
					postload.Count));
				foreach (var handler in postload)
					try {
						handler?.Invoke(hInst);
					} catch (Exception e) {
						var method = handler.Method;
						// Say which mod's postload crashed
						if (method != null)
							PRegistry.LogPatchWarning("Postload handler for mod {0} failed:".F(
								method.DeclaringType.Assembly?.GetName()?.Name ?? "?"));
						PUtil.LogException(e);
					}
			}
		}

		/// <summary>
		/// Gets the instance of Harmony used for immediately patching.
		/// </summary>
		/// <returns>The Harmony instance (only created once) for this mod's immediate PLib
		/// methods / patches.</returns>
		private static HarmonyInstance GetImmediateInstance() {
			if (immediateInstance == null)
				immediateInstance = HarmonyInstance.Create("PLib.PostLoad.Immediate");
			return immediateInstance;
		}

		/// <summary>
		/// Initializes the master list of post load patches to apply.
		/// </summary>
		private static void InitMaster() {
			if (master == null) {
				var newMaster = PSharedData.GetData<IDictionary<uint, SharedRunList>>(
					PRegistry.KEY_POSTLOAD_ENHANCED);
				if (newMaster == null)
					PSharedData.PutData(PRegistry.KEY_POSTLOAD_ENHANCED, newMaster = new
						Dictionary<uint, SharedRunList>(8));
				master = newMaster;
			}
		}

		/// <summary>
		/// Runs all patches for all mods at the given time. Only to be run by the forwarded
		/// instance!
		/// </summary>
		/// <param name="when">The runtime (do not use Immediate) of patches to run.</param>
		internal static void RunAll(uint when) {
			SharedRunList toRun;
			lock (PSharedData.GetLock(PRegistry.KEY_POSTLOAD_LOCK)) {
				InitMaster();
				if (!master.TryGetValue(when, out toRun))
					toRun = null;
			}
			if (toRun != null) {
				PRegistry.LogPatchDebug("Executing handlers for stage {1} from {0:D} mod(s)".
					F(toRun.Count, RunAt.ToString(when)));
				foreach (var modHandler in toRun)
					modHandler?.Invoke(when);
			}
		}

		/// <summary>
		/// Runs all patches for the specified time.
		/// </summary>
		/// <param name="when">The time to run the patch.</param>
		private static void RunThisMod(uint when) {
			if (patches.TryGetValue(when, out PrivateRunList toRun)) {
				// Create Harmony instance for the patches
				var instance = HarmonyInstance.Create("PLib.PostLoad." + RunAt.ToString(when));
				foreach (var method in toRun)
					try {
						method.Run(instance);
					} catch (TargetInvocationException e) {
						// Use the inner exception
						PUtil.LogException(e.GetBaseException());
					} catch (Exception e) {
						// Say which mod's postload crashed
						PUtil.LogException(e);
					}
			}
		}
	}
}
