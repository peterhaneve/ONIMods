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
using PeterHan.PLib.Core;
using System.Reflection.Emit;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to AxialI to make the hash code method less likely to collide.
	/// </summary>
	[HarmonyPatch(typeof(AxialI), nameof(AxialI.GetHashCode))]
	public static class AxialI_GetHashCode_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Transpiles GetHashCode to replace (x ^ y) with (x ^ (y &lt;&lt; 16)).
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var r = typeof(AxialI).GetFieldSafe(nameof(AxialI.r), false);
			var q = typeof(AxialI).GetFieldSafe(nameof(AxialI.q), false);
			if (r != null && q != null) {
				// Load r
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, r);
				// Load q
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, q);
				// Shift left 16
				yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
				yield return new CodeInstruction(OpCodes.Shl);
				// Xor
				yield return new CodeInstruction(OpCodes.Xor);
				yield return new CodeInstruction(OpCodes.Ret);
			} else {
				PUtil.LogWarning("Unable to patch AxialI.GetHashCode");
				foreach (var instr in instructions)
					yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to ClusterMapHex to use the precompiled colors.
	/// </summary>
	[HarmonyPatch(typeof(ClusterMapHex), nameof(ClusterMapHex.SetRevealed))]
	public static class ClusterMapHex_SetRevealed_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ClusterMapReduce &&
			DlcManager.FeatureClusterSpaceEnabled();

		/// <summary>
		/// Sets the revealed status of a cluster map hex properly using the pre-stacked
		/// sprites.
		/// </summary>
		/// <param name="hex">The hex to modify.</param>
		/// <param name="level">The reveal level to apply.</param>
		internal static void SetRevealed(ClusterMapHex hex, ClusterRevealLevel level) {
			if (hex._revealLevel != level) {
				hex.peekedTile.gameObject.SetActive(level == ClusterRevealLevel.Peeked);
				ClusterMapScreenPatches.SetColors(hex, level);
				hex._revealLevel = level;
			}
		}

		/// <summary>
		/// Applied before SetRevealed runs.
		/// </summary>
		internal static bool Prefix(ClusterMapHex __instance, ClusterRevealLevel level) {
			SetRevealed(__instance, level);
			return false;
		}
	}

	/// <summary>
	/// Applied to ClusterMapHex to use the precompiled colors.
	/// </summary>
	[HarmonyPatch(typeof(ClusterMapHex), nameof(ClusterMapHex.UpdateHoverColors))]
	public static class ClusterMapHex_UpdateHoverColors_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ClusterMapReduce &&
			DlcManager.FeatureClusterSpaceEnabled();

		/// <summary>
		/// Applied before UpdateHoverColors runs.
		/// </summary>
		internal static bool Prefix(ClusterMapHex __instance, bool validDestination) {
			ClusterMapScreenPatches.SetHover(__instance, validDestination);
			return false;
		}
	}

	/// <summary>
	/// Groups the patches to the Spaced Out starmap screen.
	/// </summary>
	public static class ClusterMapScreenPatches {
		/// <summary>
		/// Accent color on the (destroyed) border.
		/// </summary>
		private static readonly Color BORDER_ACCENT = new Color(0.541f, 0.282f, 0.627f,
			0.102f);
		
		/// <summary>
		/// The colors used for fog of war states (premixed).
		/// </summary>
		private static readonly Color[] COLORS_FOG = new Color[5];

		/// <summary>
		/// The colors used for non fog of war states (premixed).
		/// </summary>
		private static readonly Color[] COLORS_NORMAL = new Color[5];

		/// <summary>
		/// Non-hover color of a selected cell.
		/// </summary>
		private static readonly Color FILL_SELECTED = new Color(1.0f, 0.454999983f,
			0.833294034f, 0.2509804f);

		/// <summary>
		/// Non-hover color of an orbit highlight cell.
		/// </summary>
		private static readonly Color FILL_ORBIT = new Color(0.192239255f, 0.9056604f,
			0.5334042f, 0.117647059f);

		/// <summary>
		/// Non-hover color of an idle border. All hover borders are the same color.
		/// </summary>
		private static readonly Color BORDER_IDLE = new Color(1.0f, 1.0f, 1.0f, 0.101960786f);

		/// <summary>
		/// Non-hover color of a selected cell.
		/// </summary>
		private static readonly Color BORDER_SELECTED = new Color(1.0f, 0.454902f, 0.8313726f,
			1.0f);

		/// <summary>
		/// Non-hover color of an orbit highlight cell.
		/// </summary>
		private static readonly Color BORDER_ORBIT = new Color(0.192156866f, 0.905882359f,
			0.533333361f, 0.156862751f);

		/// <summary>
		/// Fog of war overlay color.
		/// </summary>
		private static readonly Color FOG_OF_WAR = new Color(0.0f, 0.0f, 0.0f, 0.859f);

		// Fully qualified paths to the assembly resources
		public const string HEX_BORDER = "PeterHan.FastTrack.images.hex_border.png";

		public const string HEX_FILL = "PeterHan.FastTrack.images.hex_fill.png";

		public const string HEX_REVEAL = "PeterHan.FastTrack.images.hex_reveal_fill.png";

		public const string HEX_UNKNOWN = "PeterHan.FastTrack.images.hex_unknown.png";
		
		// Sprites are loaded in OnLoad
		private static Sprite hexBorder;

		private static Sprite hexFill;

		private static Sprite hexReveal;

		/// <summary>
		/// Loads the sprites needed for the fast starmap.
		/// </summary>
		internal static void LoadSprites() {
			hexBorder = PUIUtils.LoadSprite(HEX_BORDER);
			hexBorder.name = "hex";
			hexFill = PUIUtils.LoadSprite(HEX_FILL);
			hexFill.name = "hex_fill";
			hexReveal = PUIUtils.LoadSprite(HEX_REVEAL);
			hexReveal.name = "hex_reveal_fill";
			// Put this one into the sprite list
			var hexUnknown = PUIUtils.LoadSprite(HEX_UNKNOWN);
			hexUnknown.name = "hex_unknown";
			if (hexUnknown != null)
				Assets.Sprites["hex_unknown"] = hexUnknown;
			// State 0: Idle
			COLORS_FOG[0] = MixColor(BORDER_ACCENT, MixColor(FOG_OF_WAR, BORDER_IDLE));
			// State 1: Selected
			COLORS_FOG[1] = MixColor(FOG_OF_WAR, FILL_SELECTED);
			COLORS_FOG[2] = MixColor(BORDER_ACCENT, MixColor(FOG_OF_WAR, BORDER_SELECTED));
			// State 2: Orbit Highlight (is this possible with fog of war?)
			COLORS_FOG[3] = MixColor(FOG_OF_WAR, FILL_ORBIT);
			COLORS_FOG[4] = MixColor(BORDER_ACCENT, MixColor(FOG_OF_WAR, BORDER_ORBIT));
			// State 0: Idle
			COLORS_NORMAL[0] = MixColor(BORDER_ACCENT, BORDER_IDLE);
			// State 1: Selected
			COLORS_NORMAL[1] = FILL_SELECTED;
			COLORS_NORMAL[2] = MixColor(BORDER_ACCENT, BORDER_SELECTED);
			// State 2: Orbit Highlight
			COLORS_NORMAL[3] = FILL_ORBIT;
			COLORS_NORMAL[4] = MixColor(BORDER_ACCENT, BORDER_ORBIT);
		}

		/// <summary>
		/// Alpha mixes the colors as they would be displayed on screen.
		/// </summary>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		/// <returns>The mixed color resulting from putting fg on bg.</returns>
		private static Color MixColor(Color fg, Color bg) {
			float bga = bg.a, fga = fg.a;
			float alpha = Mathf.Lerp(bga, 1.0f, fga);
			return alpha == 0.0f ? new Color(0.0f, 0.0f, 0.0f, 0.0f) : new Color(
				Mathf.Lerp(bg.r * bga, fg.r, fga) / alpha,
				Mathf.Lerp(bg.g * bga, fg.g, fga) / alpha,
				Mathf.Lerp(bg.b * bga, fg.b, fga) / alpha, alpha);
		}

		/// <summary>
		/// Precalculates the required colors for the multi toggle and updates its settings.
		/// </summary>
		/// <param name="toggle">The toggle to update.</param>
		/// <param name="level">The current reveal level of the tile.</param>
		internal static void SetColors(MultiToggle toggle, ClusterRevealLevel level) {
			var states = toggle.states;
			if (states.Length == 3) {
				bool isFog = level == ClusterRevealLevel.Hidden;
				var colorPalette = isFog ? COLORS_FOG : COLORS_NORMAL;
				// If fogged, adjust background scale
				var t = toggle.toggle_image.transform;
				if (t != null) {
					float scale = isFog ? 1.0f : 0.9f;
					t.localScale = new Vector3(scale, scale, 1.0f);
				}
				// State 0: Idle
				ref var idle = ref states[0];
				idle.color.a = isFog ? FOG_OF_WAR.a : 0.0f;
				idle.additional_display_settings[0].color = colorPalette[0];
				// State 1: Selected
				ref var selected = ref states[1];
				selected.color = colorPalette[1];
				selected.additional_display_settings[0].color = colorPalette[2];
				// State 2: Orbit Highlight
				ref var orbit = ref states[2];
				orbit.color = colorPalette[3];
				orbit.additional_display_settings[0].color = colorPalette[4];
			} else
				PUtil.LogWarning("Setting colors of ClusterMapHex, but states do not match!");
		}
		
		/// <summary>
		/// Updates the hover colors for a hex.
		/// </summary>
		/// <param name="hex">The hex to update.</param>
		/// <param name="validDestination">Whether the hex is a valid destination.</param>
		internal static void SetHover(ClusterMapHex hex, bool validDestination) {
			bool isFog = hex._revealLevel == ClusterRevealLevel.Hidden;
			var hoverColor = validDestination ? hex.hoverColorValid : hex.hoverColorInvalid;
			var hoverFill = isFog ? MixColor(FOG_OF_WAR, hoverColor) : hoverColor;
			var states = hex.states;
			int n = states.Length;
			for (int i = 0; i < n; i++) {
				ref var state = ref states[i];
				state.color_on_hover = hoverFill;
				state.additional_display_settings[0].color_on_hover = hoverColor;
			}
			hex.RefreshHoverColor();
		}

		/// <summary>
		/// The core shared code of the MoveToNISPosition method, split out to optimize
		/// updates in ScreenUpdate.
		/// </summary>
		/// <param name="instance">The map screen rendering this method.</param>
		/// <param name="scale">The current scale of the starmap.</param>
		/// <param name="targetScale">The scale that the starmap is moving towards.</param>
		/// <param name="position">The current and new position of the starmap</param>
		private static void NISMoveCore(ClusterMapScreen instance,
				float scale, ref float targetScale, ref Vector3 position) {
			float dt = Time.unscaledDeltaTime;
			bool move = true;
			var pos = position;
			var targetPosition = instance.targetNISPosition;
			var destination = new Vector3(-targetPosition.x * scale, -targetPosition.y *
				scale, targetPosition.z);
			var cells = instance.m_cellVisByLocation;
			// Always 150.0 when reached
			targetScale = Mathf.Lerp(targetScale, 150.0f, dt * 2.0f);
			position = pos = Vector3.Lerp(pos, destination, dt * 2.5f);
			float distance = Vector3.Distance(pos, destination);
			// Close to destination?
			if (distance < 100.0f && cells.TryGetValue(instance.selectOnMoveNISComplete,
					out var visualizer)) {
				if (visualizer.TryGetComponent(out ClusterMapHex hex) && hex != instance.
						m_selectedHex)
					instance.SelectHex(hex);
				// Reached destination?
				if (distance < 10.0f)
					move = false;
			}
			instance.movingToTargetNISPosition = move;
		}

		/// <summary>
		/// Applied to ClusterMapScreen to turn off the floating asteroid animation which is
		/// shockingly slow.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.
			FloatyAsteroidAnimation))]
		internal static class FloatyAsteroidAnimation_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

			/// <summary>
			/// Applied before FloatyAsteroidAnimation runs.
			/// </summary>
			internal static bool Prefix() {
				return false;
			}
		}

		/// <summary>
		/// Applied to ClusterMapScreen to optimize the move-to animation.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.MoveToNISPosition))]
		internal static class MoveToNISPosition_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before MoveToNISPosition runs.
			/// </summary>
			internal static bool Prefix(ClusterMapScreen __instance) {
				RectTransform content;
				var mapScrollRect = __instance.mapScrollRect;
				if (__instance.movingToTargetNISPosition && mapScrollRect != null && (content =
						mapScrollRect.content) != null) {
					var pos = content.localPosition;
					NISMoveCore(__instance, __instance.m_currentZoomScale, ref __instance.
						m_targetZoomScale, ref pos);
					content.localPosition = pos;
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to ClusterMapScreen to massively clean up and optimize its prefabs on
		/// load.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.OnPrefabInit))]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ClusterMapReduce &&
				DlcManager.FeatureClusterSpaceEnabled();

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(ClusterMapScreen __instance) {
				var prefab = __instance.cellVisPrefab;
				if (prefab != null && hexBorder != null && hexFill != null) {
					// Kill the Mask that will not actually work as it only applies to children
					Image image;
					var fill = prefab.transform.Find("Fill");
					if (fill != null && fill.TryGetComponent(out Mask mask)) {
						// Yes this is dangerous, but regular Destroy was not actually removing
						// the objects from the prefab
						Object.DestroyImmediate(mask);
						if (fill.TryGetComponent(out image))
							image.sprite = hexFill;
					}
					// Kill the redundant accent
					var accent = prefab.transform.Find("BorderAccent");
					if (accent != null)
						Object.DestroyImmediate(accent.gameObject);
					// Border: downsize image
					var border = prefab.transform.Find("Border");
					if (border != null && border.TryGetComponent(out image))
						image.sprite = hexBorder;
					// Kill the fog of war
					var fog = prefab.transform.Find("FogOfWar");
					if (fog != null)
						Object.DestroyImmediate(fog.gameObject);
					// Peek: downscale image
					var peek = prefab.transform.Find("PeekedTile");
					if (peek != null && peek.TryGetComponent(out image)) {
						image.sprite = hexReveal;
						image.gameObject.SetActive(false);
					}
					// Configure initial multi toggle
					if (prefab.TryGetComponent(out ClusterMapHex hex)) {
						var states = hex.states;
						int n = states.Length;
						for (int i = 0; i < n; i++) {
							ref var state = ref states[i];
							state.sprite = hexFill;
							state.additional_display_settings[0].sprite = hexBorder;
						}
						SetColors(hex, ClusterRevealLevel.Hidden);
					}
				}
			}
		}

		/// <summary>
		/// Applied to ClusterMapScreen to only move things on the map if they need to be updated.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.ScreenUpdate))]
		internal static class ScreenUpdate_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before ScreenUpdate runs.
			/// </summary>
			internal static bool Prefix(ClusterMapScreen __instance) {
				RectTransform content;
				var mapScrollRect = __instance.mapScrollRect;
				if (mapScrollRect != null && (content = mapScrollRect.content) != null) {
					float scale = __instance.m_currentZoomScale, target = __instance.
						m_targetZoomScale;
					var mousePos = KInputManager.GetMousePos();
					var ip = content.InverseTransformPoint(mousePos);
					Vector3 pos = content.localPosition;
					bool move = false;
					if (!Mathf.Approximately(target, scale)) {
						// Only if necessary
						__instance.m_currentZoomScale = scale = Mathf.Lerp(scale, target,
							Mathf.Min(4.0f * Time.unscaledDeltaTime, 0.9f));
						content.localScale = new Vector3(scale, scale, 1f);
						var fp = content.InverseTransformPoint(mousePos);
						if (!Mathf.Approximately(ip.x, fp.x) || !Mathf.Approximately(ip.y,
								fp.y) || !Mathf.Approximately(ip.z, fp.z)) {
							// If the point changed, center it correctly
							pos += (fp - ip) * scale;
							move = true;
						}
					}
					if (__instance.movingToTargetNISPosition) {
						move = true;
						NISMoveCore(__instance, scale, ref target, ref pos);
						__instance.m_targetZoomScale = target;
					}
					if (move)
						content.localPosition = pos;
				}
				return false;
			}
		}
	}
}
