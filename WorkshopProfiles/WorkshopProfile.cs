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

using KSerialization;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.WorkshopProfiles {
	/// <summary>
	/// A component which stores the list of Duplicants that can use this building.
	/// </summary>
	public class WorkshopProfile : KMonoBehaviour {
		/// <summary>
		/// Pre-emptively handles Duplicant deaths by immediately removing them from the
		/// ACL if present.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<WorkshopProfile> ON_DEATH =
			new EventSystem.IntraObjectHandler<WorkshopProfile>(OnDeathDelegate);

		/// <summary>
		/// Do not use Components.Cmps as HashSet is way way faster!
		/// </summary>
		internal static readonly ISet<WorkshopProfile> Cmps = new HashSet<WorkshopProfile>();

		/// <summary>
		/// Cleans up destroyed WorkshopProfile components from the component list.
		/// </summary>
		internal static void CleanupCmps() {
			var preserve = ListPool<WorkshopProfile, WorkshopProfile>.Allocate();
			foreach (var cmp in Cmps)
				if (cmp != null)
					preserve.Add(cmp);
			Cmps.Clear();
			foreach (var cmp in preserve)
				Cmps.Add(cmp);
			preserve.Recycle();
		}

		/// <summary>
		/// Called when a Duplicant dies.
		/// </summary>
		/// <param name="target">The profile where the Duplicant should be removed.</param>
		/// <param name="dead">The dead Duplicant's game object.</param>
		private static void OnDeathDelegate(WorkshopProfile target, object dead) {
			target.OnDeath(dead);
		}

		/// <summary>
		/// The Instance IDs of Duplicants that can use this workshop. Uses the ID so that
		/// the KPrefabID need not be serialized.
		/// 
		/// Note that Klei code uses Ref&lt;KPrefabID&gt;, but that class has no CompareTo,
		/// GetHashCode, or Equals, so it is uselessly slow and requires a linear lookup for
		/// include tests. KLEI PLEASE.
		/// </summary>
		[Serialize]
		[SerializeField]
		private HashSet<int> allowIDs;

		/// <summary>
		/// The cached hash code of this object. Determined at construction time so that it
		/// will not throw on UnityEngine.Object.GetHashCode if disposed but not ref-null.
		/// </summary>
		private int hc;

		/// <summary>
		/// Adds the Duplicant to the access list. If all Duplicants are already allowed
		/// (via the "public" option in the sidescreen), this method does nothing.
		/// </summary>
		/// <param name="id">The ID of the Duplicant to add.</param>
		public void AddDuplicant(int id) {
			allowIDs?.Add(id);
		}

		/// <summary>
		/// Allows all Duplicants to operate this building by setting it to Public.
		/// </summary>
		public void AllowAll() {
			allowIDs = null;
		}

		/// <summary>
		/// Removes all invalid (dead, deleted) Duplicants from the access list if it is not
		/// set to Public.
		/// </summary>
		public void CleanupAccess() {
			if (allowIDs != null) {
				var preserve = HashSetPool<int, WorkshopProfile>.Allocate();
				var intersection = ListPool<int, WorkshopProfile>.Allocate();
				// Add all living Duplicants
				foreach (var id in Components.LiveMinionIdentities.Items)
					if (id != null && id.TryGetComponent(out KPrefabID minionID))
						preserve.Add(minionID.InstanceID);
				// Remove all dead ones
				foreach (int id in allowIDs)
					if (preserve.Contains(id))
						intersection.Add(id);
				allowIDs.Clear();
				// Copy back since removing from active enumeration is an error
				foreach (var id in intersection)
					allowIDs.Add(id);
				intersection.Recycle();
				preserve.Recycle();
			}
		}

		/// <summary>
		/// Disallows all Duplicants from operating this building, clearing Public if it is set
		/// and removing all entries from the ACL otherwise.
		/// </summary>
		public void DisallowAll() {
			if (allowIDs == null)
				allowIDs = new HashSet<int>();
			allowIDs.Clear();
		}

		public override int GetHashCode() {
			return hc;
		}

		/// <summary>
		/// Checks to see if a Duplicant can operate this building.
		/// </summary>
		/// <param name="minionID">The Duplicant ID to check.</param>
		/// <returns>true if they can use the building, or false otherwise.</returns>
		public bool IsAllowed(int minionID) {
			return allowIDs == null || allowIDs.Contains(minionID);
		}

		/// <summary>
		/// Checks to see if all Duplicants can use the building by default, like when it is
		/// first created.
		/// </summary>
		/// <returns>true if public access is allowed, or false otherwise.</returns>
		public bool IsPublicAllowed() {
			return allowIDs == null;
		}

		protected override void OnCleanUp() {
			Cmps.Remove(this);
			Game.Instance.Unsubscribe((int)GameHashes.DuplicantDied, ON_DEATH);
			Unsubscribe((int)GameHashes.CopySettings, OnCopySettings);
			base.OnCleanUp();
		}

		/// <summary>
		/// Called when the building settings are copied.
		/// </summary>
		/// <param name="data">The GameObject with the source settings.</param>
		private void OnCopySettings(object data) {
			if (data is GameObject go && go != null && go.TryGetComponent(
					out WorkshopProfile other)) {
				if (other.IsPublicAllowed())
					AllowAll();
				else
					allowIDs = new HashSet<int>(other.allowIDs);
			}
		}

		/// <summary>
		/// Called when a Duplicant dies to remove them from the access list.
		/// </summary>
		/// <param name="minion">The Duplicant to remove.</param>
		private void OnDeath(object minion) {
			if (minion is GameObject go && go != null && go.TryGetComponent(
					out KPrefabID prefabID))
				RemoveDuplicant(prefabID.InstanceID);
		}

		protected override void OnSpawn() {
			hc = base.GetHashCode();
			base.OnSpawn();
			Game.Instance.Subscribe((int)GameHashes.DuplicantDied, ON_DEATH);
			Subscribe((int)GameHashes.CopySettings, OnCopySettings);
			Cmps.Add(this);
		}

		/// <summary>
		/// Removes the Duplicant from the access list. If all Duplicants are already allowed
		/// (via the "public" option in the sidescreen), this method does nothing.
		/// </summary>
		/// <param name="id">The ID of the Duplicant to remove.</param>
		public void RemoveDuplicant(int id) {
			allowIDs?.Remove(id);
		}
	}
}
