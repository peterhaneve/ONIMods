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

namespace PeterHan.PLib.Datafiles {
	/// <summary>
	/// Handles codex entries for mods by automatically loading yaml
	/// entries and subentries for critters and plants from the codex
	/// folder in their mod directories.
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

		private static void RegisterEntry(Assembly modAssembly, string lockKey, string tableKey, string entryPath, string debugLine) {
			// Store the path to the creatures folder on disk for use in loading codex entries
			string dir = Options.POptions.GetModDir(modAssembly);
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<List<string>>(tableKey);
				if (table == null)
					PSharedData.PutData(tableKey, table = new
						List<string>(8));
#if DEBUG
				PUtil.LogDebug(debugLine.F(dir));
#endif
				table.Add(Path.Combine(dir, entryPath));
			}
		}

		/// <summary>
		/// Loads critter codex entries for the calling mod.
		/// </summary>
		public static void RegisterCreatures() {
			var assembly = Assembly.GetCallingAssembly();
			RegisterEntry(assembly, PRegistry.KEY_CODEX_CREATURES_LOCK, PRegistry.KEY_CODEX_CREATURES_TABLE,
				CREATURES_DIR, "Registered codex creatures directory: {0}");
		}

		/// <summary>
		/// Loads plant codex entries for the calling mod.
		/// </summary>
		public static void RegisterPlants() {
			var assembly = Assembly.GetCallingAssembly();
			RegisterEntry(assembly, PRegistry.KEY_CODEX_PLANTS_LOCK, PRegistry.KEY_CODEX_PLANTS_TABLE,
				PLANTS_DIR, "Registered codex plants directory: {0}");
		}

		/// <summary>
		/// Loads all codex entries for all mods reigstered to the lib.
		/// </summary>
		/// <param name="lockKey">Key for shared data lock.</param>
		/// <param name="tableKey">Key for shared data table.</param>
		/// <param name="category">The codex category under which these data entries should be loaded.</param>
		/// <returns>The list of entries that were loaded.</returns>
		private static List<CodexEntry> LoadEntries(string lockKey, string tableKey, string category) {
			List<CodexEntry> entries = new List<CodexEntry>();
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<List<string>>(tableKey);
				if (table == null)
					return entries;
				foreach (string dir in table) {
#if DEBUG
					PUtil.LogDebug("Loaded codex entries from directory: {0}".F(dir));
#endif
					string[] strArray = new string[0];
					try {
						strArray = Directory.GetFiles(dir, CODEX_FILES);
					}
					catch (UnauthorizedAccessException ex) {
						PUtil.LogExcWarn(ex);
					}
					foreach (string str in strArray) {
						try {
							string filename = str;
							YamlIO.ErrorHandler fMgCache0 = new YamlIO.ErrorHandler(PUtil.YamlParseErrorCB);
							List<Tuple<string, Type>> widgetTagMappings = Traverse.Create(typeof(CodexCache)).Field("widgetTagMappings").GetValue<List<Tuple<string, Type>>>();
							CodexEntry codexEntry = YamlIO.LoadFile<CodexEntry>(filename, fMgCache0, widgetTagMappings);
							if (codexEntry != null) {
								codexEntry.category = category;
								entries.Add(codexEntry);
							}
						}
						catch (Exception ex) {
							PUtil.LogException(ex);
						}
					}
				}
			}
			return entries;
		}

		internal static List<CodexEntry> LoadCreaturesEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE, CREATURES_CATEGORY);
		}

		internal static List<CodexEntry> LoadPlantsEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE, PLANTS_CATEGORY);
		}

		/// <summary>
		/// Loads all codex subentries for all mods reigstered to the lib.
		/// </summary>
		/// <param name="lockKey">Key for shared data lock.</param>
		/// <param name="tableKey">Key for shared data table.</param>
		/// <returns>The list of subentries that were loaded.</returns>
		private static List<SubEntry> LoadSubEntries(string lockKey, string tableKey) {
			List<SubEntry> entries = new List<SubEntry>();
			lock (PSharedData.GetLock(lockKey)) {
				var table = PSharedData.GetData<List<string>>(tableKey);
				if (table == null)
					return entries;
				foreach (string dir in table) {
#if DEBUG
					PUtil.LogDebug("Loaded codex sub entries from directory: {0}".F(dir));
#endif
					string[] strArray = new string[0];
					try {
						strArray = Directory.GetFiles(dir, CODEX_FILES, SearchOption.AllDirectories);
					}
					catch (UnauthorizedAccessException ex) {
						PUtil.LogExcWarn(ex);
					}
					foreach (string str in strArray) {
						try {
							string filename = str;
							YamlIO.ErrorHandler fMgCache1 = new YamlIO.ErrorHandler(PUtil.YamlParseErrorCB);
							List<Tuple<string, Type>> widgetTagMappings = Traverse.Create(typeof(CodexCache)).Field("widgetTagMappings").GetValue<List<Tuple<string, Type>>>();
							SubEntry subEntry = YamlIO.LoadFile<SubEntry>(filename, fMgCache1, widgetTagMappings);
							if (entries != null)
								entries.Add(subEntry);
						} catch (Exception ex) {
							PUtil.LogException(ex);
						}
					}
				}
			}
			return entries;
		}

		internal static List<SubEntry> LoadCreaturesSubEntries() {
			return LoadSubEntries(PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE);
		}

		internal static List<SubEntry> LoadPlantsSubEntries() {
			return LoadSubEntries(PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE);
		}
	}
}
