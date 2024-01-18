/*
 * Copyright 2024 Peter Han
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

using UnityEngine;

namespace PeterHan.WorkshopProfiles {
	/// <summary>
	/// One row of a side screen for workshop profiles, which borrows prefabs from the Access
	/// Control side screen row.
	/// </summary>
	public sealed class WorkshopProfileSideScreenRow : KMonoBehaviour {
		/// <summary>
		/// The text box showing "-" or "Assigned".
		/// </summary>
		[SerializeField]
		internal LocText assignment;

		/// <summary>
		/// The prefab to use for displaying Duplicant portraits.
		/// </summary>
		[SerializeField]
		internal CrewPortrait crewPortraitPrefab;

		/// <summary>
		/// The current state of the row.
		/// </summary>
		public AllowableState currentState;

		/// <summary>
		/// The current image displaying the Duplicant's portrait. Created lazily when the
		/// row is actually displayed.
		/// </summary>
		private CrewPortrait portraitInstance;

		/// <summary>
		/// The parent of this row.
		/// </summary>
		internal WorkshopProfileSideScreen sideScreen;

		/// <summary>
		/// The Duplicant for this row.
		/// </summary>
		private IAssignableIdentity targetIdentity;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		[MyCmpReq]
		private MultiToggle toggle;

		[MyCmpReq]
		private ToolTip tooltip;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		/// <summary>
		/// Clears the currently set Duplicant.
		/// </summary>
		internal void ClearIdentity() {
			targetIdentity = null;
		}

		/// <summary>
		/// Generates the tool tip text for the specified Duplicant row.
		/// </summary>
		/// <returns>The tool tip text to display for allowing or disallowing.</returns>
		private string GetTooltip() {
			string text = "";
			// UNITY WHY
			if (targetIdentity != null && !targetIdentity.IsNull()) {
				if (currentState == AllowableState.Allowed)
					text = string.Format(STRINGS.UI.UISIDESCREENS.ASSIGNABLESIDESCREEN.
						UNASSIGN_TOOLTIP, targetIdentity.GetProperName());
				else
					text = string.Format(STRINGS.UI.UISIDESCREENS.ASSIGNABLESIDESCREEN.
						ASSIGN_TO_TOOLTIP, targetIdentity.GetProperName());
			}
			return text;
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			toggle.onClick = OnSelect;
			tooltip.OnToolTip = GetTooltip;
			Refresh();
		}

		/// <summary>
		/// Fired when the row is clicked to toggle between allowed and disallowed.
		/// </summary>
		private void OnSelect() {
			if (sideScreen != null)
				sideScreen.OnRowClicked(targetIdentity);
		}

		/// <summary>
		/// Refreshes the current Duplicant's allowed or disallowed state.
		/// </summary>
		public void Refresh() {
			bool allowed;
			if (toggle != null && targetIdentity != null && !targetIdentity.IsNull()) {
				// If a true Duplicant, use their profile, otherwise the "public" profile
				if (targetIdentity is MinionAssignablesProxy trueDupe)
					allowed = sideScreen.profile.IsAllowed(trueDupe.TargetInstanceID);
				else
					allowed = sideScreen.profile.IsPublicAllowed();
				if (allowed) {
					currentState = AllowableState.Allowed;
					assignment.text = STRINGS.UI.UISIDESCREENS.ASSIGNABLESIDESCREEN.ASSIGNED;
				} else {
					currentState = AllowableState.Disallowed;
					assignment.text = STRINGS.UI.UISIDESCREENS.ASSIGNABLESIDESCREEN.UNASSIGNED;
				}
				toggle.ChangeState((int)currentState);
			}
		}

		/// <summary>
		/// Initializes the row with a specific Duplicant/group and the parent side screen.
		/// </summary>
		/// <param name="minion">The Duplicant to display.</param>
		/// <param name="parent">The parent of this side screen row.</param>
		public void SetContent(IAssignableIdentity minion, WorkshopProfileSideScreen parent) {
			sideScreen = parent;
			targetIdentity = minion;
			// Create the picture of the Duplicant's head
			if (portraitInstance == null) {
				portraitInstance = Util.KInstantiateUI<CrewPortrait>(crewPortraitPrefab.
					gameObject, gameObject, false);
				portraitInstance.transform.SetSiblingIndex(1);
				portraitInstance.SetAlpha(1f);
			}
			portraitInstance.SetIdentityObject(minion, false);
			Refresh();
		}

		/// <summary>
		/// The potential display states of a particular Duplicant.
		/// </summary>
		public enum AllowableState {
			Allowed,
			Disallowed
		}
	}
}
