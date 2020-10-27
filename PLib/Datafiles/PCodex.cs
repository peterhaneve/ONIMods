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
using Klei;
using Harmony;
using PeterHan.PLib.Detours;

using WidgetMappingList = System.Collections.Generic.List<Tuple<string, System.Type>>;

namespace PeterHan.PLib.Datafiles {
	/// <summary>
	/// Handles codex entries for mods by automatically loading YAML entries and subentries for
	/// critters and plants from the codex folder in their mod directories.
	/// 
	/// The layerable files loader in the stock game is broken, so this class is required to
	/// correctly load new codex entries.
	/// </summary>
	public static class PCodex {
		/// <summary>
		/// The subfolder from which critter codex entries are loaded.
		/// </summary>
		public const string CREATURES_DIR = "codex/Creatures";

		/// <summary>
		/// The subfolder from which plant codex entries are loaded.
		/// </summary>
		public const string PLANTS_DIR = "codex/Plants";

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
		/// Allow access to the private widget tag mappings field.
		/// </summary>
		private static readonly FieldInfo WIDGET_TAG_MAPPINGS = typeof(CodexCache).
			GetFieldSafe("widgetTagMappings", true);

		private static void RegisterEntry(Assembly modAssembly, string lockKey, string tableKey,
				string entryPath, string debugLine) {
			// Store the path to the creatures folder on disk for use in loading codex entries
			string dir = Options.POptions.GetModDir(modAssembly);
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<IList<string>>(tableKey);
				if (table == null)
					PSharedData.PutData(tableKey, table = new List<string>(8));
#if DEBUG
				PUtil.LogDebug(debugLine.F(dir));
#endif
				table.Add(Path.Combine(dir, entryPath));
			}
		}

		/// <summary>
		/// Registers the calling mod as having custom creature codex entries. The entries will
		/// be read from the mod directory in the "codex/Creatures" subfolder.
		/// </summary>
		public static void RegisterCreatures() {
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"RegisterCreatures!");
			}
			RegisterEntry(Assembly.GetCallingAssembly(), PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE, CREATURES_DIR,
				"Registered codex creatures directory: {0}");
		}

		/// <summary>
		/// Registers the calling mod as having custom plant codex entries. The entries will
		/// be read from the mod directory in the "codex/Plants" subfolder.
		/// </summary>
		public static void RegisterPlants() {
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"RegisterPlants!");
			}
			RegisterEntry(Assembly.GetCallingAssembly(), PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE, PLANTS_DIR,
				"Registered codex plants directory: {0}");
		}

		/// <summary>
		/// Loads all codex entries for all mods registered.
		/// </summary>
		/// <param name="lockKey">Key for shared data lock.</param>
		/// <param name="tableKey">Key for shared data table.</param>
		/// <param name="category">The codex category under which these data entries should be loaded.</param>
		/// <returns>The list of entries that were loaded.</returns>
		private static IList<CodexEntry> LoadEntries(string lockKey, string tableKey,
				string category) {
			var entries = new List<CodexEntry>(32);
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<IList<string>>(tableKey);
				if (table != null)
					foreach (string dir in table) {
#if DEBUG
						PUtil.LogDebug("Loaded codex entries from directory: {0}".F(dir));
#endif
						LoadFromDirectory(entries, dir, category);
					}
			}
			return entries;
		}

		/// <summary>
		/// Loads the mod creature entries.
		/// </summary>
		/// <returns>The list of all creature entries loaded from mods.</returns>
		internal static IList<CodexEntry> LoadCreaturesEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE, CREATURES_CATEGORY);
		}

		/// <summary>
		/// Loads codex entries from the specified directory.
		/// </summary>
		/// <param name="entries">The location where the data will be placed.</param>
		/// <param name="dir">The directory to load.</param>
		/// <param name="category">The category to assign to each entry thus loaded.</param>
		private static void LoadFromDirectory(ICollection<CodexEntry> entries, string dir,
				string category) {
			string[] codexFiles = new string[0];
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
				PUtil.LogWarning("Unable to load codex files: no tag mappings found");
			foreach (string str in codexFiles)
				try {
					string filename = str;
					var codexEntry = YamlIO.LoadFile<CodexEntry>(filename, PUtil.
						YamlParseErrorCB, widgetTagMappings);
					if (codexEntry != null) {
						codexEntry.category = category;
						entries.Add(codexEntry);
					}
				} catch (IOException ex) {
					PUtil.LogException(ex);
				} catch (InvalidDataException ex) {
					PUtil.LogException(ex);
				}
		}

		/// <summary>
		/// Loads codex entries from the specified directory.
		/// </summary>
		/// <param name="entries">The location where the data will be placed.</param>
		/// <param name="dir">The directory to load.</param>
		private static void LoadFromDirectory(ICollection<SubEntry> entries, string dir) {
			string[] codexFiles = new string[0];
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
				PUtil.LogWarning("Unable to load codex files: no tag mappings found");
			foreach (string filename in codexFiles)
				try {
					var subEntry = YamlIO.LoadFile<SubEntry>(filename, PUtil.YamlParseErrorCB,
						widgetTagMappings);
					if (entries != null)
						entries.Add(subEntry);
				} catch (IOException ex) {
					PUtil.LogException(ex);
				} catch (InvalidDataException ex) {
					PUtil.LogException(ex);
				}
#if DEBUG
			PUtil.LogDebug("Loaded codex sub entries from directory: {0}".F(dir));
#endif
		}

		/// <summary>
		/// Loads the mod plant entries.
		/// </summary>
		/// <returns>The list of all plant entries loaded from mods.</returns>
		internal static IList<CodexEntry> LoadPlantsEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE, PLANTS_CATEGORY);
		}

		/// <summary>
		/// Loads all codex subentries for all mods reigstered to the lib.
		/// </summary>
		/// <param name="lockKey">Key for shared data lock.</param>
		/// <param name="tableKey">Key for shared data table.</param>
		/// <returns>The list of subentries that were loaded.</returns>
		private static IList<SubEntry> LoadSubEntries(string lockKey, string tableKey) {
			var entries = new List<SubEntry>(32);
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<List<string>>(tableKey);
				if (table != null)
					foreach (string dir in table)
						LoadFromDirectory(entries, dir);
			}
			return entries;
		}

		/// <summary>
		/// Loads the mod creature sub-entries.
		/// </summary>
		/// <returns>The list of all creature sub-entries loaded from mods.</returns>
		internal static IList<SubEntry> LoadCreaturesSubEntries() {
			return LoadSubEntries(PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE);
		}

		/// <summary>
		/// Loads the mod plant sub-entries.
		/// </summary>
		/// <returns>The list of all plant sub-entries loaded from mods.</returns>
		internal static IList<SubEntry> LoadPlantsSubEntries() {
			return LoadSubEntries(PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE);
		}
	}
}
