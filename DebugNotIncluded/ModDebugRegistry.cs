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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Stores lots of extra information about loaded data to allow debugging.
	/// 
	/// This class is thread safe.
	/// </summary>
	public sealed class ModDebugRegistry {
		/// <summary>
		/// Retrieves the mod debug registry.
		/// </summary>
		public static ModDebugRegistry Instance { get; } = new ModDebugRegistry();

		/// <summary>
		/// Stores debug information about each mod. Keyed by label.
		/// </summary>
		private readonly ConcurrentDictionary<string, ModDebugInfo> debugInfo;

		/// <summary>
		/// Maps loaded assemblies to mods to determine which mod owns a type.
		/// </summary>
		private readonly ConcurrentDictionary<string, ModDebugInfo> modAssemblies;

		private ModDebugRegistry() {
			debugInfo = new ConcurrentDictionary<string, ModDebugInfo>(4, 256);
			modAssemblies = new ConcurrentDictionary<string, ModDebugInfo>(4, 256);
		}

		/// <summary>
		/// Returns the owning mod of the specified assembly. Returns null if no mod appears to
		/// own this assembly.
		/// </summary>
		/// <param name="assembly">The assembly to query.</param>
		/// <returns>The owning mod, or null if no owning mod could be determined.</returns>
		internal ModDebugInfo OwnerOfAssembly(Assembly assembly) {
			ModDebugInfo mod = null;
			if (assembly != null)
				modAssemblies.TryGetValue(assembly.FullName, out mod);
			return mod;
		}

		/// <summary>
		/// Returns the owning mod of the specified type. Returns null if no mod appears to
		/// own this type.
		/// </summary>
		/// <param name="type">The type to query.</param>
		/// <returns>The owning mod, or null if no owning mod could be determined.</returns>
		internal ModDebugInfo OwnerOfType(Type type) {
			return OwnerOfAssembly(type?.Assembly);
		}

		/// <summary>
		/// Retrieves the debug information for the specified mod, registering it if it has
		/// not been seen before.
		/// </summary>
		/// <param name="mod">The mod to retrieve.</param>
		/// <returns>The debug information about that mod.</returns>
		internal ModDebugInfo GetDebugInfo(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			return debugInfo.GetOrAdd(ModDebugInfo.GetIdentifier(mod), (_) =>
				new ModDebugInfo(mod));
		}

		/// <summary>
		/// Retrieves the debug information for the specified harmony instance ID.
		/// </summary>
		/// <param name="id">The harmony instance ID to retrieve.</param>
		/// <returns>The debug information about that mod, or null if no mod with that harmony
		/// ID was registered.</returns>
		internal ModDebugInfo GetDebugInfo(string id) {
			if (!debugInfo.TryGetValue(id, out ModDebugInfo info))
				info = null;
			return info;
		}

		/// <summary>
		/// Registers assemblies for their particular mod.
		/// </summary>
		internal void Populate(IReadOnlyCollection<Mod> allMods) {
			foreach (var mod in allMods) {
				var assemblies = mod.loaded_mod_data?.dlls;
				if (assemblies != null)
					foreach (var assembly in assemblies)
						RegisterAssembly(assembly, GetDebugInfo(mod));
			}
		}

		/// <summary>
		/// Registers an assembly as loaded by a particular mod.
		/// </summary>
		/// <param name="assembly">The assembly to register.</param>
		/// <param name="owner">The owning mod.</param>
		internal void RegisterAssembly(Assembly assembly, ModDebugInfo owner) {
			if (assembly == null)
				throw new ArgumentNullException(nameof(assembly));
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			string fullName = assembly.FullName;
			var oldMod = modAssemblies.GetOrAdd(fullName, owner);
			if (oldMod != owner) {
				// Possible if multiple mods include the same dependency DLL
				DebugLogger.LogDebug("Assembly \"{0}\" is used by multiple mods:", fullName);
				DebugLogger.LogDebug("First loaded by {0} (used), also loaded by {1} (ignored)",
					oldMod.ModName, owner.ModName);
			} else
				owner.ModAssemblies.Add(assembly);
		}
	}
}
