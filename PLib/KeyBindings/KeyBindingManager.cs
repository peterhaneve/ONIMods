/*
 * Copyright 2019 Peter Han
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

using Harmony;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib {
	/// <summary>
	/// Manages the addition of key bindings to the game with a method that can be configured
	/// in the InputBindingsScreen.
	/// 
	/// This class is only patched in once thanks to PLibPatches.
	/// </summary>
	internal sealed class KeyBindingManager {
		/// <summary>
		/// The key binding manager in use, if instantiated. This is an acceptable singleton
		/// since InputBindingsScreen is also a singleton only instantiated via a prefab.
		/// </summary>
		internal static KeyBindingManager Instance { get; } = new KeyBindingManager();

		/// <summary>
		/// Allocates the UI Entry pool for the InputBindingsManager if not already created.
		/// </summary>
		/// <returns>The UI entry pool for key binding rows.</returns>
		private static UIPool<HorizontalLayoutGroup> AllocEntryPool(Traverse trInstance) {
			var entryPool = trInstance.GetField<UIPool<HorizontalLayoutGroup>>("entryPool");
			if (entryPool == null) {
				entryPool = new UIPool<HorizontalLayoutGroup>(trInstance.GetField<GameObject>(
					"entryPrefab")?.GetComponent<HorizontalLayoutGroup>());
				trInstance.SetField("entryPool", entryPool);
			}
			return entryPool;
		}

		/// <summary>
		/// The bindings registered in this way, keyed by the action ID.
		/// </summary>
		private readonly IDictionary<string, KeyAction> actions;

		/// <summary>
		/// If non-null, the action which the next keypress will rebind.
		/// </summary>
		private string pendingBind;

		private KeyBindingManager() {
			pendingBind = null;
			actions = new Dictionary<string, KeyAction>(32);
		}

		/// <summary>
		/// Adds all supplied key bindings to the key binding manager.
		/// </summary>
		/// <param name="bindings">The key bindings to add.</param>
		internal void AddKeyBindings(IList<object> bindings) {
			string targetName = typeof(PAction).FullName;
			foreach (object binding in bindings)
				if (binding.GetType().FullName == targetName) {
					var keyAction = KeyAction.Create(binding);
					if (keyAction != null) {
						string action = keyAction.Action;
						if (actions.ContainsKey(action))
							KeyUtils.LogKeyBindWarning("Duplicate action: " + action);
						else {
							KeyUtils.LogKeyBind("Registered binding: " + keyAction);
							actions.Add(action, keyAction);
						}
					}
				}
		}

		/// <summary>
		/// Binds a key to the currently pending action.
		/// </summary>
		/// <param name="code">The key pressed.</param>
		/// <param name="modifiers">The modifiers pressed with the key.</param>
		private void Bind(InputBindingsScreen instance, KKeyCode code, Modifier modifiers) {
			if (actions.TryGetValue(pendingBind, out KeyAction current)) {
				PKeyBinding ckey = current.CurrentKey, nkey = new PKeyBinding(code, modifiers,
					ckey.GamePadButton);
				ConfirmDialogScreen prefab;
				if (!nkey.Equals(ckey)) {
					// Key binding is actually different
					var trInstance = Traverse.Create(instance);
					KeyUtils.LogKeyBind("Binding key: " + nkey);
					string conflict = FindDuplicateBinding(trInstance, nkey);
					// Report the conflicting key
					if (conflict != null && (prefab = trInstance.GetField<ConfirmDialogScreen>(
							"confirmPrefab")) != null)
						trInstance.SetField("confirmDialog", PUIElements.ShowConfirmDialog(
							prefab.gameObject, instance.transform.gameObject, conflict));
					ckey.Key = nkey.Key;
					ckey.Modifiers = nkey.Modifiers;
					current.UpdateKeyBindings();
					pendingBind = null;
					// Rebuild display and key mapping
					Global.Instance.GetInputManager().RebindControls();
					trInstance.CallMethod("BuildDisplay");
				}
			}
		}

		/// <summary>
		/// Builds the InputBindingsScreen display if the PLib tag is selected.
		/// </summary>
		internal void BuildDisplay(InputBindingsScreen instance) {
			var trInstance = Traverse.Create(instance);
			// Lots of parameters, so just traverse them
			var screens = trInstance.GetField<List<string>>("screens");
			string curScreen = screens[trInstance.GetField<int>("activeScreen")];
			var screenTitle = trInstance.GetField<LocText>("screenTitle");
			var parent = trInstance.GetField<GameObject>("parent");
			var entryPool = AllocEntryPool(trInstance);
			int num = 0;
			// Clean the existing display
			trInstance.CallMethod("DestroyDisplay");
			if (curScreen == KeyUtils.CATEGORY) {
				var transform = instance.gameObject.transform;
				screenTitle.text = KeyUtils.CATEGORY_TITLE;
				// Add a row for each custom key bind
				foreach (var binding in actions.Values) {
					var row = entryPool.GetFreeElement(parent, true).gameObject;
					var button = BuildRow(row, binding.Title, out LocText keyLabel);
					keyLabel.text = binding.CurrentKey.GetBindingText();
					void onClick() {
						pendingBind = binding.Action;
						keyLabel.text = STRINGS.UI.FRONTEND.INPUT_BINDINGS_SCREEN.
							WAITING_FOR_INPUT;
					}
					row.AddOrGet<ActionContainer>().OnClick = onClick;
					button.onClick += onClick;
					row.transform.SetSiblingIndex(num++);
				}
			} else {
				screenTitle.text = KeyUtils.GetBindingTitle(curScreen, "NAME");
				// Add a row for each matching key binding
				foreach (var binding in GameInputMapping.KeyBindings)
					if (binding.mGroup == curScreen && binding.mRebindable) {
						var action = binding.mAction;
						var row = entryPool.GetFreeElement(parent, true).gameObject;
						// Calculate the title like the stock game does
						string actionKey = KeyUtils.GetBindingTitle(curScreen, action.
							ToString());
						var button = BuildRow(row, actionKey, out LocText keyLabel);
						keyLabel.text = trInstance.CallMethod<string>("GetBindingText",
							binding);
						bool ignoreConflicts = binding.mIgnoreRootConflics;
						void onClick() {
							// Re-create traverse to avoid holding reference to it
							var newTraverse = Traverse.Create(instance);
							newTraverse.SetField("waitingForKeyPress", true);
							newTraverse.SetField("actionToRebind", action);
							newTraverse.SetField("ignoreRootConflicts", ignoreConflicts);
							newTraverse.SetField("activeButton", button);
							keyLabel.text = STRINGS.UI.FRONTEND.INPUT_BINDINGS_SCREEN.
								WAITING_FOR_INPUT;
						}
						row.AddOrGet<ActionContainer>().OnClick = onClick;
						button.onClick += onClick;
						row.transform.SetSiblingIndex(num++);
					}
			}
		}

		/// <summary>
		/// Builds one row of the key binding screen.
		/// </summary>
		private KButton BuildRow(GameObject row, string title, out LocText keyLabel) {
			var rowTransform = row.transform;
			// Key bind title
			rowTransform.GetChild(0).GetComponentInChildren<LocText>().text = title;
			// Current key bind
			keyLabel = rowTransform.GetChild(1).GetComponentInChildren<LocText>();
			return row.GetComponentInChildren<KButton>();
		}

		/// <summary>
		/// Before destroying the display, cleans up the old button click actions.
		/// </summary>
		internal void DestroyDisplay(GameObject parent) {
			var transform = parent.transform;
			int n = transform.childCount;
			System.Action onClick;
			for (int i = 0; i < n; i++) {
				var row = transform.GetChild(i);
				var container = row.GetComponent<ActionContainer>();
				// Remove the action to allow the new action to be attached without conflicts
				if (container != null && (onClick = container.OnClick) != null) {
					row.GetComponentInChildren<KButton>().onClick -= onClick;
					container.OnClick = null;
				}
			}
		}

		/// <summary>
		/// Looks for duplicate key bindings in all key bindings. If duplicates are found,
		/// they are unbound.
		/// </summary>
		/// <param name="newBinding">The key binding to check.</param>
		/// <returns>null if no conflicts, or the error message otherwise.</returns>
		private string FindDuplicateBinding(Traverse trInstance, PKeyBinding newBinding) {
			string conflictText = null, keyText = newBinding.GetBindingText();
			// Search for a duplicate
			var kBinding = newBinding.FindKleiDuplicateBinding();
			Action kAction = kBinding.mAction;
			if (kAction != Action.Invalid) {
				string title = KeyUtils.GetBindingTitle(kBinding.mGroup, kAction.ToString());
				// Unbind Klei action
				KeyUtils.LogKeyBind("Found Klei key conflict: " + kAction);
				trInstance.CallMethod("Unbind", kAction);
				conflictText = string.Format(STRINGS.UI.FRONTEND.INPUT_BINDINGS_SCREEN.
					DUPLICATE, title, keyText);
			}
			var pBinding = FindPLibDuplicateBinding(newBinding);
			if (pBinding != null) {
				// Reassign to None
				KeyUtils.LogKeyBind("Found PLib key conflict: " + pBinding.Action);
				pBinding.CurrentKey.Key = KKeyCode.None;
				pBinding.CurrentKey.Modifiers = Modifier.None;
				pBinding.UpdateKeyBindings();
				conflictText = string.Format(STRINGS.UI.FRONTEND.INPUT_BINDINGS_SCREEN.
					DUPLICATE, pBinding.Title, keyText);
			}
			return conflictText;
		}

		/// <summary>
		/// Looks for duplicate key bindings in our key bindings.
		/// </summary>
		/// <param name="newBinding">The key binding to check.</param>
		/// <returns>null if no conflicts, or the conflicting key bind otherwise.</returns>
		private KeyAction FindPLibDuplicateBinding(PKeyBinding newBinding) {
			// Category and action are irrelevant here
			KeyAction result = null;
			foreach (var entry in actions.Values)
				if (entry.CurrentKey.Equals(newBinding)) {
					result = entry;
					break;
				}
			return result;
		}

		/// <summary>
		/// Cancels all key inputs.
		/// </summary>
		internal void HandleCancelInput() {
			foreach (var binding in actions.Values)
				if (binding.IsDown) {
					binding.TriggerKeyUp();
					binding.IsDown = false;
				}
		}

		/// <summary>
		/// Loads the PLib key bindings from the modded key binding JSON file.
		/// </summary>
		internal void LoadBindings() {
			string file = KeyUtils.BINDINGS_FILE;
			if (File.Exists(file))
				// TOCTTOU, but exceptions will also be caught and logged
				try {
					SerializedKeyBinding[] newBindings;
					using (JsonReader jr = new JsonTextReader(File.OpenText(file))) {
						var serializer = new JsonSerializer { MaxDepth = 4 };
						// Deserialize from stream avoids reading file text into memory
						newBindings = serializer.Deserialize<SerializedKeyBinding[]>(jr);
						jr.Close();
					}
					if (newBindings != null)
						// For bindings which match registered bindings, update values
						foreach (var binding in newBindings) {
							string action = binding.Action;
							if (actions.TryGetValue(action, out KeyAction userBinding)) {
								userBinding.LoadFrom(binding);
								userBinding.UpdateKeyBindings();
							}
						}
				} catch (IOException e) {
					PUtil.LogException(e);
				}
		}

		/// <summary>
		/// Suppresses keypress handling if we are binding new keys.
		/// </summary>
		internal bool OnKeyDown(KButtonEvent e) {
			bool consume = !string.IsNullOrEmpty(pendingBind);
			e.Consumed = consume;
			return !consume;
		}

		/// <summary>
		/// Queues a key event. If it matches a PLib key binding, its actions are invoked.
		/// </summary>
		internal void ProcessKeys(KInputController controller, Modifier modifiers) {
			if (!controller.IsGamepad)
				foreach (var binding in actions.Values) {
					var ckey = binding.CurrentKey;
					if (KeyUtils.IsKeyDown(ckey.Key) && modifiers == ckey.Modifiers) {
						binding.TriggerKeyDown();
						binding.IsDown = true;
					} else if (KeyUtils.IsKeyUp(ckey.Key) && binding.IsDown) {
						binding.TriggerKeyUp();
						binding.IsDown = false;
					}
				}
		}

		/// <summary>
		/// Resets all PLib key bindings to defaults.
		/// </summary>
		internal void Reset() {
			foreach (var binding in actions.Values) {
				PKeyBinding current = binding.CurrentKey, def = binding.DefaultKey;
				current.GamePadButton = def.GamePadButton;
				current.Key = def.Key;
				current.Modifiers = def.Modifiers;
			}
		}

		/// <summary>
		/// Saves the PLib key bindings to the modded key binding JSON file.
		/// </summary>
		internal void SaveBindings() {
			// Postfixed to default method so the directory must exist
			string file = KeyUtils.BINDINGS_FILE;
			int n = actions.Count, i = 0;
			try {
				if (n > 0) {
					var bindings = new SerializedKeyBinding[n];
					// Convert to serializable form
					foreach (var binding in actions.Values)
						bindings[i++] = binding.CreateBinding();
					File.WriteAllText(file, JsonConvert.SerializeObject(bindings));
				} else if (File.Exists(file))
					File.Delete(file);
			} catch (IOException e) {
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Handles key presses while actions are being rebound.
		/// </summary>
		internal void Update(InputBindingsScreen instance, KeyCode[] validKeys) {
			if (!string.IsNullOrEmpty(pendingBind)) {
				bool foundKey = false;
				var modifier = KeyUtils.GetModifiersDown();
				// Iterate all valid keys
				foreach (var keyCode in validKeys)
					if (Input.GetKeyDown(keyCode)) {
						Bind(instance, (KKeyCode)keyCode, modifier);
						foundKey = true;
						break;
					}
				if (!foundKey) {
					// Attempt binding to the mouse scroll wheel
					float axis = Input.GetAxis("Mouse ScrollWheel");
					var wheelCode = KKeyCode.None;
					if (axis < 0.0f)
						wheelCode = KKeyCode.MouseScrollDown;
					else if (axis > 0.0f)
						wheelCode = KKeyCode.MouseScrollUp;
					if (wheelCode != KKeyCode.None)
						Bind(instance, wheelCode, modifier);
				}
			}
		}
	}
}
