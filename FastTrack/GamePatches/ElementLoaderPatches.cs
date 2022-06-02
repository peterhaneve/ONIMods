/*
 * Copyright 2022 Peter Han
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

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to ElementLoader to skip a slow Enum.Parse in favor of just computing the hash.
	/// </summary>
	[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.FindElementByName))]
	public static class ElementLoader_FindElementByName_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before FindElementByName runs.
		/// </summary>
		internal static bool Prefix(string name, ref Element __result) {
			if (!ElementLoader.elementTable.TryGetValue(Hash.SDBMLower(name), out Element e))
				e = null;
			__result = e;
			return false;
		}
	}

	/// <summary>
	/// Applied to ElementLoader to speed up a linear search into a hashtable lookup.
	/// </summary>
	[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.GetElement))]
	public static class ElementLoader_GetElement_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before GetElement runs.
		/// </summary>
		internal static bool Prefix(Tag tag, ref Element __result) {
			if (!ElementLoader.elementTable.TryGetValue(tag.GetHash(), out Element element))
				element = null;
			__result = element;
			return false;
		}
	}

	/// <summary>
	/// Applied to ElementLoader to speed up a linear search into a hashtable lookup.
	/// </summary>
	[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.GetElementID))]
	public static class ElementLoader_GetElementID_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before GetElementID runs.
		/// </summary>
		internal static bool Prefix(Tag tag, ref SimHashes __result) {
			int hash = tag.GetHash();
			if (ElementLoader.elementTable.ContainsKey(hash))
				__result = (SimHashes)hash;
			else
				__result = SimHashes.Vacuum;
			return false;
		}
	}

	/// <summary>
	/// Applied to ElementLoader to speed up another linear search into a hashtable lookup.
	/// </summary>
	[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.GetElementIndex), typeof(
		SimHashes))]
	public static class ElementLoader_GetElementIndexHash_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before GetElementIndex runs.
		/// </summary>
		internal static bool Prefix(SimHashes hash, ref int __result) {
			int index = -1;
			if (ElementLoader.elementTable.TryGetValue((int)hash, out Element e))
				index = e.idx;
			__result = index;
			return false;
		}
	}

	/// <summary>
	/// Applied to ElementLoader to speed up yet another linear search into a hashtable lookup.
	/// </summary>
	[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.GetElementIndex), typeof(Tag))]
	public static class ElementLoader_GetElementIndexTag_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Applied before GetElementIndex runs.
		/// </summary>
		internal static bool Prefix(Tag element_tag, ref byte __result) {
			byte index = byte.MaxValue;
			if (ElementLoader.elementTable.TryGetValue(element_tag.GetHash(), out Element e))
				index = e.idx;
			__result = index;
			return false;
		}
	}
}
