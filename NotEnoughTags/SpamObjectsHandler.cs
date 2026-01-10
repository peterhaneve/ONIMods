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

#if DEBUG
using PeterHan.PLib.Core;
using PeterHan.PLib.Actions;
using UnityEngine;
using PeterHan.PLib.PatchManager;

namespace PeterHan.NotEnoughTags {
	/// <summary>
	/// Handles the Spam Objects action.
	/// </summary>
	internal sealed class SpamObjectsHandler : IInputHandler {
		/// <summary>
		/// Debug-only action to spam objects under the cursor to exhaust tagbits.
		/// </summary>
		private static PAction SpamObjectsAction;

		[PLibMethod(RunAt.AfterLayerableLoad)]
		internal static void AddSpamHandler() {
			KInputHandler.Add(Global.GetInputManager().GetDefaultController(),
				new SpamObjectsHandler(), 512);
		}

		internal static void PrepareSpamHandler(PPatchManager manager) {
			manager.RegisterPatchClass(typeof(SpamObjectsHandler));
			SpamObjectsAction = new PActionManager().CreateAction(
				"NotEnoughTags.SpamObjectsAction", "Spam objects under cursor",
				new PKeyBinding(KKeyCode.Y, Modifier.Ctrl));
		}

		/// <summary>
		/// Spams all the things!
		/// </summary>
		private static System.Collections.IEnumerator SpamObjectsRoutine(int cell) {
			Grid.CellToXY(cell, out int x, out int y);
			PUtil.LogDebug("Spawning objects at cell ({0:D}, {1:D})".F(x, y));
			var pos = Grid.CellToPosCBC(cell, Grid.SceneLayer.Ore);
			foreach (var prefab in Assets.Prefabs) {
				string id = prefab.PrefabTag.Name;
				if (prefab.HasTag(GameTags.MiscPickupable) || prefab.HasTag(GameTags.Edible) ||
						prefab.HasTag(GameTags.Seed) || prefab.HasTag(GameTags.
						CookingIngredient) || prefab.HasTag(GameTags.MedicalSupplies)) {
					PUtil.LogDebug("Spawning item {0}".F(id));
					GameUtil.KInstantiate(prefab.gameObject, pos, Grid.SceneLayer.Ore).
						SetActive(true);
					yield return null;
				} else if ((prefab.HasTag(GameTags.Creature) && id != ShockwormConfig.ID) ||
						prefab.HasTag(GameTags.Egg)) {
					PUtil.LogDebug("Spawning critter {0}".F(id));
					GameUtil.KInstantiate(prefab.gameObject, pos, Grid.SceneLayer.Creatures).
						SetActive(true);
					yield return null;
				}
			}
			// Chunks of all elements
			foreach (var element in ElementLoader.elements)
				if (!element.IsVacuum && element.id != SimHashes.Void) {
					float temp = 1.0f;
					PUtil.LogDebug("Spawning element {0}".F(element.name));
					if (element.lowTempTransition != null && element.lowTemp > temp)
						temp = element.lowTemp + 3.0f;
					element.substance.SpawnResource(pos, 1000.0f, temp, Sim.InvalidDiseaseIdx, 0);
					yield return null;
				}
		}

		public string handlerName => "Spam Objects Handler";

		public KInputHandler inputHandler { get; set; }

		/// <summary>
		/// The action that will trigger the spam.
		/// </summary>
		private readonly Action snapshotAction;

		internal SpamObjectsHandler() {
			var action = SpamObjectsAction;
			if (action != null)
				snapshotAction = action.GetKAction();
			else
				snapshotAction = PAction.MaxAction;
		}

		/// <summary>
		/// Fired when a key is pressed.
		/// </summary>
		/// <param name="e">The event that occurred.</param>
		public void OnKeyDown(KButtonEvent e) {
			var game = Game.Instance;
			if (e.TryConsume(snapshotAction) && game != null && game.SandboxModeActive)
				game.StartCoroutine(SpamObjectsRoutine(Grid.PosToCell(Camera.current.
					ScreenToWorldPoint(KInputManager.GetMousePos()))));
		}
	}
}
#endif
