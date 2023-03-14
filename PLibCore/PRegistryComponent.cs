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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// A custom component added to manage shared data between mods, especially instances of
	/// PForwardedComponent used by both PLib and other mods.
	/// </summary>
	internal sealed class PRegistryComponent : MonoBehaviour, IPLibRegistry {
		/// <summary>
		/// The Harmony instance name used when patching via PLib.
		/// </summary>
		internal const string PLIB_HARMONY = "PeterHan.PLib";

		/// <summary>
		/// A pointer to the active PLib registry.
		/// </summary>
		private static PRegistryComponent instance = null;

		/// <summary>
		/// true if the forwarded components have been instantiated, or false otherwise.
		/// </summary>
		private static bool instantiated = false;

		/// <summary>
		/// Applies the latest version of all forwarded components.
		/// </summary>
		private static void ApplyLatest() {
			bool apply = false;
			if (instance != null)
				lock (instance) {
					if (!instantiated)
						apply = instantiated = true;
				}
			if (apply)
				instance.Instantiate();
		}

		/// <summary>
		/// Stores shared mod data which needs single instance existence. Available to all
		/// PLib consumers through PLib API.
		/// </summary>
		public IDictionary<string, object> ModData { get; }

		/// <summary>
		/// The Harmony instance used by PLib patching.
		/// </summary>
		public Harmony PLibInstance { get; }

		/// <summary>
		/// The candidate components with versions, from multiple assemblies.
		/// </summary>
		private readonly ConcurrentDictionary<string, PVersionList> forwardedComponents;

		/// <summary>
		/// The components actually instantiated (latest version of each).
		/// </summary>
		private readonly ConcurrentDictionary<string, object> instantiatedComponents;

		/// <summary>
		/// The latest versions of each component.
		/// </summary>
		private readonly ConcurrentDictionary<string, PForwardedComponent> latestComponents;

		internal PRegistryComponent() {
			if (instance == null)
				instance = this;
			else {
#if DEBUG
				PRegistry.LogPatchWarning("Multiple PLocalRegistry created!");
#endif
			}
			ModData = new ConcurrentDictionary<string, object>(2, 64);
			forwardedComponents = new ConcurrentDictionary<string, PVersionList>(2, 32);
			instantiatedComponents = new ConcurrentDictionary<string, object>(2, 32);
			latestComponents = new ConcurrentDictionary<string, PForwardedComponent>(2, 32);
			PLibInstance = new Harmony(PLIB_HARMONY);
		}

		public void AddCandidateVersion(PForwardedComponent instance) {
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			AddCandidateVersion(instance.ID, instance);
		}

		/// <summary>
		/// Adds a remote or local forwarded component by ID.
		/// </summary>
		/// <param name="id">The real ID of the component.</param>
		/// <param name="instance">The candidate instance to add.</param>
		private void AddCandidateVersion(string id, PForwardedComponent instance) {
			var versions = forwardedComponents.GetOrAdd(id, (_) => new PVersionList());
			if (versions == null)
				PRegistry.LogPatchWarning("Missing version info for component type " + id);
			else {
				var list = versions.Components;
				bool first = list.Count < 1;
				list.Add(instance);
#if DEBUG
				PRegistry.LogPatchDebug("Candidate version of {0} from {1}".F(id, instance.
					GetOwningAssembly()));
#endif
				if (first)
					instance.Bootstrap(PLibInstance);
			}
		}

		/// <summary>
		/// Applies a bootstrapper patch which will complete forwarded component initialization
		/// before mods are post-loaded.
		/// </summary>
		internal void ApplyBootstrapper() {
			try {
				PLibInstance.Patch(typeof(KMod.Mod), nameof(KMod.Mod.PostLoad), prefix:
					new HarmonyMethod(typeof(PRegistryComponent), nameof(ApplyLatest)));
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			} catch (ArgumentException e) {
				PUtil.LogException(e);
			} catch (TypeLoadException e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Called from other mods to add a candidate version of a particular component.
		/// </summary>
		/// <param name="instance">The component to be added.</param>
		internal void DoAddCandidateVersion(object instance) {
			AddCandidateVersion(instance.GetType().FullName, new PRemoteComponent(instance));
		}

		/// <summary>
		/// Called from other mods to get a list of all components with the given ID.
		/// </summary>
		/// <param name="id">The component ID to retrieve.</param>
		/// <returns>The instantiated instance of that component, or null if no component by
		/// that name was found or ever registered.</returns>
		internal System.Collections.ICollection DoGetAllComponents(string id) {
			if (!forwardedComponents.TryGetValue(id, out PVersionList all))
				all = null;
			return all?.Components;
		}

		/// <summary>
		/// Called from other mods to get the instantiated version of a particular component.
		/// </summary>
		/// <param name="id">The component ID to retrieve.</param>
		/// <returns>The instantiated instance of that component, or null if no component by
		/// that name was found or successfully instantiated.</returns>
		internal object DoGetLatestVersion(string id) {
			if (!instantiatedComponents.TryGetValue(id, out object component))
				component = null;
			return component;
		}

		public IEnumerable<PForwardedComponent> GetAllComponents(string id) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			if (!forwardedComponents.TryGetValue(id, out PVersionList all))
				all = null;
			return all?.Components;
		}

		public PForwardedComponent GetLatestVersion(string id) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			if (!latestComponents.TryGetValue(id, out PForwardedComponent remoteComponent)) {
#if DEBUG
				PRegistry.LogPatchWarning("Unable to find a component matching: " + id);
#endif
				remoteComponent = null;
			}
			return remoteComponent;
		}

		public object GetSharedData(string id) {
			if (!forwardedComponents.TryGetValue(id, out PVersionList all))
				all = null;
			return all?.SharedData;
		}

		/// <summary>
		/// Goes through the forwarded components, and picks the latest version of each to
		/// instantiate.
		/// </summary>
		public void Instantiate() {
			foreach (var pair in forwardedComponents) {
				// Sort value by version
				var versions = pair.Value.Components;
				int n = versions.Count;
				if (n > 0) {
					string id = pair.Key;
					versions.Sort();
					var component = versions[n - 1];
					latestComponents.GetOrAdd(id, component);
#if DEBUG
					PRegistry.LogPatchDebug("Instantiating component {0} using version {1} from assembly {2}".F(
						id, component.Version, component.GetOwningAssembly().FullName));
#endif
					try {
						instantiatedComponents.GetOrAdd(id, component?.DoInitialize(
							PLibInstance));
					} catch (Exception e) {
						PRegistry.LogPatchWarning("Error when instantiating component " + id +
							":");
						PUtil.LogException(e);
					}
				}
			}
			// Post initialize for component compatibility
			foreach (var pair in latestComponents)
				try {
					pair.Value.PostInitialize(PLibInstance);
				} catch (Exception e) {
					PRegistry.LogPatchWarning("Error when instantiating component " +
						pair.Key + ":");
					PUtil.LogException(e);
				}
		}

		public void SetSharedData(string id, object data) {
			if (forwardedComponents.TryGetValue(id, out PVersionList all))
				all.SharedData = data;
		}

		public override string ToString() {
			return forwardedComponents.ToString();
		}
	}
}
