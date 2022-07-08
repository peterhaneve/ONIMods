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
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Database;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Patches which will be applied via annotations for Forbid Items.
	/// </summary>
	public sealed class ForbidItemsPatches : KMod.UserMod2 {
		internal static readonly Tag Forbidden = new Tag("Forbidden");

		internal static StatusItem ForbiddenStatus;
		
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			ForbiddenStatus = Db.Get().MiscStatusItems.Add(new StatusItem(Forbidden.Name,
				"MISC", "status_item_building_disabled", StatusItem.IconType.Custom,
				NotificationType.Neutral, false, OverlayModes.None.ID));
		}

		/// <summary>
		/// Takes into account whether the item is forbidden when considering it for fetching.
		/// </summary>
		/// <param name="pickupable">The item to query.</param>
		/// <param name="originalTag">The original tag that may not be fetched (StoredPrivate).</param>
		/// <returns>true if the item may not be fetched, or false if it can be fetched.</returns>
		private static bool IsSuitableTags(Component pickupable, Tag originalTag) {
			return pickupable != null && pickupable.TryGetComponent(out KPrefabID id) &&
				(id.HasTag(originalTag) || id.HasTag(Forbidden));
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(ForbidItemsPatches));
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.MISC));
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.UI));
			new PLocalization().Register();
		}

		/// <summary>
		/// Applied to Clearable to add a button to toggle forbid.
		/// 
		/// Settings Change Tool adds a drag-tool for mass forbidding.
		/// </summary>
		[HarmonyPatch(typeof(Clearable), "OnRefreshUserMenu")]
		public static class Clearable_OnRefreshUserMenu_Patch {
			/// <summary>
			/// Applied after OnRefreshUserMenu runs.
			/// </summary>
			internal static void Postfix(Clearable __instance) {
				GameObject go;
				// Only apply to things that can otherwise be swept
				if (__instance != null && __instance.isClearable && (go = __instance.
						gameObject).TryGetComponent(out KPrefabID kpid) && !kpid.HasTag(
						GameTags.Stored) && !__instance.TryGetComponent(out Health _)) {
					var forbiddable = go.AddOrGet<Forbiddable>();
					string text, tooltip;
					System.Action handler;
					if (kpid.HasTag(Forbidden)) {
						text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME_OFF;
						tooltip = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP_OFF;
						handler = forbiddable.Reclaim;
					} else {
						text = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.NAME;
						tooltip = ForbidItemsStrings.UI.USERMENUACTIONS.FORBIDITEM.TOOLTIP;
						handler = forbiddable.Forbid;
					}
					Game.Instance.userMenu.AddButton(__instance.gameObject, new
						KIconButtonMenu.ButtonInfo("action_building_disabled", text, handler,
						PAction.MaxAction, null, null, null, tooltip));
				}
			}
		}

		/// <summary>
		/// Applied to EntityTemplates to make dropped items forbiddable.
		/// </summary>
		[HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.
			CreateBaseOreTemplates))]
		public static class EntityTemplates_CreateBaseOreTemplates_Patch {
			/// <summary>
			/// Applied after CreateBaseOreTemplates runs.
			/// </summary>
			internal static void Postfix(GameObject ___baseOreTemplate) {
				___baseOreTemplate.AddOrGet<Forbiddable>();
			}
		}

		/// <summary>
		/// Applied to FetchableMonitor.Instance to make forbidden items unfetchable.
		/// </summary>
		[HarmonyPatch(typeof(FetchableMonitor.Instance), nameof(FetchableMonitor.Instance.
			IsFetchable))]
		public static class FetchableMonitor_IsFetchable_Patch {
			/// <summary>
			/// Transpiles IsFetchable to check for the Forbidden tag. Much faster than
			/// postfixing it, as this is a performance sensitive method.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(
					KPrefabIDExtensions).GetMethodSafe(nameof(KPrefabIDExtensions.HasTag),
					true, typeof(Component), typeof(Tag)), typeof(ForbidItemsPatches).
					GetMethodSafe(nameof(IsSuitableTags), true, typeof(Component),
					typeof(Tag)));
			}
		}
	}
}
