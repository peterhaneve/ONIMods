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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.PLib.Actions {
	/// <summary>
	/// Manages PAction functionality which must be single instance.
	/// </summary>
	public sealed class PActionManager : PForwardedComponent {
		/// <summary>
		/// Prototypes the required parameters for new BindingEntry() since they changed in
		/// U39-489490.
		/// </summary>
		private delegate BindingEntry NewEntry(string group, GamepadButton button,
			KKeyCode key_code, Modifier modifier, Action action);

		/// <summary>
		/// The category used for all PLib keys.
		/// </summary>
		public const string CATEGORY = "PLib";

		/// <summary>
		/// Creates a new BindingEntry.
		/// </summary>
		private static readonly NewEntry NEW_BINDING_ENTRY = typeof(BindingEntry).
			DetourConstructor<NewEntry>();

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PActionManager Instance { get; private set; }

		/// <summary>
		/// Assigns the key bindings to each Action when they are needed.
		/// </summary>
		private static void AssignKeyBindings() {
			var allMods = PRegistry.Instance.GetAllComponents(typeof(PActionManager).FullName);
			if (allMods != null)
				foreach (var mod in allMods)
					mod.Process(0, null);
		}

		private static void CKeyDef_Postfix(KInputController.KeyDef __instance) {
			if (Instance != null)
				__instance.mActionFlags = ExtendFlags(__instance.mActionFlags,
					Instance.GetMaxAction());
		}

		/// <summary>
		/// Extends the action flags array to the new maximum length.
		/// </summary>
		/// <param name="oldActionFlags">The old flags array.</param>
		/// <param name="newMax">The minimum length.</param>
		/// <returns>The new action flags array.</returns>
		internal static bool[] ExtendFlags(bool[] oldActionFlags, int newMax) {
			int n = oldActionFlags.Length;
			bool[] newActionFlags;
			if (n < newMax) {
				newActionFlags = new bool[newMax];
				Array.Copy(oldActionFlags, newActionFlags, n);
			} else
				newActionFlags = oldActionFlags;
			return newActionFlags;
		}

		/// <summary>
		/// Checks to see if an action is already bound to a key.
		/// </summary>
		/// <param name="currentBindings">The current key bindings.</param>
		/// <param name="action">The action to look up.</param>
		/// <returns>true if the action already has a binding assigned, or false otherwise.</returns>
		private static bool FindKeyBinding(IEnumerable<BindingEntry> currentBindings,
				Action action) {
			bool inBindings = false;
			foreach (var entry in currentBindings)
				if (entry.mAction == action) {
					LogKeyBind("Action {0} already exists; assigned to KeyCode {1}".F(action,
						entry.mKeyCode));
					inBindings = true;
					break;
				}
			return inBindings;
		}

		/// <summary>
		/// Retrieves a Klei key binding title.
		/// </summary>
		/// <param name="category">The category of the key binding.</param>
		/// <param name="item">The key binding to retrieve.</param>
		/// <returns>The Strings entry describing this key binding.</returns>
		private static string GetBindingTitle(string category, string item) {
			return "STRINGS.INPUT_BINDINGS." + category.ToUpperInvariant() + "." + item.
				ToUpperInvariant();
		}

		/// <summary>
		/// Retrieves a "localized" (if PLib is localized) description of additional key codes
		/// from the KKeyCode enumeration, to avoid warning spam on popular keybinds like
		/// arrow keys, delete, home, and so forth.
		/// </summary>
		/// <param name="code">The key code.</param>
		/// <returns>A description of that key code, or null if no localization is found.</returns>
		internal static string GetExtraKeycodeLocalized(KKeyCode code) {
			string localCode = null;
			switch (code) {
			case KKeyCode.Home:
				localCode = PLibStrings.KEY_HOME;
				break;
			case KKeyCode.End:
				localCode = PLibStrings.KEY_END;
				break;
			case KKeyCode.Delete:
				localCode = PLibStrings.KEY_DELETE;
				break;
			case KKeyCode.PageDown:
				localCode = PLibStrings.KEY_PAGEDOWN;
				break;
			case KKeyCode.PageUp:
				localCode = PLibStrings.KEY_PAGEUP;
				break;
			case KKeyCode.LeftArrow:
				localCode = PLibStrings.KEY_ARROWLEFT;
				break;
			case KKeyCode.UpArrow:
				localCode = PLibStrings.KEY_ARROWUP;
				break;
			case KKeyCode.RightArrow:
				localCode = PLibStrings.KEY_ARROWRIGHT;
				break;
			case KKeyCode.DownArrow:
				localCode = PLibStrings.KEY_ARROWDOWN;
				break;
			case KKeyCode.Pause:
				localCode = PLibStrings.KEY_PAUSE;
				break;
			case KKeyCode.SysReq:
				localCode = PLibStrings.KEY_SYSRQ;
				break;
			case KKeyCode.Print:
				localCode = PLibStrings.KEY_PRTSCREEN;
				break;
			default:
				break;
			}
			return localCode;
		}

		private static bool GetKeycodeLocalized_Prefix(KKeyCode key_code, ref string __result) {
			string newResult = GetExtraKeycodeLocalized(key_code);
			if (newResult != null)
				__result = newResult;
			return newResult == null;
		}

		private static void IsActive_Prefix(ref bool[] ___mActionState) {
			if (Instance != null)
				___mActionState = ExtendFlags(___mActionState, Instance.GetMaxAction());
		}

		/// <summary>
		/// Logs a message encountered by the PLib key binding system.
		/// </summary>
		/// <param name="message">The message.</param>
		internal static void LogKeyBind(string message) {
			Debug.LogFormat("[PKeyBinding] {0}", message);
		}

		/// <summary>
		/// Logs a warning encountered by the PLib key binding system.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogKeyBindWarning(string message) {
			Debug.LogWarningFormat("[PKeyBinding] {0}", message);
		}

		private static void QueueButtonEvent_Prefix(ref bool[] ___mActionState,
				KInputController.KeyDef key_def) {
			if (KInputManager.isFocused && Instance != null) {
				int max = Instance.GetMaxAction();
				key_def.mActionFlags = ExtendFlags(key_def.mActionFlags, max);
				___mActionState = ExtendFlags(___mActionState, max);
			}
		}

		private static void SetDefaultKeyBindings_Postfix() {
			Instance?.UpdateMaxAction();
		}

		public override Version Version => VERSION;

		/// <summary>
		/// Queued key binds which are resolved on load.
		/// </summary>
		private readonly IList<PAction> actions;

		/// <summary>
		/// The maximum action index of any custom action registered across all mods.
		/// </summary>
		private int maxAction;

		/// <summary>
		/// Creates a new action manager used to create and assign custom actions. Due to the
		/// timing of when the user's key bindings are loaded, all actions must be added in
		/// OnLoad().
		/// </summary>
		public PActionManager() {
			actions = new List<PAction>(8);
			maxAction = 0;
		}

		public override void Bootstrap(Harmony plibInstance) {
			// GameInputMapping.LoadBindings occurs after mods load but before the rest of the
			// PLib components are initialized
			plibInstance.Patch(typeof(GameInputMapping), nameof(GameInputMapping.
				LoadBindings), prefix: PatchMethod(nameof(AssignKeyBindings)));
		}

		/// <summary>
		/// Registers a PAction with the action manager.
		/// 
		/// This call should occur after PUtil.InitLibrary() during the mod OnLoad(). If called
		/// earlier, it may fail with InvalidOperationException, and if called later, the
		/// user's custom key bind (if applicable) will be discarded.
		/// </summary>
		/// <param name="identifier">The identifier for this action.</param>
		/// <param name="title">The action's title. If null, the default value from
		/// STRINGS.INPUT_BINDINGS.PLIB.identifier will be used instead.</param>
		/// <param name="binding">The default key binding for this action. If null, no key will
		/// be bound by default, but the user can set a key bind.</param>
		/// <returns>The action thus registered.</returns>
		public PAction CreateAction(string identifier, LocString title,
				PKeyBinding binding = null) {
			PAction action;
			RegisterForForwarding();
			int curIndex = GetSharedData(1);
			action = new PAction(curIndex, identifier, title, binding);
			SetSharedData(curIndex + 1);
			actions.Add(action);
			return action;
		}

		/// <summary>
		/// Returns the maximum length of the Action enum, including custom actions. If no
		/// actions are defined, returns NumActions - 1 since NumActions is reserved in the
		/// base game.
		/// 
		/// This value will not be accurate until all mods have loaded and key binds
		/// registered (AfterLayerableLoad or later such as BeforeDbInit).
		/// </summary>
		/// <returns>The maximum length required to represent all Actions.</returns>
		public int GetMaxAction() {
			int nActions = (int)PAction.MaxAction;
			return (maxAction > 0) ? (maxAction + nActions) : nActions - 1;
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			// GameInputMapping
			plibInstance.Patch(typeof(GameInputMapping), nameof(GameInputMapping.
				SetDefaultKeyBindings), postfix: PatchMethod(nameof(
				SetDefaultKeyBindings_Postfix)));

			// GameUtil
			plibInstance.Patch(typeof(GameUtil), nameof(GameUtil.GetKeycodeLocalized),
				prefix: PatchMethod(nameof(GetKeycodeLocalized_Prefix)));

			// KInputController
			plibInstance.PatchConstructor(typeof(KInputController.KeyDef), new Type[] {
				typeof(KKeyCode), typeof(Modifier)
			}, postfix: PatchMethod(nameof(CKeyDef_Postfix)));
			plibInstance.Patch(typeof(KInputController), nameof(KInputController.IsActive),
				prefix: PatchMethod(nameof(IsActive_Prefix)));
			plibInstance.Patch(typeof(KInputController), nameof(KInputController.
				QueueButtonEvent), prefix: PatchMethod(nameof(QueueButtonEvent_Prefix)));
		}

		public override void PostInitialize(Harmony plibInstance) {
			// Needs to occur after localization
			Strings.Add(GetBindingTitle(CATEGORY, "NAME"), PLibStrings.KEY_CATEGORY_TITLE);
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			if (allMods != null)
				foreach (var mod in allMods)
					mod.Process(1, null);
		}

		public override void Process(uint operation, object _) {
			int n = actions.Count;
			if (n > 0) {
				if (operation == 0)
					RegisterKeyBindings();
				else if (operation == 1) {
#if DEBUG
					LogKeyBind("Localizing titles for {0}".F(Assembly.GetExecutingAssembly().
						GetNameSafe() ?? "?"));
#endif
					foreach (var action in actions)
						Strings.Add(GetBindingTitle(CATEGORY, action.GetKAction().ToString()),
							action.Title);
				}
			}
		}

		private void RegisterKeyBindings() {
			int n = actions.Count;
			LogKeyBind("Registering {0:D} key bind(s) for mod {1}".F(n, Assembly.
				GetExecutingAssembly().GetNameSafe() ?? "?"));
			var currentBindings = new List<BindingEntry>(GameInputMapping.DefaultBindings);
			foreach (var action in actions) {
				var kAction = action.GetKAction();
				var binding = action.DefaultBinding;
				if (!FindKeyBinding(currentBindings, kAction)) {
					if (binding == null)
						binding = new PKeyBinding();
					// This constructor changes often enough to be worth detouring
					currentBindings.Add(NEW_BINDING_ENTRY.Invoke(CATEGORY, binding.
						GamePadButton, binding.Key, binding.Modifiers, kAction));
				}
			}
			GameInputMapping.SetDefaultKeyBindings(currentBindings.ToArray());
			UpdateMaxAction();
		}

		/// <summary>
		/// Updates the maximum action for this instance.
		/// </summary>
		private void UpdateMaxAction() {
			maxAction = GetSharedData(0);
		}
	}
}
