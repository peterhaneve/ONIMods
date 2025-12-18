/*
 * Copyright 2025 Peter Han
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

using System.Collections.Concurrent;

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Stores a list of prefab IDs that can use forbidden items.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class AllowPrefabID : KMonoBehaviour {
		/// <summary>
		/// Stores the instance IDs that can use forbidden items.
		/// 
		/// A concurrent collection is required since sweepers run on a background task.
		/// </summary>
		private static readonly ConcurrentDictionary<int, bool> ALLOWED_FORBIDDEN =
			new ConcurrentDictionary<int, bool>(4, 64);
		
		/// <summary>
		/// Reports whether the prefab ID can use forbidden items.
		/// </summary>
		/// <param name="id">The ID to check.</param>
		/// <returns>true if it can use forbidden items, or false otherwise.</returns>
		public static bool CanUseForbidden(int id) {
			return ALLOWED_FORBIDDEN.ContainsKey(id);
		}

		/// <summary>
		/// Called when tags are changed.
		/// </summary>
		private static readonly EventSystem.IntraObjectHandler<AllowPrefabID> OnTagsChangedDelegate =
			new EventSystem.IntraObjectHandler<AllowPrefabID>(delegate(AllowPrefabID component, object _) {
			component.OnTagsChanged();
		});

#pragma warning disable CS0649
#pragma warning disable IDE0044
		[MyCmpReq]
		private KPrefabID prefabID;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		/// <summary>
		/// Clears out the list of allowed IDs when the game ends.
		/// </summary>
		internal static void Cleanup() {
			ALLOWED_FORBIDDEN.Clear();
		}

		public override void OnCleanUp() {
			Unsubscribe((int)GameHashes.TagsChanged, OnTagsChangedDelegate);
			// Remove it no matter what, the object is dead!
			ALLOWED_FORBIDDEN.TryRemove(prefabID.InstanceID, out _);
			base.OnCleanUp();
		}

		public override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.TagsChanged, OnTagsChangedDelegate);
		}

		/// <summary>
		/// Called when the tags of this object are changed.
		/// </summary>
		private void OnTagsChanged() {
			if (prefabID != null) {
				int id = prefabID.InstanceID;
				if (prefabID.HasTag(AllowForbiddenItems.AllowForbiddenUse))
					ALLOWED_FORBIDDEN.TryAdd(id, true);
				else
					ALLOWED_FORBIDDEN.TryRemove(id, out _);
			}
		}
	}
}
