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

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Allows the use of forbidden items.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class AllowForbiddenItems : KMonoBehaviour, ICheckboxControl {
		/// <summary>
		/// The event triggered when the Allow Forbidden Items checkbox is changed.
		/// </summary>
		public const GameHashes ForbiddenUseChanged = (GameHashes)(-973895770);

		/// <summary>
		/// The tag applied to objects where forbidden object use is acceptable.
		/// </summary>
		public static readonly Tag AllowForbiddenUse = TagManager.Create("AllowForbiddenUse");

		public string CheckboxTitleKey => ForbidItemsStrings.UI.ALLOW_FORBIDDEN_SIDE_SCREEN.FORBIDDEN.key.String;

		public string CheckboxLabel => ForbidItemsStrings.UI.ALLOW_FORBIDDEN_SIDE_SCREEN.FORBIDDEN;

		public string CheckboxTooltip => ForbidItemsStrings.UI.ALLOW_FORBIDDEN_SIDE_SCREEN.FORBIDDEN_TOOLTIP;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		[MyCmpReq]
		private KPrefabID prefabID;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		[Serialize]
		public bool allowForbidden;

		public bool GetCheckboxValue() {
			return allowForbidden;
		}

		public override void OnSpawn() {
			base.OnSpawn();
			if (allowForbidden)
				prefabID.AddTag(AllowForbiddenUse);
		}

		public void SetCheckboxValue(bool value) {
			if (allowForbidden != value) {
				if (value)
					prefabID.AddTag(AllowForbiddenUse);
				else
					prefabID.RemoveTag(AllowForbiddenUse);
			}
			allowForbidden = value;
		}
	}
}
