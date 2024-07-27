// This code was reused from "Better Farming Effects and Tweaks" by Sanchozz. It is
// available from https://github.com/SanchozzDeponianin/ONIMods/blob/master/LICENSE under
// the MIT license.

using PlantHandler = EventSystem.IntraObjectHandler<PeterHan.StockBugFix.PlantStatusMonitorFixed>;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Monitors plant status events and properly turns irrigation on and off.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class PlantStatusMonitorFixed : KMonoBehaviour {
		/// <summary>
		/// Invoked when a plant lifecycle-related event occurs.
		/// </summary>
		private static readonly PlantHandler OnEvent = new PlantHandler((component, _) => {
			component.dirty = true;
			component.QueueApplyModifier();
		});

		/// <summary>
		/// Subscribes to plant lifecycle change events. A reference counter is used so calls
		/// should be matched with Unsubscribe when the state machine leaves the proper state.
		/// </summary>
		/// <param name="smi">The state machine instance to be subscribed.</param>
		public static void Subscribe(StateMachine.Instance smi) {
			var monitor = smi.GetComponent<PlantStatusMonitorFixed>();
			if (monitor != null)
				monitor.Subscribe();
		}
		
		/// <summary>
		/// Unsubscribes from plant lifecycle change events.
		/// </summary>
		/// <param name="smi">The state machine instance to be subscribed.</param>
		public static void Unsubscribe(StateMachine.Instance smi) {
			var monitor = smi.GetComponent<PlantStatusMonitorFixed>();
			if (monitor != null)
				monitor.Unsubscribe();
		}

		public bool ShouldAbsorb {
			get {
				if (dirty) {
					UpdateAbsorb();
					dirty = false;
				}
				return shouldAbsorb;
			}
		}

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MySmiGet]
		private FertilizationMonitor.Instance fertilization;

		[MySmiGet]
		private PlantBranchGrower.Instance grower;

		[MyCmpGet]
		private Growing growing;

		[MySmiGet]
		private IrrigationMonitor.Instance irrigation;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		/// <summary>
		/// Whether the irrigation status needs to be updated.
		/// </summary>
		private bool dirty;

		/// <summary>
		/// A reference counter to enable subscription to plant events.
		/// </summary>
		private int subscribeCount;

		/// <summary>
		/// Is the plant actually growing and using fertilizer?
		/// </summary>
		private bool shouldAbsorb;

		/// <summary>
		/// Queues the update of plant modifiers after a sim update has passed.
		/// </summary>
		private SchedulerHandle updateHandle;

		internal PlantStatusMonitorFixed() {
			shouldAbsorb = true;
			dirty = true;
			subscribeCount = 0;
		}

		/// <summary>
		/// Turns the irrigation and fertilization monitors on or off according to the current
		/// plant state.
		/// </summary>
		private void ApplyModifier(object _) {
			if (!fertilization.IsNullOrStopped() && fertilization.IsInsideState(fertilization.
					sm.replanted.fertilized.absorbing))
				fertilization.StartAbsorbing();
			if (!irrigation.IsNullOrStopped() && irrigation.IsInsideState(irrigation.sm.
					replanted.irrigated.absorbing))
				irrigation.UpdateAbsorbing(true);
		}

		/// <summary>
		/// Unsubscribes from the required plant lifecycle events without checking the
		/// reference counter.
		/// </summary>
		private void ForceUnsubscribe() {
			Unsubscribe((int)GameHashes.Grow, OnEvent);
			Unsubscribe((int)GameHashes.Wilt, OnEvent);
			Unsubscribe((int)GameHashes.CropWakeUp, OnEvent);
			Unsubscribe((int)GameHashes.CropSleep, OnEvent);
		}

		/// <summary>
		/// Subscribes to the required plant lifecycle events.
		/// </summary>
		private void Subscribe() {
			if (subscribeCount++ == 0) {
				Subscribe((int)GameHashes.Grow, OnEvent);
				Subscribe((int)GameHashes.Wilt, OnEvent);
				Subscribe((int)GameHashes.CropWakeUp, OnEvent);
				Subscribe((int)GameHashes.CropSleep, OnEvent);
			}
		}

		protected override void OnCleanUp() {
			ForceUnsubscribe();
			subscribeCount = 0;
			if (updateHandle.IsValid)
				updateHandle.ClearScheduler();
			base.OnCleanUp();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			dirty = true;
		}

		/// <summary>
		/// Updates the plant modifiers after a sim tick has passed.
		/// </summary>
		private void QueueApplyModifier() {
			if (updateHandle.IsValid)
				updateHandle.ClearScheduler();
			updateHandle = GameScheduler.Instance.Schedule("PlantStatusUpdateFixed", 2.0f *
				UpdateManager.SecondsPerSimTick, ApplyModifier);
		}

		/// <summary>
		/// Unsubscribes from the required plant lifecycle events.
		/// </summary>
		private void Unsubscribe() {
			int count = subscribeCount;
			if (count > 0) {
				subscribeCount = --count;
				if (count <= 0)
					ForceUnsubscribe();
			}
		}

		/// <summary>
		/// Updates the plant absorption status.
		/// </summary>
		private void UpdateAbsorb() {
			if (growing != null) {
				bool harvestReady = growing.ReachedNextHarvest();
				int n;
				if (grower != null && harvestReady && (n = grower.CurrentBranchCount) > 0) {
					var branches = grower.GetExistingBranches();
					int branchesGrowing = 0;
					for (int i = 0; i < n; i++) {
						var go = branches[i];
						if (go != null && go.TryGetComponent(out Growing gr) && gr.
								IsGrowing() && !gr.ReachedNextHarvest())
							branchesGrowing++;
					}
					shouldAbsorb = branchesGrowing > 0 || n < grower.
						MaxBranchesAllowedAtOnce;
				} else
					shouldAbsorb = growing.IsGrowing() && !harvestReady;
			}
		}
	}
}
