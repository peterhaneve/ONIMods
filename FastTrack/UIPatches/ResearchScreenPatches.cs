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
using System;
using PeterHan.PLib.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Manages movement of the Research screen.
	/// </summary>
	internal static class ResearchScreenMovementUpdater {
		/// <summary>
		/// The Steam Controller fields are not available on the WeGame or EGS versions.
		/// </summary>
		private static readonly bool HAS_STEAM = PPatchTools.GetTypeSafe(nameof(
			SteamInputInterpreter)) != null;

		/// <summary>
		/// The squared threshold in pixels where movement ends.
		/// </summary>
		private const float THRESHOLD_SQ = 4.0f;

		private static Vector2 ClampBack(ResearchScreen __instance, RectTransform rt,
				float zoom, Vector2 inertia, Vector2 anchorPos) {
			const float ZS = 250.0f;
			Vector2 contentSize = rt.rect.size, target = __instance.forceTargetPosition;
			float y = 0.0f, xMin = (-contentSize.x * 0.5f - ZS) * zoom, xMax = ZS * zoom,
				yMin = -ZS * zoom;
			if (__instance.TryGetComponent(out RectTransform irt))
				y = irt.rect.size.y;
			float yMax = (contentSize.y + ZS) * zoom - y;
			target.x = Mathf.Clamp(target.x, xMin, xMax);
			target.y = Mathf.Clamp(target.y, yMin, yMax);
			var deltaAnchor = new Vector2(Mathf.Clamp(anchorPos.x, xMin, xMax),
				Mathf.Clamp(anchorPos.y, yMin, yMax)) + inertia - anchorPos;
			__instance.forceTargetPosition = target;
			return deltaAnchor;
		}

		/// <summary>
		/// Handles the Steam Controller. Has its own method to reduce crashes.
		/// </summary>
		/// <param name="rate">The rate to move the camera.</param>
		/// <param name="panDelta">The location where the delta will be stored.</param>
		/// <returns>true if the game pad is the current controller, or false otherwise.</returns>
		private static bool SteamInputSupport(float rate, ref Vector2 panDelta) {
			bool anyDown = KInputManager.currentControllerIsGamepad;
			if (anyDown)
				panDelta = KInputManager.steamInputInterpreter.GetSteamCameraMovement() * rate;
			return anyDown;
		}

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Update(ResearchScreen instance) {
			if (instance.canvas.enabled && instance.scrollContent.TryGetComponent(
					out RectTransform rt)) {
				Vector2 anchorPos = rt.anchoredPosition, startPos = anchorPos, inertia =
					instance.dragInteria;
				var mousePos = KInputManager.GetMousePos();
				bool dragging = UpdateDrag(instance);
				float dt = Time.unscaledDeltaTime;
				// Update from user input
				float zoom = UpdateZoom(instance, mousePos, rt, dt, ref anchorPos);
				bool anyDown = UpdateKeyboard(instance, dt, ref anchorPos,
					out var keyDelta);
				if (dragging) {
					var inertiaDelta = (Vector2)(mousePos - instance.dragLastPosition);
					anchorPos += inertiaDelta;
					instance.dragLastPosition = mousePos;
					inertia = Vector2.ClampMagnitude(inertia + inertiaDelta, 400.0f);
				}
				inertia *= Math.Max(0.0f, 1.0f - dt * 4.0f);
				instance.dragInteria = inertia;
				// Slide view back in bounds if not dragging
				if (!dragging) {
					var deltaAnchor = ClampBack(instance, rt, zoom, inertia, anchorPos);
					if (anyDown) {
						// Zero out keyboard input vectors at edge
						anchorPos += deltaAnchor;
						if (deltaAnchor.x < 0f)
							keyDelta.x = Math.Min(0f, keyDelta.x);
						if (deltaAnchor.x > 0f)
							keyDelta.x = Math.Max(0f, keyDelta.x);
						if (deltaAnchor.y < 0f)
							keyDelta.y = Math.Min(0f, keyDelta.y);
						if (deltaAnchor.y > 0f)
							keyDelta.y = Math.Max(0f, keyDelta.y);
					} else
						anchorPos += deltaAnchor * instance.edgeClampFactor * dt;
				}
				instance.keyPanDelta = keyDelta;
				ZoomToTarget(instance, dt, anyDown || dragging, ref anchorPos);
				if (!Mathf.Approximately(anchorPos.x, startPos.x) || !Mathf.Approximately(
						anchorPos.y, startPos.y))
					rt.anchoredPosition = anchorPos;
			}
			return false;
		}

		/// <summary>
		/// Updates the drag state of the research screen.
		/// </summary>
		/// <param name="instance">The research screen instance to update.</param>
		/// <returns>true if the user is dragging the screen, or false otherwise.</returns>
		private static bool UpdateDrag(ResearchScreen instance) {
			bool dragging = instance.isDragging;
			if (dragging && !KInputManager.isFocused) {
				dragging = false;
				instance.isDragging = false;
				instance.draggingJustEnded = true;
			}
			return dragging;
		}

		/// <summary>
		/// Updates the keyboard input for panning the research screen.
		/// </summary>
		/// <param name="instance">The research screen to pan.</param>
		/// <param name="dt">The unscaled delta time since the last update.</param>
		/// <param name="anchorPos">The position of the tech tree to update.</param>
		/// <param name="delta">The rate at which the keyboard is moving the view.</param>
		/// <returns>true if any keyboard keys are down, or false otherwise.</returns>
		private static bool UpdateKeyboard(ResearchScreen instance, float dt,
				ref Vector2 anchorPos, out Vector2 delta) {
			float speed = instance.keyboardScrollSpeed, easing = instance.keyPanEasing;
			var panDelta = instance.keyPanDelta;
			bool anyDown = false;
			if (instance.panUp) {
				panDelta.y -= dt * speed;
				anyDown = true;
			} else if (instance.panDown) {
				panDelta.y += dt * speed;
				anyDown = true;
			}
			if (instance.panLeft) {
				panDelta.x += dt * speed;
				anyDown = true;
			} else if (instance.panRight) {
				panDelta.x -= dt * speed;
				anyDown = true;
			}
			// Steam Controller/Deck support
			if (HAS_STEAM)
				anyDown |= SteamInputSupport(dt * speed * -2.0f, ref panDelta);
			// Deceleration
			panDelta.x -= Mathf.Lerp(0f, panDelta.x, dt * easing);
			panDelta.y -= Mathf.Lerp(0f, panDelta.y, dt * easing);
			instance.keyPanDelta = panDelta;
			anchorPos += panDelta;
			delta = panDelta;
			return anyDown;
		}

		/// <summary>
		/// Updates the zoom state of the research screen.
		/// </summary>
		/// <param name="instance">The research screen instance to update.</param>
		/// <param name="mousePos">The current mouse position.</param>
		/// <param name="rt">The transform with the displayed tech tree.</param>
		/// <param name="dt">The unscaled delta time since the last update.</param>
		/// <param name="anchorPos">The position of the tech tree to update.</param>
		/// <returns>The current zoom level.</returns>
		private static float UpdateZoom(ResearchScreen instance, Vector3 mousePos,
				RectTransform rt, float dt, ref Vector2 anchorPos) {
			float zoom = instance.currentZoom, oldZoom = zoom;
			Vector2 target = mousePos;
			zoom = Mathf.Lerp(zoom, instance.targetZoom, Math.Min(0.9f, instance.
				effectiveZoomSpeed * dt));
			instance.currentZoom = zoom;
			if (instance.zoomCenterLock)
				target = new Vector2(0.5f * Screen.width, 0.5f * Screen.height);
			Vector2 before = zoom * rt.InverseTransformPoint(target);
			if (!Mathf.Approximately(zoom, oldZoom))
				rt.localScale = new Vector3(zoom, zoom, 1.0f);
			anchorPos += (Vector2)rt.InverseTransformPoint(target) * zoom - before;
			return zoom;
		}

		/// <summary>
		/// Zooms to the target coordinates if necessary.
		/// </summary>
		/// <param name="instance">The research screen instance to zoom.</param>
		/// <param name="dt">The unscaled delta time since the last update.</param>
		/// <param name="input">Whether user input is currently occurring.</param>
		/// <param name="anchorPos">The position of the tech tree to update.</param>
		private static void ZoomToTarget(ResearchScreen instance, float dt, bool input,
				ref Vector2 anchorPos) {
			Vector2 target = instance.forceTargetPosition, pos = anchorPos;
			if (instance.zoomingToTarget) {
				// Process automatic zoom in, cancel if user input occurs
				pos = Vector2.Lerp(pos, target, dt * 4.0f);
				if ((pos - target).sqrMagnitude < THRESHOLD_SQ || input)
					instance.zoomingToTarget = false;
				anchorPos = pos;
			}
		}
	}

	/// <summary>
	/// Applied to ResearchScreen to make its dreadfully slow Update method way faster.
	/// </summary>
	[HarmonyPatch(typeof(ResearchScreen), nameof(ResearchScreen.Update))]
	public static class ResearchScreen_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ResearchScreen __instance) {
			return ResearchScreenMovementUpdater.Update(__instance);
		}
	}

	/// <summary>
	/// Applied to ResearchScreen to update the canvas size only when it is shown.
	/// </summary>
	[HarmonyPatch(typeof(ResearchScreen), nameof(ResearchScreen.OnShow))]
	public static class ResearchScreen_OnShow_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Freezes all of the top level layouts in the children of the specified object.
		/// </summary>
		/// <param name="components">The object containing the children to freeze. Please do not freeze children IRL.</param>
		private static System.Collections.IEnumerator FreezeLayouts(Transform components) {
			yield return null;
			if (components != null) {
				int n = components.childCount;
				for (int i = 0; i < n; i++) {
					var child = components.GetChild(i);
					GameObject go;
					// Only handle active children
					if (child != null && (go = child.gameObject).activeSelf && go.
							TryGetComponent(out LayoutGroup realLayout)) {
						var frozenLayout = go.AddOrGet<LayoutElement>();
						frozenLayout.CopyFrom(realLayout);
						frozenLayout.layoutPriority = 100;
						frozenLayout.enabled = true;
						realLayout.enabled = false;
					}
				}
			}
		}

		/// <summary>
		/// Applied after OnShow runs.
		/// </summary>
		internal static void Postfix(ResearchScreen __instance, bool show) {
			var content = __instance.scrollContent;
			if (show && content != null && __instance.isActiveAndEnabled) {
				if (content.TryGetComponent(out KChildFitter cf))
					cf.FitSize();
				__instance.StartCoroutine(FreezeLayouts(content.transform));
			}
		}
	}
}
