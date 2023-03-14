/*
 * Copyright 2023 Peter Han
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

using PrivateRunList = System.Collections.Generic.ICollection<PeterHan.PLib.PatchManager.
	IPatchMethodInstance>;

namespace PeterHan.PLib.PatchManager {
	/// <summary>
	/// Manages patches that PLib will conditionally apply.
	/// </summary>
	public sealed class PPatchManager : PForwardedComponent {
		/// <summary>
		/// The base flags to use when matching instance or static methods.
		/// </summary>
		internal const BindingFlags FLAGS = PPatchTools.BASE_FLAGS | BindingFlags.DeclaredOnly;

		/// <summary>
		/// The flags to use when matching instance and static methods.
		/// </summary>
		internal const BindingFlags FLAGS_EITHER = FLAGS | BindingFlags.Static | BindingFlags.
			Instance;

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PPatchManager Instance { get; private set; }

		/// <summary>
		/// true if the AfterModsLoad patches have been run, or false otherwise.
		/// </summary>
		private static volatile bool afterModsLoaded = false;

		private static void Game_DestroyInstances_Postfix() {
			Instance?.InvokeAllProcess(RunAt.OnEndGame, null);
		}

		private static void Game_OnPrefabInit_Postfix() {
			Instance?.InvokeAllProcess(RunAt.OnStartGame, null);
		}

		private static void Initialize_Prefix() {
			Instance?.InvokeAllProcess(RunAt.BeforeDbInit, null);
		}

		private static void Initialize_Postfix() {
			Instance?.InvokeAllProcess(RunAt.AfterDbInit, null);
		}

		private static void Instance_Postfix() {
			bool load = false;
			if (Instance != null)
				lock (VERSION) {
					if (!afterModsLoaded)
						load = afterModsLoaded = true;
				}
			if (load)
				Instance.InvokeAllProcess(RunAt.AfterLayerableLoad, null);
		}

		private static void MainMenu_OnSpawn_Postfix() {
			Instance?.InvokeAllProcess(RunAt.InMainMenu, null);
		}

		public override Version Version => VERSION;

		/// <summary>
		/// The Harmony instance to use for patching.
		/// </summary>
		private readonly Harmony harmony;

		/// <summary>
		/// Patches and delegates to be run at specific points in the runtime. Put the kibosh
		/// on patching Db.Initialize()!
		/// </summary>
		private readonly IDictionary<uint, PrivateRunList> patches;

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
		/// Creates a patch manager to execute patches at specific times.
		/// 
		/// Create this instance in OnLoad() and use RegisterPatchClass to register a
		/// patch class.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		public PPatchManager(Harmony harmony) {
			if (harmony == null) {
				PUtil.LogWarning("Use the Harmony instance from OnLoad to create PPatchManager");
				harmony = new Harmony("PLib.PostLoad." + Assembly.GetExecutingAssembly().
					GetNameSafe());
			}
			this.harmony = harmony;
			patches = new Dictionary<uint, PrivateRunList>(8);
			InstanceData = patches;
		}

		/// <summary>
		/// Schedules a patch method instance to be run.
		/// </summary>
		/// <param name="when">When to run the patch.</param>
		/// <param name="instance">The patch method instance to run.</param>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		private void AddHandler(uint when, IPatchMethodInstance instance) {
			if (!patches.TryGetValue(when, out PrivateRunList atTime))
				patches.Add(when, atTime = new List<IPatchMethodInstance>(16));
			atTime.Add(instance);
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			// Db
			plibInstance.Patch(typeof(Db), nameof(Db.Initialize), prefix: PatchMethod(nameof(
				Initialize_Prefix)), postfix: PatchMethod(nameof(Initialize_Postfix)));

			// Game
			plibInstance.Patch(typeof(Game), "DestroyInstances", postfix: PatchMethod(nameof(
				Game_DestroyInstances_Postfix)));
			plibInstance.Patch(typeof(Game), "OnPrefabInit", postfix: PatchMethod(nameof(
				Game_OnPrefabInit_Postfix)));

			// GlobalResources
			plibInstance.Patch(typeof(GlobalResources), "Instance", postfix:
				PatchMethod(nameof(Instance_Postfix)));

			// MainMenu
			plibInstance.Patch(typeof(MainMenu), "OnSpawn", postfix: PatchMethod(
				nameof(MainMenu_OnSpawn_Postfix)));
		}

		public override void PostInitialize(Harmony plibInstance) {
			InvokeAllProcess(RunAt.AfterModsLoad, null);
		}

		public override void Process(uint when, object _) {
			if (patches.TryGetValue(when, out PrivateRunList atTime) && atTime != null &&
					atTime.Count > 0) {
				string stage = RunAt.ToString(when);
#if DEBUG
				PRegistry.LogPatchDebug("Executing {0:D} handler(s) from {1} for stage {2}".F(
					atTime.Count, Assembly.GetExecutingAssembly().GetNameSafe() ?? "?", stage));
#endif
				foreach (var patch in atTime)
					try {
						patch.Run(harmony);
					} catch (TargetInvocationException e) {
						// Use the inner exception
						PUtil.LogError("Error running patches for stage " + stage + ":");
						PUtil.LogException(e.GetBaseException());
					} catch (Exception e) {
						// Say which mod's postload crashed
						PUtil.LogError("Error running patches for stage " + stage + ":");
						PUtil.LogException(e);
					}
			}
		}

		/// <summary>
		/// Registers a single patch to be run by Patch Manager. Obviously, the patch must be
		/// registered before the time that it is used.
		/// </summary>
		/// <param name="when">The time when the method should be run.</param>
		/// <param name="patch">The patch to execute.</param>
		public void RegisterPatch(uint when, IPatchMethodInstance patch) {
			RegisterForForwarding();
			if (patch == null)
				throw new ArgumentNullException(nameof(patch));
			if (when == RunAt.Immediately)
				// Now now now!
				patch.Run(harmony);
			else
				AddHandler(when, patch);
		}

		/// <summary>
		/// Registers a class containing methods for [PLibPatch] and [PLibMethod] handlers.
		/// All methods, public and private, of the type will be searched for annotations.
		/// However, nested and derived types will not be searched, nor will inherited methods.
		/// 
		/// This method cannot be used to register a class from another mod, as the annotations
		/// on those methods would have a different assembly qualified name and would thus
		/// not be recognized.
		/// </summary>
		/// <param name="type">The type to register.</param>
		/// <param name="harmony">The Harmony instance to use for immediate patches. Use
		/// the instance provided from UserMod2.OnLoad().</param>
		public void RegisterPatchClass(Type type) {
			int count = 0;
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			RegisterForForwarding();
			foreach (var method in type.GetMethods(FLAGS | BindingFlags.Static))
				foreach (var attrib in method.GetCustomAttributes(true))
					if (attrib is IPLibAnnotation pm) {
						var when = pm.Runtime;
						var instance = pm.CreateInstance(method);
						if (when == RunAt.Immediately)
							// Now now now!
							instance.Run(harmony);
						else
							AddHandler(pm.Runtime, instance);
						count++;
					}
			if (count > 0)
				PRegistry.LogPatchDebug("Registered {0:D} handler(s) for {1}".F(count,
					Assembly.GetCallingAssembly().GetNameSafe() ?? "?"));
			else
				PRegistry.LogPatchWarning("RegisterPatchClass could not find any handlers!");
		}
	}
}
