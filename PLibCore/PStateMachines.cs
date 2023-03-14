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

using System;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Contains tools for dealing with state machines.
	/// </summary>
	public static class PStateMachines {
		/// <summary>
		/// Creates and initializes a new state. This method should be used in a postfix patch
		/// on InitializeStates if new states are to be added.
		/// </summary>
		/// <typeparam name="T">The state machine type.</typeparam>
		/// <typeparam name="I">The state machine Instance type.</typeparam>
		/// <param name="sm">The base state machine.</param>
		/// <param name="name">The state name.</param>
		/// <returns>The new state.</returns>
		public static GameStateMachine<T, I>.State CreateState<T, I>(
				this GameStateMachine<T, I> sm, string name)
				where T : GameStateMachine<T, I, IStateMachineTarget, object> where I :
				GameStateMachine<T, I, IStateMachineTarget, object>.GameInstance {
			var state = new GameStateMachine<T, I>.State();
			if (string.IsNullOrEmpty(name))
				name = "State";
			if (sm == null)
				throw new ArgumentNullException(nameof(sm));
			state.defaultState = sm.GetDefaultState();
			// Process any sub parameters
			sm.CreateStates(state);
			sm.BindState(sm.root, state, name);
			return state;
		}

		/// <summary>
		/// Creates and initializes a new state. This method should be used in a postfix patch
		/// on InitializeStates if new states are to be added.
		/// </summary>
		/// <typeparam name="T">The state machine type.</typeparam>
		/// <typeparam name="I">The state machine Instance type.</typeparam>
		/// <typeparam name="M">The state machine Target type.</typeparam>
		/// <param name="sm">The base state machine.</param>
		/// <param name="name">The state name.</param>
		/// <returns>The new state.</returns>
		public static GameStateMachine<T, I, M>.State CreateState<T, I, M>(
				this GameStateMachine<T, I, M> sm, string name) where M : IStateMachineTarget 
				where T : GameStateMachine<T, I, M, object> where I :
				GameStateMachine<T, I, M, object>.GameInstance {
			var state = new GameStateMachine<T, I, M>.State();
			if (string.IsNullOrEmpty(name))
				name = "State";
			if (sm == null)
				throw new ArgumentNullException(nameof(sm));
			state.defaultState = sm.GetDefaultState();
			// Process any sub parameters
			sm.CreateStates(state);
			sm.BindState(sm.root, state, name);
			return state;
		}

		/// <summary>
		/// Clears the existing Enter actions on a state.
		/// </summary>
		/// <param name="state">The state to modify.</param>
		public static void ClearEnterActions(this StateMachine.BaseState state) {
			if (state != null)
				state.enterActions.Clear();
		}

		/// <summary>
		/// Clears the existing Exit actions on a state.
		/// </summary>
		/// <param name="state">The state to modify.</param>
		public static void ClearExitActions(this StateMachine.BaseState state) {
			if (state != null)
				state.exitActions.Clear();
		}

		/// <summary>
		/// Clears the existing Transition actions on a state. Parameter transitions are not
		/// affected.
		/// </summary>
		/// <param name="state">The state to modify.</param>
		public static void ClearTransitions(this StateMachine.BaseState state) {
			if (state != null)
				state.transitions.Clear();
		}
	}
}
