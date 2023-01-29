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
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using ITState = ImageToggleState.State;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to DragMe to set it as always visible when dragged off screen.
	/// </summary>
	[HarmonyPatch(typeof(DragMe), nameof(DragMe.OnBeginDrag))]
	public static class DragMe_OnBeginDrag_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after OnBeginDrag runs.
		/// </summary>
		internal static void Postfix(DragMe __instance) {
			GameObject go;
			if (__instance != null && (go = __instance.gameObject) != null) {
				var vs = go.GetComponentInParent<VirtualScroll>();
				if (vs != null)
					vs.SetForceShow(go);
			}
		}
	}

	/// <summary>
	/// Applied to DragMe to clear it from always visible after dragging is complete.
	/// </summary>
	[HarmonyPatch(typeof(DragMe), nameof(DragMe.OnEndDrag))]
	public static class DragMe_OnEndDrag_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after OnEndDrag runs.
		/// </summary>
		internal static void Postfix(DragMe __instance) {
			GameObject go;
			if (__instance != null && (go = __instance.gameObject) != null) {
				var vs = go.GetComponentInParent<VirtualScroll>();
				if (vs != null)
					vs.ClearForceShow(go);
			}
		}
	}

	/// <summary>
	/// Applied to ModsScreen to update the scroll pane whenever the list changes.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.BuildDisplay))]
	public static class ModsScreen_BuildDisplay_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied before BuildDisplay runs.
		/// </summary>
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ModsScreen __instance, ref VirtualScroll __state) {
			var entryList = __instance.entryParent;
			if (entryList != null && entryList.TryGetComponent(out VirtualScroll vs)) {
				vs.OnBuild();
				__state = vs;
			} else
				__state = null;
		}

		/// <summary>
		/// Applied after BuildDisplay runs.
		/// </summary>
		[HarmonyPriority(Priority.VeryLow)]
		internal static void Postfix(VirtualScroll __state) {
			if (__state != null)
				__state.Rebuild();
		}
	}

	/// <summary>
	/// Applied to ModsScreen to set up listeners and state for virtual scroll.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.OnActivate))]
	public static class ModsScreen_OnActivate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after OnActivate runs.
		/// </summary>
		internal static void Postfix(ModsScreen __instance) {
			var entryList = __instance.entryParent;
			GameObject go;
			if (entryList != null && (go = entryList.gameObject) != null) {
				var vs = go.AddOrGet<VirtualScroll>();
				vs.freezeLayout = true;
				vs.Initialize();
			}
		}
	}
	
	/// <summary>
	/// Groups patches used for the Receptacle side screen (incubator, farm tile, pedestal...)
	/// </summary>
	public static class ReceptacleSideScreenPatches {
		/// <summary>
		/// Since ReceptacleSideScreen is a pseudo singleton (only one will be active at a
		/// time), static is safe here.
		/// </summary>
		private static bool initializing;

		/// <summary>
		/// Applied to ReceptacleSideScreen to force states to be valid on initialize (the
		/// entries are initialized with the available material, but their state is set to
		/// disabled).
		/// </summary>
		[HarmonyPatch(typeof(ReceptacleSideScreen), nameof(ReceptacleSideScreen.Initialize))]
		internal static class Initialize_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

			/// <summary>
			/// Applied before Initialize runs.
			/// </summary>
			internal static void Prefix(ReceptacleSideScreen __instance,
					ref VirtualScroll __state) {
				var obj = __instance.requestObjectList;
				if (obj != null && obj.TryGetComponent(out VirtualScroll vs)) {
					vs.OnBuild();
					__state = vs;
				} else
					__state = null;
				initializing = true;
			}

			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix(ReceptacleSideScreen __instance, VirtualScroll __state) {
				var entryList = __instance.requestObjectList;
				GameObject go;
				if (__state != null)
					__state.Rebuild();
				else if ((go = entryList.gameObject) != null) {
					// Add if first load
					var vs = go.AddComponent<VirtualScroll>();
					vs.freezeLayout = true;
					vs.Initialize();
				}
			}
		}

		/// <summary>
		/// Applied to ReceptacleSideScreen to properly rebuild the layout if the available
		/// item amounts change.
		/// </summary>
		[HarmonyPatch(typeof(ReceptacleSideScreen), nameof(ReceptacleSideScreen.
			UpdateAvailableAmounts))]
		internal static class UpdateAvailableAmounts_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every frame.
			/// </summary>
			private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(16);

			internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

			/// <summary>
			/// Applied before UpdateAvailableAmounts runs.
			/// </summary>
			internal static bool Prefix(ReceptacleSideScreen __instance, ref bool __result) {
				bool result = false, changed = false, hide = !DebugHandler.InstantBuildMode &&
					__instance.hideUndiscoveredEntities;
				var inst = DiscoveredResources.Instance;
				var selected = __instance.selectedEntityToggle;
				var obj = __instance.requestObjectList;
				var text = CACHED_BUILDER;
				VirtualScroll vs = null;
				if (obj != null)
					obj.TryGetComponent(out vs);
				foreach (var pair in __instance.depositObjectMap) {
					var key = pair.Key;
					var display = pair.Value;
					var go = key.gameObject;
					bool active = go.activeSelf;
					var tag = display.tag;
					// Hide undiscovered entities in some screens (like pedestal)
					if (hide && !inst.IsDiscovered(tag)) {
						if (active) {
							if (!changed && vs != null) {
								vs.OnBuild();
								changed = true;
							}
							go.SetActive(active = false);
						}
					} else if (!active) {
						if (!changed && vs != null) {
							vs.OnBuild();
							changed = true;
						}
						go.SetActive(active = true);
					}
					if (active) {
						var toggle = key.toggle;
						// Do not update amounts of inactive items
						float availableAmount = __instance.GetAvailableAmount(tag);
						if (!Mathf.Approximately(display.lastAmount, availableAmount)) {
							result = true;
							// Update display only if it actually changed
							display.lastAmount = availableAmount;
							text.Clear();
							availableAmount.ToRyuSoftString(text, 2);
							key.amount.SetText(text);
						}
						if (!__instance.ValidRotationForDeposit(display.direction) ||
								availableAmount <= 0.0f)
							// Disable items which cannot fit in this orientation or are
							// unavailable
							SetImageToggleState(__instance, toggle, selected != key ? ITState.
								Disabled : ITState.DisabledActive);
						else if (selected != key)
							SetImageToggleState(__instance, toggle, ITState.Inactive);
						else
							SetImageToggleState(__instance, toggle, ITState.Active);
					}
				}
				// Null was already checked
				if (changed)
					vs.Rebuild();
				__result = result;
				initializing = false;
				return false;
			}

			/// <summary>
			/// Sets the toggle state of a button only if it actually changed.
			/// </summary>
			/// <param name="instance">The side screen being updated.</param>
			/// <param name="toggle">The toggle to modify.</param>
			/// <param name="state">The state to apply.</param>
			private static void SetImageToggleState(ReceptacleSideScreen instance,
					KToggle toggle, ITState state) {
				if (toggle.TryGetComponent(out ImageToggleState its) && (initializing ||
						state != its.currentState)) {
					// SetState provides no feedback on whether the state actually changed
					var targetImage = toggle.gameObject.GetComponentInChildrenOnly<Image>();
					switch (state) {
					case ITState.Disabled:
						its.SetDisabled();
						targetImage.material = instance.desaturatedMaterial;
						break;
					case ITState.Inactive:
						its.SetInactive();
						targetImage.material = instance.defaultMaterial;
						break;
					case ITState.Active:
						its.SetActive();
						targetImage.material = instance.defaultMaterial;
						break;
					case ITState.DisabledActive:
						its.SetDisabledActive();
						targetImage.material = instance.desaturatedMaterial;
						break;
					}
				}
			}
		}
	}
}
