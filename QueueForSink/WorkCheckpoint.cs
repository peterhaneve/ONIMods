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

using UnityEngine;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// A checkpoint component which prevents Duplicants from passing if a workable is in use
	/// and they could use it.
	/// </summary>
	public abstract class WorkCheckpoint<T> : KMonoBehaviour where T : Workable {
		// These fields are filled in automatically by KMonoBehaviour
#pragma warning disable CS0649
		[MyCmpReq]
		protected DirectionControl direction;
#pragma warning restore CS0649

		/// <summary>
		/// Whether the workable this checkpoint guards is currently in use.
		/// </summary>
		protected bool inUse;

		/// <summary>
		/// The current reaction.
		/// </summary>
		private WorkCheckpointReactable reactable;

		/// <summary>
		/// The workable which controls tasks.
		/// </summary>
		private T workable;

		/// <summary>
		/// Destroys the current reaction.
		/// </summary>
		private void ClearReactable() {
			if (reactable != null) {
				reactable.Cleanup();
				reactable = null;
			}
		}

		/// <summary>
		/// Creates a new reaction.
		/// </summary>
		private void CreateNewReactable() {
			reactable = new WorkCheckpointReactable(this);
		}

		/// <summary>
		/// Handles work events to keep the status of this workable.
		/// </summary>
		/// <param name="evt">The type of work event which occurred.</param>
		private void HandleWorkableAction(Workable _, Workable.WorkableEvent evt) {
			switch (evt) {
			case Workable.WorkableEvent.WorkStarted:
				inUse = true;
				break;
			case Workable.WorkableEvent.WorkCompleted:
			case Workable.WorkableEvent.WorkStopped:
				inUse = false;
				break;
			}
		}

		/// <summary>
		/// Called to see if a Duplicant shall not pass!
		/// </summary>
		/// <param name="reactor">The Duplicant to check.</param>
		/// <param name="direction">The X direction the Duplicant is moving.</param>
		/// <returns>true if the Duplicant must stop, or false if they can pass</returns>
		protected abstract bool MustStop(GameObject reactor, float direction);

		protected override void OnCleanUp() {
			base.OnCleanUp();
			ClearReactable();
			if (workable != null)
				workable.OnWorkableEventCB -= HandleWorkableAction;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			// Using MyCmpReq on generics was crashing
			workable = gameObject.GetComponent<T>();
			if (workable != null)
				workable.OnWorkableEventCB += HandleWorkableAction;
			CreateNewReactable();
		}

		/// <summary>
		/// A reaction which stops Duplicants in their tracks if they need to use a workable
		/// that is already in use.
		/// </summary>
		private sealed class WorkCheckpointReactable : Reactable {
			/// <summary>
			/// The parent work checkpoint.
			/// </summary>
			private readonly WorkCheckpoint<T> checkpoint;

			/// <summary>
			/// The animation to play while stopped.
			/// </summary>
			private readonly KAnimFile distractedAnim;

			/// <summary>
			/// The navigator of the Duplicant who is waiting.
			/// </summary>
			private Navigator reactorNavigator;

			internal WorkCheckpointReactable(WorkCheckpoint<T> checkpoint) : base(checkpoint.
					gameObject, "WorkCheckpointReactable", Db.Get().ChoreTypes.Checkpoint,
					1, 1) {
				this.checkpoint = checkpoint;
				distractedAnim = Assets.GetAnim("anim_idle_distracted_kanim");
				preventChoreInterruption = false;
			}

			protected override void InternalBegin() {
				reactorNavigator = reactor.GetComponent<Navigator>();
				// Animation to make them stand impatiently in line
				var controller = reactor.GetComponent<KBatchedAnimController>();
				controller.AddAnimOverrides(distractedAnim, 1f);
				controller.Play("idle_pre");
				controller.Queue("idle_default", KAnim.PlayMode.Loop);
				checkpoint.CreateNewReactable();
			}

			public override bool InternalCanBegin(GameObject newReactor, Navigator.
					ActiveTransition transition) {
				bool disposed = checkpoint?.workable == null;
				if (disposed)
					Cleanup();
				bool canBegin = !disposed && reactor == null;
				if (canBegin)
					canBegin = MustStop(newReactor, transition.x);
				return canBegin;
			}

			protected override void InternalCleanup() {
				reactorNavigator = null;
			}

			protected override void InternalEnd() {
				reactor?.GetComponent<KBatchedAnimController>().RemoveAnimOverrides(
					distractedAnim);
			}

			/// <summary>
			/// Returns whether a duplicant must stop and wait.
			/// </summary>
			/// <param name="dupe">The duplicant to check.</param>
			/// <param name="x">The X direction they are going.</param>
			/// <returns>true if they must wait, or false if they may pass.</returns>
			private bool MustStop(GameObject dupe, float x) {
				var dir = checkpoint.direction.allowedDirection;
				// Allow suffocating Duplicants to pass
				var suff = (dupe == null) ? null : dupe.GetSMI<SuffocationMonitor.Instance>();
				// Left is decreasing X, must be facing the correct direction
				return (dir == WorkableReactable.AllowedDirection.Any ||
					(dir == WorkableReactable.AllowedDirection.Left) == (x < 0.0f)) &&
					checkpoint.workable.GetWorker() != null && dupe != null && checkpoint.
					MustStop(dupe, x) && (suff == null || !suff.IsSuffocating());
			}

			public override void Update(float dt) {
				if (checkpoint == null || checkpoint.workable == null || reactorNavigator ==
						null)
					Cleanup();
				else {
					reactorNavigator.AdvancePath(false);
					if (!reactorNavigator.path.IsValid() || !MustStop(reactor,
							reactorNavigator.GetNextTransition().x))
						Cleanup();
				}
			}
		}
	}
}
