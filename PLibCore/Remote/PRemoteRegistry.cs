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

using System;
using System.Collections.Generic;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Transparently provides the functionality of PRegistry, while the actual instance is
	/// from another mod's bootstrapper.
	/// </summary>
	internal sealed class PRemoteRegistry : IPLibRegistry {
		/// <summary>
		/// The prototype used for delegates to remote GetAllComponents.
		/// </summary>
		private delegate System.Collections.ICollection GetAllComponentsDelegate(string id);

		/// <summary>
		/// The prototype used for delegates to remote GetLatestVersion and GetSharedData.
		/// </summary>
		private delegate object GetObjectDelegate(string id);

		/// <summary>
		/// The prototype used for delegates to remote SetSharedData.
		/// </summary>
		private delegate void SetObjectDelegate(string id, object value);

		/// <summary>
		/// Points to the local registry's version of AddCandidateVersion.
		/// </summary>
		private readonly Action<object> addCandidateVersion;

		/// <summary>
		/// Points to the local registry's version of GetAllComponents.
		/// </summary>
		private readonly GetAllComponentsDelegate getAllComponents;

		/// <summary>
		/// Points to the local registry's version of GetLatestVersion.
		/// </summary>
		private readonly GetObjectDelegate getLatestVersion;

		/// <summary>
		/// Points to the local registry's version of GetSharedData.
		/// </summary>
		private readonly GetObjectDelegate getSharedData;

		/// <summary>
		/// Points to the local registry's version of SetSharedData.
		/// </summary>
		private readonly SetObjectDelegate setSharedData;

		public IDictionary<string, object> ModData { get; private set; }

		/// <summary>
		/// The components actually instantiated (latest version of each).
		/// </summary>
		private readonly IDictionary<string, PForwardedComponent> remoteComponents;

		/// <summary>
		/// Creates a remote registry wrapping the target object.
		/// </summary>
		/// <param name="instance">The PRegistryComponent instance to wrap.</param>
		internal PRemoteRegistry(object instance) {
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			remoteComponents = new Dictionary<string, PForwardedComponent>(32);
			if (!PPatchTools.TryGetPropertyValue(instance, nameof(ModData), out
					IDictionary<string, object> modData))
				throw new ArgumentException("Remote instance missing ModData");
			ModData = modData;
			var type = instance.GetType();
			addCandidateVersion = type.CreateDelegate<Action<object>>(nameof(
				PRegistryComponent.DoAddCandidateVersion), instance, typeof(object));
			getAllComponents = type.CreateDelegate<GetAllComponentsDelegate>(nameof(
				PRegistryComponent.DoGetAllComponents), instance, typeof(string));
			getLatestVersion = type.CreateDelegate<GetObjectDelegate>(nameof(
				PRegistryComponent.DoGetLatestVersion), instance, typeof(string));
			if (addCandidateVersion == null || getLatestVersion == null ||
					getAllComponents == null)
				throw new ArgumentException("Remote instance missing candidate versions");
			getSharedData = type.CreateDelegate<GetObjectDelegate>(nameof(IPLibRegistry.
				GetSharedData), instance, typeof(string));
			setSharedData = type.CreateDelegate<SetObjectDelegate>(nameof(IPLibRegistry.
				SetSharedData), instance, typeof(string), typeof(object));
			if (getSharedData == null || setSharedData == null)
				throw new ArgumentException("Remote instance missing shared data");
		}

		public void AddCandidateVersion(PForwardedComponent instance) {
			addCandidateVersion.Invoke(instance);
		}

		public IEnumerable<PForwardedComponent> GetAllComponents(string id) {
			ICollection<PForwardedComponent> results = null;
			var all = getAllComponents.Invoke(id);
			if (all != null) {
				results = new List<PForwardedComponent>(all.Count);
				foreach (var component in all)
					if (component is PForwardedComponent local)
						results.Add(local);
					else
						results.Add(new PRemoteComponent(component));
			}
			return results;
		}

		public PForwardedComponent GetLatestVersion(string id) {
			if (!remoteComponents.TryGetValue(id, out PForwardedComponent remoteComponent)) {
				// Attempt to resolve it
				object instantiated = getLatestVersion.Invoke(id);
				if (instantiated == null) {
#if DEBUG
					PRegistry.LogPatchWarning("Unable to find a component matching: " + id);
#endif
					remoteComponent = null;
				} else if (instantiated is PForwardedComponent inThisMod)
					// Running the current version
					remoteComponent = inThisMod;
				else
					remoteComponent = new PRemoteComponent(instantiated);
				remoteComponents.Add(id, remoteComponent);
			}
			return remoteComponent;
		}

		public object GetSharedData(string id) {
			return getSharedData.Invoke(id);
		}

		public void SetSharedData(string id, object data) {
			setSharedData.Invoke(id, data);
		}
	}
}
