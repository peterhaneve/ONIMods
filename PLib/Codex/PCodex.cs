using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Klei;
using Harmony;

namespace PeterHan.PLib.Codex {
	public sealed class PCodex {

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

		public static void RegisterCreatures() {
			var assembly = Assembly.GetCallingAssembly();
			RegisterEntry(assembly, PRegistry.KEY_CODEX_CREATURES_LOCK, PRegistry.KEY_CODEX_CREATURES_TABLE,
				"codex/Creatures", "Registered codex creatures directory: {0}");
		}

		public static void RegisterPlants() {
			var assembly = Assembly.GetCallingAssembly();
			RegisterEntry(assembly, PRegistry.KEY_CODEX_PLANTS_LOCK, PRegistry.KEY_CODEX_PLANTS_TABLE,
				"codex/Plants", "Registered codex plants directory: {0}");
		}

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
						strArray = Directory.GetFiles(dir, "*.yaml");
					}
					catch (UnauthorizedAccessException ex) {
						Debug.LogWarning(ex);
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
							DebugUtil.DevLogErrorFormat("CodexCache.CollectEntries failed to load [{0}]: {1}", str, ex.ToString());
						}
					}
				}
			}
			return entries;
		}

		internal static List<CodexEntry> LoadCreaturesEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_CREATURES_LOCK,
				PRegistry.KEY_CODEX_CREATURES_TABLE, "CREATURES");
		}

		internal static List<CodexEntry> LoadPlantsEntries() {
			return LoadEntries(PRegistry.KEY_CODEX_PLANTS_LOCK,
				PRegistry.KEY_CODEX_PLANTS_TABLE, "PLANTS");
		}

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
						strArray = Directory.GetFiles(dir, "*.yaml", SearchOption.AllDirectories);
					}
					catch (UnauthorizedAccessException ex) {
						Debug.LogWarning(ex);
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
							DebugUtil.DevLogErrorFormat("CodexCache.CollectSubEntries failed to load [{0}]: {1}", str, ex.ToString());
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
