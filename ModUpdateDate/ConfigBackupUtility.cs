/*
 * Copyright 2021 Peter Han
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

using Ionic.Zip;
using Klei;
using KMod;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Any mod update currently erases configs. Go above and beyond, and back up (within
	/// reasonable limits) any configs in the mod folder.
	/// </summary>
	public sealed class ConfigBackupUtility {
		/// <summary>
		/// Only file names matching this expression will be backed up.
		/// </summary>
		private static readonly Regex CONFIG_FILES = new Regex("^.+(.txt|.json|.xml)$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		/// <summary>
		/// The maximum number of files to back up. Only counts files not already present in
		/// the new mod package.
		/// </summary>
		private const int MAX_CONFIGS_TO_SAVE = 64;

		/// <summary>
		/// The maximum size in bytes of a file to back up.
		/// </summary>
		private const long MAX_SIZE_PER_FILE = 100 * 1024;

		/// <summary>
		/// The mod folder to backup.
		/// </summary>
		private readonly string modFolder;

		/// <summary>
		/// The path to the original ZIP file containing the mod download.
		/// </summary>
		private readonly string oldFilePath;

		/// <summary>
		/// The path to a temporary data file which can be used to store the WIP backup zip.
		/// </summary>
		private readonly string tempFilePath;

		public ConfigBackupUtility(Mod mod, string oldFilePath, string tempFilePath) {
			if (mod == null)
				throw new ArgumentNullException("mod");
			this.oldFilePath = oldFilePath ?? throw new ArgumentNullException("oldFilePath");
			this.tempFilePath = tempFilePath ?? throw new ArgumentNullException("tempFilePath");
			modFolder = mod.label.install_path;
			if (string.IsNullOrEmpty(modFolder))
				throw new ArgumentException("Mod " + mod.label.title + " has no local path");
		}

		/// <summary>
		/// Commits the update from the temporary file path to the existing file path.
		/// </summary>
		public void CommitUpdate() {
			try {
				File.Copy(tempFilePath, oldFilePath, true);
				File.Delete(tempFilePath);
			} catch (SystemException e) {
				PUtil.LogWarning("Unable to commit config backup:");
				PUtil.LogExcWarn(e);
			}
		}

		/// <summary>
		/// Copies data from the file system and source zip file to the new combined backup
		/// zip file.
		/// </summary>
		/// <param name="src">The source mod data with no configs.</param>
		/// <param name="dst">The temporary file destination.</param>
		/// <param name="toCopy">The files to copy from the source.</param>
		/// <param name="toAdd">The files to add from the current mod directory.</param>
		private void CopyFiles(ZipFile src, ZipFile dst, ISet<string> toCopy,
				IDictionary<string, string> toAdd) {
			foreach (var entry in src) {
#if DEBUG
				PUtil.LogDebug("ConfigBackupUtility add existing file " + entry.FileName);
#endif
				dst.AddEntry(entry.FileName, (name, stream) => src[name].Extract(stream));
			}
			foreach (var pair in toAdd) {
				// Avoid capturing the wrong pair value
				string target = pair.Value, entryName = pair.Key;
				if (!dst.ContainsEntry(entryName))
					dst.AddEntry(entryName, (name, os) => CopyFromModFolder(name, target, os));
			}
		}

		/// <summary>
		/// Copies mod data from the mod folder to the backup ZIP.
		/// </summary>
		/// <param name="name">The entry name being copied.</param>
		/// <param name="file">The existing file to copy.</param>
		/// <param name="os">The ZIP stream where the data will be stored.</param>
		private void CopyFromModFolder(string name, string file, Stream os) {
			// Copy the content from the file system
			using (var ins = new FileStream(file, FileMode.Open, FileAccess.Read)) {
#if DEBUG
				PUtil.LogDebug("ConfigBackupUtility copy " + file + " to " + name);
#endif
				ins.CopyTo(os);
				ins.Close();
			}
		}

		/// <summary>
		/// Creates a ZIP file of the old mod contents merged with the saved configs at
		/// tempFilePath.
		/// </summary>
		/// <param name="copied">The number of configuration files backed up.</param>
		/// <returns>true if successful, or false if an I/O error occurred.</returns>
		public bool CreateMergedPackage(out int copied) {
			bool ok = false;
			var toCopy = HashSetPool<string, ConfigBackupUtility>.Allocate();
			var toAdd = DictionaryPool<string, string, ConfigBackupUtility>.Allocate();
			// Open old ZIP file
			copied = 0;
			try {
				using (var src = new ZipFile(oldFilePath)) {
					foreach (var entry in src)
						// Klei normalizes the file names to forward slashes
						toCopy.Add(FileSystem.Normalize(entry.FileName));
					FindFilesToCopy(toCopy, toAdd);
					using (var dst = new ZipFile(tempFilePath)) {
						CopyFiles(src, dst, toCopy, toAdd);
						dst.Save();
						copied = toAdd.Count;
						PUtil.LogDebug("Config backup for {0} copied {1:D} files".F(modFolder,
							copied));
					}
				}
				ok = true;
			} catch (IOException e) {
				PUtil.LogWarning("Unable to backup mod configs:");
				PUtil.LogExcWarn(e);
			} catch (UnauthorizedAccessException e) {
				PUtil.LogWarning("Unable to backup mod configs:");
				PUtil.LogExcWarn(e);
			}
			toAdd.Recycle();
			toCopy.Recycle();
			return ok;
		}

		/// <summary>
		/// Finds files to copy.
		/// </summary>
		/// <param name="ignore">The normalized paths not to back up.</param>
		/// <param name="toAdd">The files to be backed up, mapping normalized name to file system name.</param>
		private void FindFilesToCopy(ISet<string> ignore, IDictionary<string, string> toAdd) {
			int len = modFolder.Length;
			string normFolder = FileSystem.Normalize(modFolder);
			foreach (var file in Directory.GetFiles(modFolder, "*", SearchOption.
					AllDirectories)) {
				string normalFile = file;
				// Relativize the path and normalize according to Klei
				if (FileSystem.Normalize(normalFile).StartsWith(normFolder, StringComparison.
						InvariantCultureIgnoreCase))
					normalFile = normalFile.Substring(len);
				if (normalFile.Length > 0 && CONFIG_FILES.IsMatch(normalFile)) {
					normalFile = FileSystem.Normalize(normalFile);
					// Leading slash must be removed
					if (normalFile[0] == '/')
						normalFile = normalFile.Substring(1);
					// Limit file size, if unavailable ignore the file
					try {
						var info = new FileInfo(file);
						if (!ignore.Contains(normalFile) && info.Length <= MAX_SIZE_PER_FILE)
							toAdd[normalFile] = file;
					} catch (SystemException) { }
					// Limit chances for a denial of service
					if (toAdd.Count >= MAX_CONFIGS_TO_SAVE)
						break;
				}
			}
		}

		/// <summary>
		/// Rolls back the config backup, clearing out the temporary ZIP.
		/// </summary>
		public void RollbackUpdate() {
			try {
				File.Delete(tempFilePath);
			} catch (SystemException) { }
		}
	}
}
