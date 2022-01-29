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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.WorkshopProfiles {
	/// <summary>
	/// A side screen for workshop profiles, which borrows prefabs from the Access Control
	/// side screen.
	/// </summary>
	public sealed class WorkshopProfileSideScreen : SideScreenContent {
		// Detours used for accessing the prefab properties in AssignableSideScreen
		private static readonly IDetouredField<AssignableSideScreen, MultiToggle> ALLOW_SORT =
			PDetours.DetourField<AssignableSideScreen, MultiToggle>("generalSortingToggle");
		private static readonly IDetouredField<AssignableSideScreenRow, LocText> ASSIGN_TEXT =
			PDetours.DetourField<AssignableSideScreenRow, LocText>("assignmentText");
		private static readonly IDetouredField<AssignableSideScreenRow, CrewPortrait>
			CREW_IMAGE = PDetours.DetourField<AssignableSideScreenRow, CrewPortrait>(
			"crewPortraitPrefab");
		private static readonly IDetouredField<AssignableSideScreen, MultiToggle> DUPE_SORT =
			PDetours.DetourField<AssignableSideScreen, MultiToggle>("dupeSortingToggle");
		private static readonly IDetouredField<AssignableSideScreen, GameObject> ROW_GROUP =
			PDetours.DetourField<AssignableSideScreen, GameObject>("rowGroup");
		private static readonly IDetouredField<AssignableSideScreen, AssignableSideScreenRow>
			ROW_PREFAB = PDetours.DetourField<AssignableSideScreen, AssignableSideScreenRow>(
			"rowPrefab");

		/// <summary>
		/// Adds this side screen to the side screen list, and loads the prefabs from the
		/// Assignable side screen for UI consistency.
		/// </summary>
		/// <param name="existing">The existing side screens.</param>
		/// <param name="parent">The parent where the screens need to be added.</param>
		internal static void AddSideScreen(IList<DetailsScreen.SideScreenRef> existing,
				GameObject parent) {
			bool found = false;
			// Does it in a custom way because we need to duplicate an existing UI, not make
			// a new one with PLib
			foreach (var ssRef in existing)
				if (ssRef.screenPrefab is AssignableSideScreen ss) {
					var newScreen = new DetailsScreen.SideScreenRef();
					var ours = CreateScreen(ss);
					// Reparent to the details screen
					found = true;
					newScreen.name = nameof(WorkshopProfileSideScreen);
					newScreen.screenPrefab = ours;
					newScreen.screenInstance = ours;
					// It somehow gets a 0.8x scale
					var ssTransform = ours.gameObject.transform;
					ssTransform.SetParent(parent.transform);
					ssTransform.localScale = Vector3.one;
					existing.Insert(0, newScreen);
					// Must break to avoid concurrent modification exception
					break;
				}
			if (!found)
				PUtil.LogWarning("Unable to find Assignable side screen!");
		}

		/// <summary>
		/// Creates the workshop profiles side screen.
		/// </summary>
		/// <param name="ss">The template Assignable side screen to use for the UI.</param>
		/// <returns>The Workshop Profiles side screen.</returns>
		private static WorkshopProfileSideScreen CreateScreen(AssignableSideScreen ss) {
			var template = Instantiate(ss.gameObject);
			template.name = nameof(WorkshopProfileSideScreen);
			// Copy the assignable screen
			bool active = template.activeSelf;
			template.SetActive(false);
			var oldScreen = template.GetComponent<AssignableSideScreen>();
			var ours = template.AddComponent<WorkshopProfileSideScreen>();
			// Both toggles are named "ArrowToggle" so using Find won't work well
			ours.rowGroup = ROW_GROUP.Get(oldScreen);
			ours.generalSortingToggle = ALLOW_SORT.Get(oldScreen);
			ours.dupeSortingToggle = DUPE_SORT.Get(oldScreen);
			// Copy data from the Assignable row
			var rowObject = Instantiate(ROW_PREFAB.Get(oldScreen).gameObject);
			var oldRow = rowObject.GetComponent<AssignableSideScreenRow>();
			var wpr = rowObject.AddComponent<WorkshopProfileSideScreenRow>();
			wpr.assignment = ASSIGN_TEXT.Get(oldRow);
			wpr.crewPortraitPrefab = CREW_IMAGE.Get(oldRow);
			ours.rowPrefab = wpr;
			// Remove the old components
			DestroyImmediate(oldScreen);
			DestroyImmediate(oldRow);
			template.SetActive(active);
			return ours;
		}

		/// <summary>
		/// The UI element currently being used for sorting.
		/// </summary>
		private MultiToggle activeSortToggle;

		/// <summary>
		/// Selects sorting by whether the Duplicant is allowed.
		/// </summary>
		private MultiToggle generalSortingToggle;

		private readonly IDictionary<IAssignableIdentity, WorkshopProfileSideScreenRow> allowRows;

		/// <summary>
		/// Selects sorting by Duplicant name.
		/// </summary>
		private MultiToggle dupeSortingToggle;

		/// <summary>
		/// A list of all assignable Duplicants.
		/// </summary>
		private readonly List<MinionAssignablesProxy> identities;

		/// <summary>
		/// The parent of the allowable rows.
		/// </summary>
		private GameObject rowGroup;

		/// <summary>
		/// Generates new rows when necessary.
		/// </summary>
		private UIPool<WorkshopProfileSideScreenRow> rowPool;

		/// <summary>
		/// The template used for each new row.
		/// </summary>
		private WorkshopProfileSideScreenRow rowPrefab;

		/// <summary>
		/// Whether the current sort direction is reversed.
		/// </summary>
		private bool sortReversed;

		/// <summary>
		/// The selected object with workshop profiles.
		/// </summary>
		internal WorkshopProfile profile;

		internal WorkshopProfileSideScreen() {
			allowRows = new Dictionary<IAssignableIdentity, WorkshopProfileSideScreenRow>(32);
			identities = new List<MinionAssignablesProxy>(32);
			sortReversed = false;
		}

		/// <summary>
		/// Removes all rows from the side screen.
		/// </summary>
		private void ClearContent() {
			if (rowPool != null)
				rowPool.DestroyAll();
			// Unassign all existing rows
			foreach (var pair in allowRows)
				pair.Value.ClearIdentity();
			allowRows.Clear();
		}

		public override void ClearTarget() {
			profile = null;
			Components.LiveMinionIdentities.OnAdd -= OnMinionIdentitiesChanged;
			Components.LiveMinionIdentities.OnRemove -= OnMinionIdentitiesChanged;
			base.ClearTarget();
		}

		/// <summary>
		/// Compares two rows by the current allow state.
		/// </summary>
		private int CompareByAllowed(IAssignableIdentity id1, IAssignableIdentity id2) {
			int result = allowRows[id1].currentState.CompareTo(allowRows[id2].
				currentState);
			if (result == 0)
				result = id1.GetProperName().CompareTo(id2.GetProperName());
			return result * (sortReversed ? -1 : 1);
		}

		/// <summary>
		/// Converts the profile from a "public" profile to one with each current Duplicant
		/// allowed explicitly by name.
		/// </summary>
		private void ConvertPublicToExplicit() {
			// All Duplicants must then be set to "allow", so disallow all and
			// add each Duplicant manually
			profile.DisallowAll();
			foreach (var eachDupe in identities)
				if (eachDupe != null && !eachDupe.IsNull() && (eachDupe is
						MinionAssignablesProxy proxy))
					profile.AddDuplicant(proxy.TargetInstanceID);
		}

		/// <summary>
		/// Compares two rows by the Duplicant's name.
		/// </summary>
		private int CompareByName(IAssignableIdentity id1, IAssignableIdentity id2) {
			return id1.GetProperName().CompareTo(id2.GetProperName()) *
				(sortReversed ? -1 : 1);
		}

		/// <summary>
		/// Sorts the rows by the specified sorting function, respecting the reverse flag.
		/// </summary>
		private void ExecuteSort() {
			var rowKeys = ListPool<IAssignableIdentity, WorkshopProfileSideScreen>.Allocate();
			rowKeys.AddRange(allowRows.Keys);
			if (activeSortToggle == dupeSortingToggle)
				rowKeys.Sort(CompareByName);
			else
				rowKeys.Sort(CompareByAllowed);
			for (int i = 0; i < rowKeys.Count; i++)
				allowRows[rowKeys[i]].transform.SetSiblingIndex(i);
			rowKeys.Recycle();
		}

		public override string GetTitle() {
			return WorkshopProfilesStrings.UI.UISIDESCREENS.WORKSHOPPROFILES.TITLE;
		}

		public override bool IsValidForTarget(GameObject target) {
			return target.GetComponentSafe<WorkshopProfile>() != null;
		}

		private void OnMinionIdentitiesChanged(MinionIdentity _) {
			ReloadIdentities();
			Refresh();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			// Borrow the UI from AssignableSideScreen
			rowPool = new UIPool<WorkshopProfileSideScreenRow>(rowPrefab);
			ReloadIdentities();
			SortByAllowed(false);
			if (profile != null)
				Refresh();
		}

		/// <summary>
		/// Fired by the rows when a row is clicked in the UI.
		/// </summary>
		/// <param name="identity">The Duplicant (or "public") that was selected or
		/// deselected.</param>
		internal void OnRowClicked(IAssignableIdentity identity) {
			if (identity != null && !identity.IsNull()) {
#if DEBUG
				PUtil.LogDebug("Selected row " + identity.GetProperName());
#endif
				if (identity is MinionAssignablesProxy trueDupe) {
					// Actual Duplicant
					int prefabID = trueDupe.TargetInstanceID;
					if (profile.IsPublicAllowed())
						ConvertPublicToExplicit();
					if (profile.IsAllowed(prefabID))
						profile.RemoveDuplicant(prefabID);
					else
						profile.AddDuplicant(prefabID);
				} else {
					// "Public"
					if (profile.IsPublicAllowed())
						profile.DisallowAll();
					else
						profile.AllowAll();
				}
				// Cheaper than a full rebuild
				foreach (var row in allowRows)
					row.Value.Refresh();
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			dupeSortingToggle.onClick += () => SortByName(true);
			generalSortingToggle.onClick += () => SortByAllowed(true);
		}

		/// <summary>
		/// Called by Unity when this game object is activated or deactivated.
		/// </summary>
		internal void OnValidStateChanged(bool _) {
			if (this != null && gameObject != null && gameObject.activeInHierarchy)
				Refresh();
		}

		/// <summary>
		/// Refreshes the list of available Duplicants.
		/// </summary>
		private void Refresh() {
			ClearContent();
			if (profile != null && rowPool != null) {
				var publicIdentity = Game.Instance.assignmentManager.assignment_groups[
					"public"];
				// "Public"
				var publicRow = rowPool.GetFreeElement(rowGroup, true);
				publicRow.sideScreen = this;
				publicRow.transform.SetAsFirstSibling();
				allowRows.Add(publicIdentity, publicRow);
				publicRow.SetContent(publicIdentity, this);
				// Each Duplicant
				foreach (var identity in identities) {
					var dupeRow = rowPool.GetFreeElement(rowGroup, true);
					dupeRow.sideScreen = this;
					allowRows.Add(identity, dupeRow);
					dupeRow.SetContent(identity, this);
				}
				ExecuteSort();
			}
		}

		/// <summary>
		/// Reloads the list of living Duplicants.
		/// </summary>
		private void ReloadIdentities() {
			identities.Clear();
			identities.AddRange(Components.MinionAssignablesProxy.Items);
		}

		public override void SetTarget(GameObject target) {
			var wp = target.GetComponentSafe<WorkshopProfile>();
			if (wp != null) {
				// Register for changes to Duplicant roster while sidescreen is open
				if (profile == null) {
					Components.LiveMinionIdentities.OnAdd += OnMinionIdentitiesChanged;
					Components.LiveMinionIdentities.OnRemove += OnMinionIdentitiesChanged;
				}
				profile = wp;
				ReloadIdentities();
				Refresh();
			} else
				PUtil.LogWarning("Invalid workshop profile received!");
		}

		/// <summary>
		/// Updates the visuals on the side screen when the list is sorted.
		/// </summary>
		/// <param name="toggle">The sorting option that was chosen.</param>
		/// <param name="reselect">true to allow reversing if that option was already the
		/// current sorting mode, or false to never reverse.</param>
		private void SelectSortToggle(MultiToggle toggle, bool reselect) {
			dupeSortingToggle.ChangeState(0);
			generalSortingToggle.ChangeState(0);
			if (toggle != null) {
				if (reselect && activeSortToggle == toggle)
					sortReversed = !sortReversed;
				activeSortToggle = toggle;
			}
			activeSortToggle.ChangeState(sortReversed ? 2 : 1);
		}

		/// <summary>
		/// Sorts the Duplicant list by whether they are allowed.
		/// </summary>
		/// <param name="reselect">true to allow reversing if that option was already the
		/// current sorting mode, or false to never reverse.</param>
		private void SortByAllowed(bool reselect) {
			SelectSortToggle(generalSortingToggle, reselect);
			ExecuteSort();
		}

		/// <summary>
		/// Sorts the Duplicant list by name.
		/// </summary>
		/// <param name="reselect">true to allow reversing if that option was already the
		/// current sorting mode, or false to never reverse.</param>
		private void SortByName(bool reselect) {
			SelectSortToggle(dupeSortingToggle, reselect);
			ExecuteSort();
		}
	}
}
