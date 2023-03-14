/*
 * Copyright 2020 Davis Cook
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Klei;
using PeterHan.PLib.Core;

using CodexDictionary = System.Collections.Generic.Dictionary<string, System.Collections.
	Generic.ISet<string>>;
using WidgetMappingList = System.Collections.Generic.List<Tuple<string, System.Type>>;

namespace PeterHan.PLib.Database {
	/// <summary>
	/// Handles codex entries for mods by automatically loading YAML entries and subentries for
	/// critters and plants from the codex folder in their mod directories.
	/// 
	/// The layerable files loader in the stock game is broken, so this class is required to
	/// correctly load new codex entries.
	/// </summary>
	public sealed class PCodexManager : PForwardedComponent {
		/// <summary>
		/// The subfolder from which critter codex entries are loaded.
		/// </summary>
		public const string CREATURES_DIR = "codex/Creatures";

		/// <summary>
		/// The subfolder from which plant codex entries are loaded.
		/// </summary>
		public const string PLANTS_DIR = "codex/Plants";
		
		/// <summary>
		/// The subfolder from which story trait codex entries are loaded.
		/// </summary>
		public const string STORY_DIR = "codex/StoryTraits";

		/// <summary>
		/// The file extension used for codex entry/subentries.
		/// </summary>
		public const string CODEX_FILES = "*.yaml";

		/// <summary>
		/// The codex category under which critter entries should go.
		/// </summary>
		public const string CREATURES_CATEGORY = "CREATURES";

		/// <summary>
		/// The codex category under which plant entries should go.
		/// </summary>
		public const string PLANTS_CATEGORY = "PLANTS";
		
		/// <summary>
		/// The codex category under which story trait entries should go.
		/// </summary>
		public const string STORY_CATEGORY = "STORYTRAITS";

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// Allow access to the private widget tag mappings field.
		/// Detouring sadly is not possible because CodexCache is a static class and cannot be
		/// a type parameter.
		/// </summary>
		private static readonly FieldInfo WIDGET_TAG_MAPPINGS = typeof(CodexCache).
			GetFieldSafe("widgetTagMappings", true);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PCodexManager Instance { get; private set; }

		public override Version Version => VERSION;

		/// <summary>
		/// Applied to CodexCache to collect dynamic codex entries from the file system.
		/// </summary>
		private static void CollectEntries_Postfix(string folder, List<CodexEntry> __result,
				string ___baseEntryPath) {
			if (Instance != null) {
				string path = string.IsNullOrEmpty(folder) ? ___baseEntryPath : Path.Combine(
					___baseEntryPath, folder);
				bool modified = false;
				if (path.EndsWith("Creatures")) {
					__result.AddRange(Instance.LoadEntries(CREATURES_CATEGORY));
					modified = true;
				}
				if (path.EndsWith("Plants")) {
					__result.AddRange(Instance.LoadEntries(PLANTS_CATEGORY));
					modified = true;
				}
				if (path.EndsWith("StoryTraits")) {
					__result.AddRange(Instance.LoadEntries(STORY_CATEGORY));
					modified = true;
				}
				if (modified) {
					foreach (var codexEntry in __result)
						// Fill in a default sort string if necessary
						if (string.IsNullOrEmpty(codexEntry.sortString))
							codexEntry.sortString = Strings.Get(codexEntry.title);
					__result.Sort((x, y) => string.Compare(x.sortString, y.sortString,
						StringComparison.CurrentCulture));
				}
			}
		}

		/// <summary>
		/// Applied to CodexCache to collect dynamic codex sub entries from the file system.
		/// </summary>
		private static void CollectSubEntries_Postfix(List<SubEntry> __result) {
			if (Instance != null) {
				int startSize = __result.Count;
				__result.AddRange(Instance.LoadSubEntries());
				if (__result.Count != startSize)
					__result.Sort((x, y) => string.Compare(x.title, y.title, StringComparison.
						CurrentCulture));
			}
		}

		/// <summary>
		/// Loads codex entries from the specified directory.
		/// </summary>
		/// <param name="entries">The location where the data will be placed.</param>
		/// <param name="dir">The directory to load.</param>
		/// <param name="category">The category to assign to each entry thus loaded.</param>
		private static void LoadFromDirectory(ICollection<CodexEntry> entries, string dir,
				string category) {
			string[] codexFiles = Array.Empty<string>();
			try {
				// List codex data files in the codex directory
				codexFiles = Directory.GetFiles(dir, CODEX_FILES);
			} catch (UnauthorizedAccessException ex) {
				PUtil.LogExcWarn(ex);
			} catch (IOException ex) {
				PUtil.LogExcWarn(ex);
			}
			var widgetTagMappings = WIDGET_TAG_MAPPINGS?.GetValue(null) as WidgetMappingList;
			if (widgetTagMappings == null)
				PDatabaseUtils.LogDatabaseWarning("Unable to load codex files: no tag mappings found");
			foreach (string filename in codexFiles)
				try {
					var codexEntry = YamlIO.LoadFile<CodexEntry>(filename, YamlParseErrorCB,
						widgetTagMappings);
					if (codexEntry != null) {
						codexEntry.category = category;
						entries?.Add(codexEntry);
					}
				} catch (IOException ex) {
					PDatabaseUtils.LogDatabaseWarning("Unable to load codex files from {0}:".
						F(dir));
					PUtil.LogExcWarn(ex);
				} catch (InvalidDataException ex) {
					PUtil.LogException(ex);
				}
#if DEBUG
			PDatabaseUtils.LogDatabaseDebug("Loaded codex entries from directory: {0}".F(dir));
#endif
		}

		/// <summary>
		/// Loads codex subentries from the specified directory.
		/// </summary>
		/// <param name="entries">The location where the data will be placed.</param>
		/// <param name="dir">The directory to load.</param>
		private static void LoadFromDirectory(ICollection<SubEntry> entries, string dir) {
			string[] codexFiles = Array.Empty<string>();
			try {
				// List codex data files in the codex directory
				codexFiles = Directory.GetFiles(dir, CODEX_FILES, SearchOption.
					AllDirectories);
			} catch (UnauthorizedAccessException ex) {
				PUtil.LogExcWarn(ex);
			} catch (IOException ex) {
				PUtil.LogExcWarn(ex);
			}
			var widgetTagMappings = WIDGET_TAG_MAPPINGS?.GetValue(null) as WidgetMappingList;
			if (widgetTagMappings == null)
				PDatabaseUtils.LogDatabaseWarning("Unable to load codex files: no tag mappings found");
			foreach (string filename in codexFiles)
				try {
					var subEntry = YamlIO.LoadFile<SubEntry>(filename, YamlParseErrorCB,
						widgetTagMappings);
					entries?.Add(subEntry);
				} catch (IOException ex) {
					PDatabaseUtils.LogDatabaseWarning("Unable to load codex files from {0}:".
						F(dir));
					PUtil.LogExcWarn(ex);
				} catch (InvalidDataException ex) {
					PUtil.LogException(ex);
				}
#if DEBUG
			PDatabaseUtils.LogDatabaseDebug("Loaded codex sub entries from directory: {0}".
				F(dir));
#endif
		}

		/// <summary>
		/// A callback function for the YAML parser to process errors that it throws.
		/// </summary>
		/// <param name="error">The YAML parsing error</param>
		internal static void YamlParseErrorCB(YamlIO.Error error, bool _) {
			throw new InvalidDataException(string.Format("{0} parse error in {1}\n{2}", error.
				severity, error.file.full_path, error.message), error.inner_exception);
		}

		/// <summary>
		/// The paths for creature codex entries.
		/// </summary>
		private readonly ISet<string> creaturePaths;

		/// <summary>
		/// The paths for plant codex entries.
		/// </summary>
		private readonly ISet<string> plantPaths;
		
		/// <summary>
		/// The paths for story trait codex entries.
		/// </summary>
		private readonly ISet<string> storyPaths;

		public PCodexManager() {
			creaturePaths = new HashSet<string>();
			plantPaths = new HashSet<string>();
			storyPaths = new HashSet<string>();
			// Data is a hacky but usable 2 item dictionary
			InstanceData = new CodexDictionary(4) {
				{ CREATURES_CATEGORY, creaturePaths },
				{ PLANTS_CATEGORY, plantPaths },
				{ STORY_CATEGORY, storyPaths }
			};
			PUtil.InitLibrary(false);
			PRegistry.Instance.AddCandidateVersion(this);
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			plibInstance.Patch(typeof(CodexCache), nameof(CodexCache.CollectEntries),
				postfix: PatchMethod(nameof(CollectEntries_Postfix)));
			plibInstance.Patch(typeof(CodexCache), nameof(CodexCache.CollectSubEntries),
				postfix: PatchMethod(nameof(CollectSubEntries_Postfix)));
		}

		/// <summary>
		/// Loads all codex entries for all mods registered.
		/// </summary>
		/// <param name="category">The codex category under which these data entries should be loaded.</param>
		/// <returns>The list of entries that were loaded.</returns>
		private IEnumerable<CodexEntry> LoadEntries(string category) {
			var entries = new List<CodexEntry>(32);
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			if (allMods != null)
				foreach (var mod in allMods) {
					var codex = mod?.GetInstanceData<CodexDictionary>();
					if (codex != null && codex.TryGetValue(category, out var dirs))
						foreach (var dir in dirs)
							LoadFromDirectory(entries, dir, category);
				}
			return entries;
		}

		/// <summary>
		/// Loads all codex subentries for all mods registered.
		/// </summary>
		/// <returns>The list of subentries that were loaded.</returns>
		private IEnumerable<SubEntry> LoadSubEntries() {
			var entries = new List<SubEntry>(32);
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			if (allMods != null)
				foreach (var mod in allMods) {
					var codex = mod?.GetInstanceData<CodexDictionary>();
					if (codex != null)
						// Lots of nested for, but required! (entryType should only have
						// 2 values, and usually only one dir per mod)
						foreach (var entryType in codex)
							foreach (var dir in entryType.Value)
								LoadFromDirectory(entries, dir);
				}
			return entries;
		}

		/// <summary>
		/// Registers the calling mod as having custom creature codex entries. The entries will
		/// be read from the mod directory in the "codex/Creatures" subfolder. If the argument
		/// is omitted, the calling assembly is registered.
		/// </summary>
		/// <param name="assembly">The assembly to register as having creatures.</param>
		public void RegisterCreatures(Assembly assembly = null) {
			if (assembly == null)
				assembly = Assembly.GetCallingAssembly();
			string dir = Path.Combine(PUtil.GetModPath(assembly), CREATURES_DIR);
			creaturePaths.Add(dir);
#if DEBUG
			PDatabaseUtils.LogDatabaseDebug("Registered codex creatures directory: {0}".
				F(dir));
#endif
		}

		/// <summary>
		/// Registers the calling mod as having custom plant codex entries. The entries will
		/// be read from the mod directory in the "codex/Plants" subfolder. If the argument
		/// is omitted, the calling assembly is registered.
		/// </summary>
		/// <param name="assembly">The assembly to register as having plants.</param>
		public void RegisterPlants(Assembly assembly = null) {
			if (assembly == null)
				assembly = Assembly.GetCallingAssembly();
			string dir = Path.Combine(PUtil.GetModPath(assembly), PLANTS_DIR);
			plantPaths.Add(dir);
#if DEBUG
			PDatabaseUtils.LogDatabaseDebug("Registered codex plants directory: {0}".F(dir));
#endif
		}

		/// <summary>
		/// Registers the calling mod as having custom story trait codex entries. The entries
		/// will be read from the mod directory in the "codex/StoryTraits" subfolder. If the
		/// argument is omitted, the calling assembly is registered.
		/// </summary>
		/// <param name="assembly">The assembly to register as having story traits.</param>
		public void RegisterStory(Assembly assembly = null) {
			if (assembly == null)
				assembly = Assembly.GetCallingAssembly();
			string dir = Path.Combine(PUtil.GetModPath(assembly), STORY_DIR);
			storyPaths.Add(dir);
#if DEBUG
			PDatabaseUtils.LogDatabaseDebug("Registered codex story traits directory: {0}".
				F(dir));
#endif
		}
	}
}
