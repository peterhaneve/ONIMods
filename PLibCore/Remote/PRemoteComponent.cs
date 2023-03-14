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
using System;
using System.Reflection;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Delegates calls to forwarded components in other assemblies.
	/// </summary>
	internal sealed class PRemoteComponent : PForwardedComponent {
		/// <summary>
		/// The prototype used for delegates to remote Initialize.
		/// </summary>
		private delegate void InitializeDelegate(Harmony instance);

		/// <summary>
		/// The prototype used for delegates to remote Process.
		/// </summary>
		private delegate void ProcessDelegate(uint operation, object args);

		protected override object InstanceData {
			get {
				return getData?.Invoke();
			}
			set {
				setData?.Invoke(value);
			}
		}

		public override Version Version {
			get {
				return version;
			}
		}

		/// <summary>
		/// Points to the component's version of Bootstrap.
		/// </summary>
		private readonly InitializeDelegate doBootstrap;

		/// <summary>
		/// Points to the component's version of Initialize.
		/// </summary>
		private readonly InitializeDelegate doInitialize;

		/// <summary>
		/// Points to the component's version of PostInitialize.
		/// </summary>
		private readonly InitializeDelegate doPostInitialize;

		/// <summary>
		/// Gets the component's data.
		/// </summary>
		private readonly Func<object> getData;

		/// <summary>
		/// Runs the processing method of the component.
		/// </summary>
		private readonly ProcessDelegate process;

		/// <summary>
		/// Sets the component's data.
		/// </summary>
		private readonly Action<object> setData;

		/// <summary>
		/// The component's version.
		/// </summary>
		private readonly Version version;

		/// <summary>
		/// The wrapped instance from the other mod.
		/// </summary>
		private readonly object wrapped;

		internal PRemoteComponent(object wrapped) {
			this.wrapped = wrapped ?? throw new ArgumentNullException(nameof(wrapped));
			if (!PPatchTools.TryGetPropertyValue(wrapped, nameof(Version), out Version
					version))
				throw new ArgumentException("Remote component missing Version property");
			this.version = version;
			// Initialize
			var type = wrapped.GetType();
			doInitialize = type.CreateDelegate<InitializeDelegate>(nameof(PForwardedComponent.
				Initialize), wrapped, typeof(Harmony));
			if (doInitialize == null)
				throw new ArgumentException("Remote component missing Initialize");
			// Bootstrap
			doBootstrap = type.CreateDelegate<InitializeDelegate>(nameof(PForwardedComponent.
				Bootstrap), wrapped, typeof(Harmony));
			doPostInitialize = type.CreateDelegate<InitializeDelegate>(nameof(
				PForwardedComponent.PostInitialize), wrapped, typeof(Harmony));
			getData = type.CreateGetDelegate<object>(nameof(InstanceData), wrapped);
			setData = type.CreateSetDelegate<object>(nameof(InstanceData), wrapped);
			process = type.CreateDelegate<ProcessDelegate>(nameof(PForwardedComponent.
				Process), wrapped, typeof(uint), typeof(object));
		}

		public override void Bootstrap(Harmony plibInstance) {
			doBootstrap?.Invoke(plibInstance);
		}

		internal override object DoInitialize(Harmony plibInstance) {
			doInitialize.Invoke(plibInstance);
			return wrapped;
		}

		public override Assembly GetOwningAssembly() {
			return wrapped.GetType().Assembly;
		}

		public override void Initialize(Harmony plibInstance) {
			DoInitialize(plibInstance);
		}

		public override void PostInitialize(Harmony plibInstance) {
			doPostInitialize?.Invoke(plibInstance);
		}

		public override void Process(uint operation, object args) {
			process?.Invoke(operation, args);
		}

		public override string ToString() {
			return "PRemoteComponent[ID={0},TargetType={1}]".F(ID, wrapped.GetType().
				AssemblyQualifiedName);
		}
	}
}
