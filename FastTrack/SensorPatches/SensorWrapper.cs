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

using HarmonyLib;
namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// Wraps several sensors that were removed from the Sensors class, and only invokes them
	/// when required.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class SensorWrapper : KMonoBehaviour, ISlicedSim1000ms {
		/// <summary>
		/// The sensor used to find a balloon stand location.
		/// </summary>
		private BalloonStandCellSensor balloonSensor;

		/// <summary>
		/// The sensor used to find food to eat.
		/// </summary>
		private ClosestEdibleSensor edibleSensor;

		/// <summary>
		/// The sensor used to find a cell to Idle.
		/// </summary>
		private IdleCellSensor idleSensor;

		/// <summary>
		/// The sensor used to find a cell to Mingle.
		/// </summary>
		private MingleCellSensor mingleSensor;

		/// <summary>
		/// The sensor used to set up FetchManager state for ClosestEdibleSensor and
		/// PickupableSensor.
		/// </summary>
		private PathProberSensor pathSensor;

		/// <summary>
		/// The sensor used to find reachable debris items.
		/// </summary>
		private PickupableSensor pickupSensor;

		/// <summary>
		/// The sensor used to find a "safe" cell to move if becoming idle in a dangerous
		/// area.
		/// </summary>
		private SafeCellSensor safeSensor;

		/// <summary>
		/// The sensor used to find available bathrooms.
		/// </summary>
		private ToiletSensor toiletSensor;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private KPrefabID id;

		[MyCmpReq]
		private Sensors sensors;

		[MyCmpReq]
		private Klei.AI.Traits traits;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		protected override void OnCleanUp() {
			SlicedUpdaterSim1000ms<SensorWrapper>.instance.UnregisterUpdate1000ms(this);
			base.OnCleanUp();
		}

		protected override void OnSpawn() {
			var opts = FastTrackOptions.Instance;
			base.OnSpawn();
			if (opts.SensorOpts) {
				balloonSensor = sensors.GetSensor<BalloonStandCellSensor>();
				idleSensor = sensors.GetSensor<IdleCellSensor>();
				mingleSensor = sensors.GetSensor<MingleCellSensor>();
				safeSensor = sensors.GetSensor<SafeCellSensor>();
				toiletSensor = sensors.GetSensor<ToiletSensor>();
			}
			if (opts.PickupOpts) {
				edibleSensor = sensors.GetSensor<ClosestEdibleSensor>();
				pathSensor = sensors.GetSensor<PathProberSensor>();
				pickupSensor = sensors.GetSensor<PickupableSensor>();
			}
			SlicedUpdaterSim1000ms<SensorWrapper>.instance.RegisterUpdate1000ms(this);
		}

		/// <summary>
		/// Updates the sensors only once a second, as opposed to every frame.
		/// </summary>
		internal void RunUpdate() {
			if (id != null && !id.HasTag(GameTags.Dead)) {
				// The order of sensors matters here
				if (pathSensor != null)
					PathProberSensorUpdater.Update(pathSensor);
				if (pickupSensor != null)
					PickupableSensorUpdater.Update(pickupSensor);
				if (edibleSensor != null)
					ClosestEdibleSensorUpdater.Update(edibleSensor);
				if (balloonSensor != null && traits.HasTrait("BalloonArtist"))
					BalloonStandCellSensorUpdater.Update(balloonSensor);
				if (idleSensor != null)
					IdleCellSensorUpdater.Update(idleSensor);
				if (mingleSensor != null)
					MingleCellSensorUpdater.Update(mingleSensor);
				if (safeSensor != null)
					SafeCellSensorUpdater.Update(safeSensor);
				if (toiletSensor != null)
					ToiletSensorUpdater.Update(toiletSensor);
				// AssignableReachabilitySensor and BreathableAreaSensor are pretty cheap
			}
		}

		public void SlicedSim1000ms(float _) {
			RunUpdate();
		}
	}

#if false
	/// <summary>
	/// Applied to RationalAi.Instance to force update the sensors one time (when sensor
	/// optimizations are active) right before the first chore is chosen.
	/// Unfortunately this patch did not fix the idle on startup "problem". SafeCellSensor
	/// needs to be run while the dupe is idle...
	/// </summary>
	[HarmonyPatch(typeof(RationalAi.Instance), MethodType.Constructor,
		typeof(IStateMachineTarget))]
	public static class RationalAi_Instance_Constructor_Patch {
		internal static bool Prepare() {
			var opts = FastTrackOptions.Instance;
			return opts.SensorOpts || opts.PickupOpts;
		}

		/// <summary>
		/// Applied after the constructor runs.
		/// </summary>
		internal static void Postfix(RationalAi.Instance __instance) {
			var opts = FastTrackOptions.Instance;
			var sensors = __instance.GetComponent<Sensors>();
			if (sensors != null) {
				// Manually update the sensors required
				if (opts.PickupOpts) {
					var pathSensor = sensors.GetSensor<PathProberSensor>();
					var pickupSensor = sensors.GetSensor<PickupableSensor>();
					var edibleSensor = sensors.GetSensor<ClosestEdibleSensor>();
					if (pathSensor != null)
						PathProberSensorUpdater.Update(pathSensor);
					if (pickupSensor != null)
						PickupableSensorUpdater.Update(pickupSensor);
					if (edibleSensor != null)
						ClosestEdibleSensorUpdater.Update(edibleSensor);
				}
				// This might look like duplicate code with RunUpdate, but SensorWrapper is not
				// yet instantiated when these sensors need to run first
				if (opts.SensorOpts) {
					var balloonSensor = sensors.GetSensor<BalloonStandCellSensor>();
					var idleSensor = sensors.GetSensor<IdleCellSensor>();
					var mingleSensor = sensors.GetSensor<MingleCellSensor>();
					var safeSensor = sensors.GetSensor<SafeCellSensor>();
					var toiletSensor = sensors.GetSensor<ToiletSensor>();
					if (balloonSensor != null)
						BalloonStandCellSensorUpdater.Update(balloonSensor);
					if (idleSensor != null)
						IdleCellSensorUpdater.Update(idleSensor);
					if (mingleSensor != null)
						MingleCellSensorUpdater.Update(mingleSensor);
					if (safeSensor != null)
						SafeCellSensorUpdater.Update(safeSensor);
					if (toiletSensor != null)
						ToiletSensorUpdater.Update(toiletSensor);
				}
			}
		}
	}
#endif
}
