/*
 * Copyright 2021 Peter Han
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

using PeterHan.PLib;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.FinishTasks {
	/// <summary>
	/// Adapts the differing interfaces between Red Alert / Yellow Alert and the Telepad
	/// location between the DLC (per asteroid) and Vanilla (single asteroid).
	/// 
	/// TODO Vanilla/DLC code
	/// </summary>
	internal abstract class VanillaDLCAdapter {
		/// <summary>
		/// The instance to query for alert information.
		/// </summary>
		public static VanillaDLCAdapter Instance { get; private set; }

		/// <summary>
		/// Gets the default mingling cell for the specified colony.
		/// </summary>
		/// <param name="reference">The actor whose colony is to be queried.</param>
		/// <returns>The default target for mingling after tasks are done, or Grid.InvalidCell if none could be determined.</returns>
		public abstract int GetDefaultMingleCell(GameObject reference);

		/// <summary>
		/// Returns true if the specified colony is under normal working conditions.
		/// </summary>
		/// <param name="reference">The actor whose colony is to be queried.</param>
		/// <returns>true if that colony is NOT under Yellow Alert or Red Alert, or false otherwise.</returns>
		public abstract bool IsNormalCondition(GameObject reference);

		/// <summary>
		/// Cleans up the adapter instance at the end of the game.
		/// </summary>
		public static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// Initializes the adapter instance at the start of the game.
		/// </summary>
		public static void InitInstance() {
			var clusterManager = PPatchTools.GetTypeSafe("ClusterManager");
			if (clusterManager == null)
				Instance = new VanillaAdapter();
			else
				Instance = new DLCAdapter();
		}

		/// <summary>
		/// Instantiated only on vanilla.
		/// </summary>
		private sealed class VanillaAdapter : VanillaDLCAdapter {
			private delegate GameObject GetTelepad();

			// Vanilla: GameObject GameUtil.GetTelepad(void)
			private readonly GetTelepad getTelepad = typeof(GameUtil).Detour<GetTelepad>(
				nameof(GameUtil.GetTelepad));

			public override int GetDefaultMingleCell(GameObject reference) {
				var pod = getTelepad.Invoke();
				return (pod == null) ? Grid.InvalidCell : Grid.PosToCell(pod);
			}

			public override bool IsNormalCondition(GameObject reference) {
				// Ignore reference, single colony
				var alertManager = VignetteManager.Instance.Get();
				return alertManager == null || (!alertManager.IsRedAlert() && !alertManager.
					IsYellowAlert());
			}
		}

		/// <summary>
		/// Instantiated only on the DLC. To make this simpler, it can only be compiled against
		/// DLC libraries, but will run on vanilla as the foreign type is not referenced if
		/// the DLC is not installed.
		/// </summary>
		private sealed class DLCAdapter : VanillaDLCAdapter {
			private delegate GameObject GetTelepad(int id);

			private delegate int GetWorldID(GameObject reference);

			// DLC: int ClusterUtil.GetMyWorldId(this GameObject)
			private readonly GetWorldID getWorldID;

			// DLC: GameObject GameUtil.GetTelepad(int)
			private readonly GetTelepad getTelepad;

			internal DLCAdapter() {
				// Extension method is defined on the util class
				getWorldID = PPatchTools.GetTypeSafe("ClusterUtil")?.Detour<GetWorldID>(
					"GetMyWorldId");
				if (getWorldID == null)
					PUtil.LogError("Unable to find method ClusterUtil.GetMyWorldId!");
				else
					getTelepad = typeof(GameUtil).Detour<GetTelepad>(nameof(GameUtil.
						GetTelepad));
			}

			public override int GetDefaultMingleCell(GameObject reference) {
				GameObject pod = null;
				int id;
				if (reference != null && getTelepad != null && (id = getWorldID.Invoke(
						reference)) >= 0)
					pod = getTelepad.Invoke(id);
				return (pod == null) ? Grid.InvalidCell : Grid.PosToCell(pod);
			}

			public override bool IsNormalCondition(GameObject reference) {
				int id;
				bool normal = true;
#if SPACEDOUT
				if (reference != null && getWorldID != null && (id = getWorldID.Invoke(
						reference)) >= 0) {
					var am = ClusterManager.Instance.GetWorld(id);
					normal = !am.IsRedAlert() && !am.IsYellowAlert();
				}
#endif
				return normal;
			}
		}

		/// <summary>
		/// Wraps a particular asteroid's GetRedAlert and GetYellowAlert methods.
		/// </summary>
		internal sealed class AlertWrapper {
			// DLC: bool WorldContainer.IsRedAlert()
			private delegate bool IsAlert();

			private readonly IsAlert isRedAlert;

			private readonly IsAlert isYellowAlert;

			public AlertWrapper(object worldContainer) {
				if (worldContainer == null)
					throw new ArgumentNullException(nameof(worldContainer));
				var wcType = worldContainer.GetType();
				var isRed = wcType.GetMethodSafe("IsRedAlert", false, typeof(bool));
				if (isRed != null)
					isRedAlert = Delegate.CreateDelegate(wcType, worldContainer, isRed,
						false) as IsAlert;
				var isYellow = wcType.GetMethodSafe("IsYellowAlert", false, typeof(bool));
				if (isYellow != null)
					isYellowAlert = Delegate.CreateDelegate(wcType, worldContainer, isYellow,
						false) as IsAlert;
			}

			public bool IsRedAlert() {
				return isRedAlert?.Invoke() ?? false;
			}

			public bool IsYellowAlert() {
				return isYellowAlert?.Invoke() ?? false;
			}
		}
	}
}
