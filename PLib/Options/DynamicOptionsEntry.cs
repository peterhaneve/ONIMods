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
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry controlled by the mod.
	/// </summary>
	internal sealed class DynamicOptionsEntry : OptionsEntry {
		/// <summary>
		/// Creates a new dynamic options entry from a [DynamicOption] attribute.
		/// </summary>
		/// <param name="name">The property name.</param>
		/// <param name="attr">The attribute giving the type of the handler.</param>
		/// <returns>A dynamic options entry for that type.</returns>
		internal static DynamicOptionsEntry Create(string name, DynamicOptionAttribute attr) {
			var targetType = attr.Handler;
			object handler;
			if (targetType == null)
				throw new ArgumentNullException("targetType");
			try {
				handler = Activator.CreateInstance(targetType);
			} catch (Exception e) {
				// Log error
				PUtil.LogError("Cannot create options handler for " + name + ":");
				PUtil.LogException(e.GetBaseException());
				handler = null;
			}
			return new DynamicOptionsEntry(name, attr.Category, handler);
		}

		/// <summary>
		/// Create a new dynamic options entry with no backing property. All handling of this
		/// entry must be done by the user options class.
		/// </summary>
		/// <param name="handler">The handler for that option.</param>
		/// <returns>A dynamic options entry for that type.</returns>
		internal static DynamicOptionsEntry Create(object handler) {
			string category = null;
			if (handler != null) {
				// Retrieve the category from the user handler
				var getter = handler.GetType().GetPropertySafe<string>(nameof(IDynamicOption.
					Category), false)?.GetGetMethod();
				if (getter != null)
					try {
						category = getter.Invoke(handler, null)?.ToString();
					} catch (TargetInvocationException e) {
						PUtil.LogExcWarn(e);
					}
			}
			return new DynamicOptionsEntry("Dynamic", category, handler);
		}

		public override string Name => nameof(DynamicOptionsEntry);

		public override object Value {
			get {
				return getValue?.Invoke();
			}
			set {
				setValue?.Invoke(value);
			}
		}

		/// <summary>
		/// Creates the entire row used to display this option.
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
		/// Gets the component used to display this option. createUIEntry will take precedence.
		/// </summary>
		private readonly Func<GameObject> getUIComponent;

		/// <summary>
		/// Gets the value of the option.
		/// </summary>
		private readonly Func<object> getValue;

		/// <summary>
		/// The raw handler from the using mod (perhaps another DLL) which handles this option.
		/// </summary>
		private readonly object handler;

		/// <summary>
		/// Sets the value of the option.
		/// </summary>
		private readonly Action<object> setValue;

		private DynamicOptionsEntry(string name, string category, object handler) : base(name,
				new OptionAttribute(name, null, category)) {
			this.handler = handler;
			if (handler != null) {
				var targetType = handler.GetType();
				createUIEntry = targetType.CreateDelegate<Func<GameObject>>(nameof(
					CreateUIEntry), handler);
				getUIComponent = targetType.CreateDelegate<Func<GameObject>>(nameof(
					IDynamicOption.GetUIComponent), handler);
				getTitle = targetType.CreateGetDelegate<string>(nameof(IDynamicOption.Title),
					handler);
				getTooltip = targetType.CreateGetDelegate<string>(nameof(IDynamicOption.
					ToolTip), handler);
				getValue = targetType.CreateGetDelegate<object>(nameof(IDynamicOption.Value),
					handler);
				setValue = targetType.CreateSetDelegate<object>(nameof(IDynamicOption.Value),
					handler);
				Title = getTitle.Invoke();
			} else {
				getUIComponent = null;
				getTitle = null;
				getTooltip = null;
				getValue = null;
				setValue = null;
			}
		}

		public override void CreateUIEntry(PGridPanel parent, ref int row) {
			if (createUIEntry != null)
				parent.AddChild(this, new GridComponentSpec(row, 0) {
					Alignment = TextAnchor.MiddleLeft, Margin = CONTROL_MARGIN
				});
			else {
				if (getTitle != null)
					Title = getTitle.Invoke();
				else
					// Only displayed in error cases, should not be localized
					Title = "<No Title>";
				parent.AddChild(new PLabel("Label") {
					Text = LookInStrings(Title), TextStyle = PUITuning.Fonts.TextLightStyle
				}.AddOnRealize(OnRealizeLabel), new GridComponentSpec(row, 0) {
					Margin = LABEL_MARGIN, Alignment = TextAnchor.MiddleLeft
				});
				parent.AddChild(this, new GridComponentSpec(row, 1) {
					Alignment = TextAnchor.MiddleRight, Margin = CONTROL_MARGIN
				});
			}
		}

		public override GameObject GetUIComponent() {
			GameObject ui;
			if (createUIEntry != null)
				// This is inserted at toplevel if it is not null
				ui = createUIEntry.Invoke();
			else if (getUIComponent != null)
				ui = getUIComponent.Invoke();
			else
				ui = new GameObject("No handler specified");
			return ui;
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
