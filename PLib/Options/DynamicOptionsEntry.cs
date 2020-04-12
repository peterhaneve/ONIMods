/*
 * Copyright 2020 Peter Han
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

using PeterHan.PLib.UI;
using System;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry controlled by the mod.
	/// </summary>
	internal sealed class DynamicOptionsEntry : OptionsEntry, IUIComponent {
		public string Name => nameof(DynamicOptionsEntry);

		protected override object Value {
			get {
				return getValue?.Invoke();
			}
			set {
				setValue?.Invoke(value);
			}
		}

		/// <summary>
		/// Creates a UI entry for the option.
		/// </summary>
		private readonly Func<GameObject> createUIEntry;

		/// <summary>
		/// Gets the title of the option.
		/// </summary>
		private readonly Func<string> getTitle;

		/// <summary>
		/// Gets the tooltip of the option.
		/// </summary>
		private readonly Func<string> getTooltip;

		/// <summary>
		/// Gets the value of the option.
		/// </summary>
		private readonly Func<object> getValue;

		/// <summary>
		/// The raw handler from the using mod (perhaps another DLL) which handles this option.
		/// </summary>
		private readonly object handler;

		public event PUIDelegates.OnRealize OnRealize;

		/// <summary>
		/// Sets the value of the option.
		/// </summary>
		private readonly Action<object> setValue;

		internal DynamicOptionsEntry(string field, DynamicOptionAttribute attr) : base(field,
				null, null) {
			var targetType = attr.Handler;
			if (targetType == null)
				throw new ArgumentNullException("targetType");
			try {
				handler = Activator.CreateInstance(targetType);
			} catch (Exception e) {
				// Log error
				PUtil.LogError("Cannot create options handler for " + field + ":");
				PUtil.LogException(e.GetBaseException() ?? e);
				handler = null;
			}
			if (handler != null) {
				createUIEntry = targetType.CreateDelegate<Func<GameObject>>(nameof(
					IDynamicOption.CreateUIEntry), handler);
				getTitle = targetType.CreateGetDelegate<string>(nameof(IDynamicOption.Title),
					handler);
				getTooltip = targetType.CreateGetDelegate<string>(nameof(IDynamicOption.
					Tooltip), handler);
				getValue = targetType.CreateGetDelegate<object>(nameof(IDynamicOption.Value),
					handler);
				setValue = targetType.CreateSetDelegate<object>(nameof(IDynamicOption.Value),
					handler);
				Title = getTitle.Invoke();
			} else {
				createUIEntry = null;
				getTitle = null;
				getTooltip = null;
				getValue = null;
				setValue = null;
			}
		}

		public GameObject Build() {
			var obj = createUIEntry?.Invoke() ?? new GameObject("No handler specified");
			OnRealize?.Invoke(obj);
			return obj;
		}

		internal override void CreateUIEntry(PGridPanel parent, ref int row) {
			if (getTitle != null)
				Title = getTitle.Invoke();
			else
				// Only displayed in error cases, should not be localized
				Title = "<No Title>";
			var label = new PLabel("Label") {
				Text = LookInStrings(Title), TextStyle = PUITuning.Fonts.TextLightStyle
			};
			label.OnRealize += OnRealizeLabel;
			parent.AddChild(label, new GridComponentSpec(row, 0) {
				Margin = LABEL_MARGIN, Alignment = TextAnchor.MiddleLeft
			});
			parent.AddChild(GetUIComponent(), new GridComponentSpec(row, 1) {
				Alignment = TextAnchor.MiddleRight, Margin = CONTROL_MARGIN
			});
		}

		protected override IUIComponent GetUIComponent() {
			return this;
		}

		private void OnRealizeLabel(GameObject realized) {
			// Make tooltip update dynamically
			if (getTooltip != null) {
				var tt = realized.AddOrGet<ToolTip>();
				tt.refreshWhileHovering = true;
				tt.OnToolTip = getTooltip;
			}
		}
	}
}
