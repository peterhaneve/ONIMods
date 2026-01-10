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
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Implements a basic automatic version check, using either Steam or an external website.
	/// 
	/// The version of the current mod is taken from the mod version attribute of the provided
	/// mod.
	/// </summary>
	public sealed class PVersionCheck : PForwardedComponent {
		/// <summary>
		/// The delegate type used when a background version check completes.
		/// </summary>
		/// <param name="result">The results of the check. If null, the check has failed,
		/// and the next version should be tried.</param>
		public delegate void OnVersionCheckComplete(ModVersionCheckResults result);

		/// <summary>
		/// Versions of PVersionCheck older than this version are broken and do not pass the
		/// linked list correctly.
		/// </summary>
		private static readonly Version WORKING_VERSION = new Version(4, 14, 0, 0);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PVersionCheck Instance { get; private set; }

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// Gets the reported version of the specified assembly.
		/// </summary>
		/// <param name="assembly">The assembly to check.</param>
		/// <returns>The assembly's file version, or if that is unset, its assembly version.</returns>
		private static string GetCurrentVersion(Assembly assembly) {
			string version = null;
			if (assembly != null) {
				version = assembly.GetFileVersion();
				if (string.IsNullOrEmpty(version))
					version = assembly.GetName().Version?.ToString();
			}
			return version;
		}

		/// <summary>
		/// Gets the current version of the mod. If the version is specified in mod_info.yaml,
		/// that version is reported. Otherwise, the assembly file version (and failing that,
		/// the assembly version) of the assembly defining the mod's first UserMod2 instance
		/// is reported.
		/// 
		/// This method will only work after mods have loaded.
		/// </summary>
		/// <param name="mod">The mod to check.</param>
		/// <returns>The current version of that mod.</returns>
		public static string GetCurrentVersion(KMod.Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			string version = mod.packagedModInfo?.version;
			if (string.IsNullOrEmpty(version)) {
				// Does it have UM2 instances?
				var instances = mod.loaded_mod_data?.userMod2Instances;
				var dlls = mod.loaded_mod_data?.dlls;
				if (instances != null)
					// Use first UserMod2
					foreach (var um2 in instances) {
						version = GetCurrentVersion(um2.Key);
						if (!string.IsNullOrEmpty(version))
							break;
					}
				else if (dlls != null && dlls.Count > 0)
					// Use first DLL
					foreach (var assembly in dlls) {
						version = GetCurrentVersion(assembly);
						if (!string.IsNullOrEmpty(version))
							break;
					}
				else
					// All methods of determining the version have failed
					version = "";
			}
			return version;
		}

		private static void MainMenu_OnSpawn_Postfix(MainMenu __instance) {
			Instance?.RunVersionCheck();
			if (__instance != null)
				__instance.gameObject.AddOrGet<ModOutdatedWarning>();
		}

		private static void ModsScreen_BuildDisplay_Postfix(System.Collections.IEnumerable
				___displayedMods) {
			// Must cast the type because ModsScreen.DisplayedMod is private
			if (Instance != null && ___displayedMods != null)
				foreach (var modEntry in ___displayedMods)
					Instance.AddWarningIfOutdated(modEntry);
		}

		/// <summary>
		/// The mods whose version will be checked.
		/// </summary>
		private readonly IDictionary<string, VersionCheckMethods> checkVersions;

		/// <summary>
		/// The number of mods that appear to be outdated.
		/// </summary>
		public int OutdatedMods {
			get {
				int outdated = 0;
				foreach (var mod in results)
					if (!mod.IsUpToDate) outdated++;
				return outdated;
			}
		}

		/// <summary>
		/// The location where the outcome of mod version checking will be stored.
		/// </summary>
		private readonly ICollection<ModVersionCheckResults> results;
		
		/// <summary>
		/// Stores results by the mod static ID. Only populated in the active instance.
		/// </summary>
		private readonly IDictionary<string, ModVersionCheckResults> resultsByMod;

		public override Version Version => VERSION;

		public PVersionCheck() {
			checkVersions = new Dictionary<string, VersionCheckMethods>(8);
			results = new List<ModVersionCheckResults>(8);
			resultsByMod = new Dictionary<string, ModVersionCheckResults>(32);
			InstanceData = results;
		}

		/// <summary>
		/// Adds a warning to the mods screen if a mod is outdated.
		/// </summary>
		/// <param name="modEntry">The mod entry to modify.</param>
		private void AddWarningIfOutdated(object modEntry) {
			int index = -1;
			var type = modEntry.GetType();
			if (PPatchTools.TryGetFieldValue(modEntry, "mod_index", out int intVal))
				index = intVal;
			var mods = Global.Instance.modManager?.mods;
			string id;
			if (PPatchTools.TryGetFieldValue(modEntry, "rect_transform", out RectTransform
					rowInstance) && mods != null && index >= 0 && index < mods.Count &&
					!string.IsNullOrEmpty(id = mods[index]?.staticID) && rowInstance.
					TryGetComponent(out HierarchyReferences hr) && resultsByMod.TryGetValue(id,
					out ModVersionCheckResults data) && data != null)
				// Version text is thankfully known, even if other mods have added buttons
				AddWarningIfOutdated(data, hr.GetReference<LocText>("Version"));
		}

		/// <summary>
		/// Adds a warning to a mod version label if it is outdated.
		/// </summary>
		/// <param name="data">The updated mod version.</param>
		/// <param name="versionText">The current mod version label.</param>
		private void AddWarningIfOutdated(ModVersionCheckResults data, LocText versionText) {
			GameObject go;
			if (versionText != null && (go = versionText.gameObject) != null && !data.
					IsUpToDate) {
				string text = versionText.text;
				if (string.IsNullOrEmpty(text))
					text = PLibStrings.OUTDATED_WARNING;
				else
					text = text + " " + PLibStrings.OUTDATED_WARNING;
				versionText.text = text;
				go.AddOrGet<ToolTip>().toolTip = string.Format(PLibStrings.OUTDATED_TOOLTIP,
					data.NewVersion ?? "");
			}
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;
			plibInstance.Patch(typeof(MainMenu), "OnSpawn", postfix: PatchMethod(nameof(
				MainMenu_OnSpawn_Postfix)));
			plibInstance.Patch(typeof(ModsScreen), "BuildDisplay", postfix: PatchMethod(
				nameof(ModsScreen_BuildDisplay_Postfix)));
		}

		public override void Process(uint operation, object args) {
			if (operation == 0 && args is System.Action runNext) {
				VersionCheckTask first = null, previous = null;
				results.Clear();
				foreach (var pair in checkVersions) {
					var mod = pair.Value;
#if DEBUG
					string staticID = pair.Key;
					PUtil.LogDebug("Checking version for mod {0}".F(staticID));
#endif
					foreach (var checker in mod.Methods) {
						var node = new VersionCheckTask(mod.ModToCheck, checker, results) {
							Next = runNext
						};
						if (previous != null)
							previous.Next = node.Run;
						if (first == null)
							first = node;
						previous = node;
					}
				}
				first?.Run();
			}
		}

		/// <summary>
		/// Registers the specified mod for automatic version checking. Mods will be registered
		/// using their static ID, so to avoid the default ID from being used instead, set this
		/// attribute in mod.yaml.
		/// 
		/// The same mod can be registered multiple times with different methods to check the
		/// mod versions. The methods will be attempted in order from first registered to last.
		/// However, the same mod must not be registered multiple times in different instances
		/// of PVersionCheck.
		/// </summary>
		/// <param name="mod">The mod instance to check.</param>
		/// <param name="checker">The method to use for checking the mod version.</param>
		public void Register(KMod.UserMod2 mod, IModVersionChecker checker) {
			var kmod = mod?.mod ?? throw new ArgumentNullException(nameof(mod));
			if (checker == null)
				throw new ArgumentNullException(nameof(checker));
			RegisterForForwarding();
			string staticID = kmod.staticID;
			if (!checkVersions.TryGetValue(staticID, out var checkers))
				checkVersions.Add(staticID, checkers = new VersionCheckMethods(kmod));
			checkers.Methods.Add(checker);
		}

		/// <summary>
		/// Reports the results of the version check.
		/// </summary>
		private void ReportResults() {
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			if (allMods != null) {
				var inst = ModOutdatedWarning.Instance;
				// Consolidate them through JSON roundtrip into results dictionary
				resultsByMod.Clear();
				foreach (var mod in allMods) {
					var modResults = mod.GetInstanceDataSerialized<ICollection<
						ModVersionCheckResults>>();
					if (modResults != null)
						foreach (var result in modResults) {
							string id = result.ModChecked;
							if (!resultsByMod.ContainsKey(id))
								resultsByMod[id] = result;
						}
				}
				results.Clear();
				foreach (var pair in resultsByMod)
					results.Add(pair.Value);
				// Attempt to update the main menu
				if (inst != null)
					inst.StartCoroutine(inst.UpdateTextThreaded());
			}
		}

		/// <summary>
		/// Starts the automatic version check for all mods.
		/// </summary>
		internal void RunVersionCheck() {
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			// See if Mod Updater triggered master disable
			if (!PRegistry.GetData<bool>("PLib.VersionCheck.ModUpdaterActive") &&
					allMods != null)
				new AllVersionCheckTask(allMods, this).Run();
		}

		/// <summary>
		/// Checks each mod's version one at a time to avoid saturating the network with
		/// generally nonessential traffic (in the case of yaml/json checkers).
		/// </summary>
		private sealed class AllVersionCheckTask {
			/// <summary>
			/// A list of actions that will check each version in turn.
			/// </summary>
			private readonly IList<PForwardedComponent> checkAllVersions;

			/// <summary>
			/// The current location in the list.
			/// </summary>
			private int index;

			/// <summary>
			/// Handles version check result reporting when complete.
			/// </summary>
			private readonly PVersionCheck parent;

			internal AllVersionCheckTask(IEnumerable<PForwardedComponent> allMods,
					PVersionCheck parent) {
				if (allMods == null)
					throw new ArgumentNullException(nameof(allMods));
				var modList = new List<PForwardedComponent>(allMods);
				// Due to bugs in older versions of PVersionCheck, try to run all of the
				// working ones first
				modList.Sort(new PComponentComparator());
				checkAllVersions = modList;
				index = 0;
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			}

			/// <summary>
			/// Runs all checks and fires the callback when complete.
			/// </summary>
			internal void Run() {
				int n = checkAllVersions.Count;
				bool allowReport = true;
				while (index < n) {
					var doCheck = checkAllVersions[index++];
					if (doCheck == null)
						PUtil.LogDebug("Invalid version checker reported by PForwardedComponent!");
					else if (doCheck.Version.CompareTo(WORKING_VERSION) < 0) {
#if DEBUG
						if (index < n)
							PUtil.LogWarning("Skipped {0:D} broken PLib version checks".
								F(n - index));
#endif
						// All remaining versions are bad
						parent.ReportResults();
						allowReport = false;
						break;
					} else {
						doCheck.Process(0, new System.Action(Run));
						allowReport = false;
						break;
					}
				}
				if (index >= n && allowReport)
					parent.ReportResults();
			}

			public override string ToString() {
				return "AllVersionCheckTask for {0:D} mods".F(checkAllVersions.Count);
			}
		}

		/// <summary>
		/// A placeholder class which stores all methods used to check a single mod.
		/// </summary>
		private sealed class VersionCheckMethods {
			/// <summary>
			/// The methods which will be used to check.
			/// </summary>
			internal IList<IModVersionChecker> Methods { get; }

			/// <summary>
			/// The mod whose version will be checked.
			/// </summary>
			internal KMod.Mod ModToCheck { get; }

			internal VersionCheckMethods(KMod.Mod mod) {
				Methods = new List<IModVersionChecker>(8);
				ModToCheck = mod ?? throw new ArgumentNullException(nameof(mod));
				PUtil.LogDebug("Registered mod ID {0} for automatic version checking".F(
					ModToCheck.staticID));
			}

			public override string ToString() {
				return ModToCheck.staticID;
			}
		}
	}
}
