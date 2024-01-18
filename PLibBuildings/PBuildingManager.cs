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

using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// Manages PLib buildings to break down PBuilding into a more reasonable sized class.
	/// </summary>
	public sealed class PBuildingManager : PForwardedComponent {
		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static PBuildingManager Instance { get; private set; }

		/// <summary>
		/// Immediately adds an <i>existing</i> building ID to an existing technology ID in the
		/// tech tree.
		/// 
		/// Do <b>not</b> use this method on buildings registered through PBuilding as they
		/// are added automatically.
		/// 
		/// This method must be used in a Db.Initialize postfix patch or RunAt.AfterDbInit
		/// PPatchManager method/patch.
		/// </summary>
		/// <param name="tech">The technology tree node ID.</param>
		/// <param name="id">The building ID to add to that node.</param>
		public static void AddExistingBuildingToTech(string tech, string id) {
			if (string.IsNullOrEmpty(tech))
				throw new ArgumentNullException(nameof(tech));
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			var technology = Db.Get().Techs?.TryGet(id);
			if (technology != null)
				technology.unlockedItemIDs?.Add(tech);
		}

		private static void CreateBuildingDef_Postfix(BuildingDef __result, string anim,
				string id) {
			var animFiles = __result?.AnimFiles;
			if (animFiles != null && animFiles.Length > 0 && animFiles[0] == null)
				Debug.LogWarningFormat("(when looking for KAnim named {0} on building {1})",
					anim, id);
		}

		private static void CreateEquipmentDef_Postfix(EquipmentDef __result, string Anim,
				string Id) {
			var anim = __result?.Anim;
			if (anim == null)
				Debug.LogWarningFormat("(when looking for KAnim named {0} on equipment {1})",
					Anim, Id);
		}

		/// <summary>
		/// Logs a message encountered by the PLib building system.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogBuildingDebug(string message) {
			Debug.LogFormat("[PLibBuildings] {0}", message);
		}

		private static void LoadGeneratedBuildings_Prefix() {
			Instance?.AddAllStrings();
		}

		/// <summary>
		/// The buildings which need to be registered.
		/// </summary>
		private readonly ICollection<PBuilding> buildings;

		public override Version Version => VERSION;

		/// <summary>
		/// Creates a building manager to register PLib buildings.
		/// </summary>
		public PBuildingManager() {
			buildings = new List<PBuilding>(16);
		}

		/// <summary>
		/// Adds the strings for every registered building in all mods to the database.
		/// </summary>
		private void AddAllStrings() {
			InvokeAllProcess(0, null);
		}

		/// <summary>
		/// Adds the strings for each registered building in this mod to the database.
		/// </summary>
		private void AddStrings() {
			int n = buildings.Count;
			if (n > 0) {
				LogBuildingDebug("Register strings for {0:D} building(s) from {1}".F(n,
					Assembly.GetExecutingAssembly().GetNameSafe() ?? "?"));
				foreach (var building in buildings)
					if (building != null) {
						building.AddStrings();
						building.AddPlan();
					}
			}
		}

		/// <summary>
		/// Adds the techs for every registered building in all mods to the database.
		/// </summary>
		private void AddAllTechs() {
			InvokeAllProcess(1, null);
		}
		
		/// <summary>
		/// Adds the techs for each registered building in this mod to the database.
		/// </summary>
		private void AddTechs() {
			int n = buildings.Count;
			if (n > 0) {
				LogBuildingDebug("Register techs for {0:D} building(s) from {1}".F(n,
					Assembly.GetExecutingAssembly().GetNameSafe() ?? "?"));
				foreach (var building in buildings)
					if (building != null) {
						building.AddTech();
					}
			}
		}

		/// <summary>
		/// Registers a building to properly display its name, description, and tech tree
		/// entry. PLib must be initialized using InitLibrary before using this method. Each
		/// building should only be registered once, either in OnLoad or a post-load patch.
		/// </summary>
		/// <param name="building">The building to register.</param>
		public void Register(PBuilding building) {
			if (building == null)
				throw new ArgumentNullException(nameof(building));
			RegisterForForwarding();
			// Must use object as the building table type
			buildings.Add(building);
#if DEBUG
			PUtil.LogDebug("Registered building: {0}".F(building.ID));
#endif
		}

		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			// Non essential, do not crash on fail
			try {
				plibInstance.Patch(typeof(BuildingTemplates), nameof(BuildingTemplates.
					CreateBuildingDef), postfix: PatchMethod(nameof(
					CreateBuildingDef_Postfix)));
				plibInstance.Patch(typeof(EquipmentTemplates), nameof(EquipmentTemplates.
					CreateEquipmentDef), postfix: PatchMethod(nameof(
					CreateEquipmentDef_Postfix)));
			} catch (Exception e) {
#if DEBUG
				PUtil.LogExcWarn(e);
#endif
			}
			plibInstance.Patch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.
				LoadGeneratedBuildings), prefix: PatchMethod(nameof(
				LoadGeneratedBuildings_Prefix)));

			// Avoid another Harmony patch by using PatchManager
			var pm = new PPatchManager(plibInstance);
			pm.RegisterPatch(RunAt.AfterDbInit, new BuildingTechRegistration());
		}

		public override void Process(uint operation, object _) {
			if (operation == 0)
				AddStrings();
			else if (operation == 1)
				AddTechs();
		}

		/// <summary>
		/// A Patch Manager patch which registers all PBuilding technologies.
		/// </summary>
		private sealed class BuildingTechRegistration : IPatchMethodInstance {
			public void Run(Harmony instance) {
				Instance?.AddAllTechs();
			}
		}
	}
}
