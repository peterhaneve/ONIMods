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
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// A library component that is forwarded across multiple assemblies, to allow only the
	/// latest version available on the system to run. Provides methods to marshal some
	/// objects across the assembly boundaries.
	/// </summary>
	public abstract class PForwardedComponent : IComparable<PForwardedComponent> {
		/// <summary>
		/// The default maximum serialization depth for marshaling data.
		/// </summary>
		public const int MAX_DEPTH = 8;

		/// <summary>
		/// The data stored in this object. It can be retrieved, with optional round trip
		/// serialization, by the instantiated version of this component.
		/// </summary>
		protected virtual object InstanceData { get; set; }

		/// <summary>
		/// The ID used by PLib for this component.
		/// 
		/// This method is non-virtual for a reason, as the ID is sometimes only available
		/// on methods of type object, so GetType().FullName is used directly there.
		/// </summary>
		public string ID {
			get {
				return GetType().FullName;
			}
		}

		/// <summary>
		/// The JSON serialization settings to be used if the Data is marshaled across
		/// assembly boundaries.
		/// </summary>
		protected JsonSerializer SerializationSettings { get; set; }

		/// <summary>
		/// Retrieves the version of the component provided by this assembly.
		/// </summary>
		public abstract Version Version { get; }

		/// <summary>
		/// Whether this object has been registered.
		/// </summary>
		private volatile bool registered;

		/// <summary>
		/// Serializes access to avoid race conditions when registering this component.
		/// </summary>
		private readonly object candidateLock;

		protected PForwardedComponent() {
			candidateLock = new object();
			InstanceData = null;
			registered = false;
			SerializationSettings = new JsonSerializer() {
				DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
				Culture = System.Globalization.CultureInfo.InvariantCulture,
				MaxDepth = MAX_DEPTH
			};
		}

		/// <summary>
		/// Called only on the first instance of a particular component to be registered.
		/// For some particular components that need very early patches, this call might be
		/// required to initialize state before the rest of the forwarded components are
		/// initialized. However, this call might occur on a version that is not the latest of
		/// this component in the system, or on an instance that will not be instantiated or
		/// initialized by the other callbacks.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching if necessary.</param>
		public virtual void Bootstrap(Harmony plibInstance) { }

		public int CompareTo(PForwardedComponent other) {
			return Version.CompareTo(other.Version);
		}

		/// <summary>
		/// Initializes this component. Only called on the version that is selected as the
		/// latest.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching if necessary.</param>
		/// <returns>The initialized instance.</returns>
		internal virtual object DoInitialize(Harmony plibInstance) {
			Initialize(plibInstance);
			return this;
		}

		/// <summary>
		/// Gets the data from this component as a specific type. Only works if the type is
		/// shared across all mods (in some shared assembly's memory space) such as types in
		/// System or the base game.
		/// </summary>
		/// <typeparam name="T">The data type to retrieve.</typeparam>
		/// <param name="defValue">The default value if the instance data is unset.</param>
		/// <returns>The data, or defValue if the instance data has not been set.</returns>
		public T GetInstanceData<T>(T defValue = default) {
			if (!(InstanceData is T outData))
				outData = defValue;
			return outData;
		}

		/// <summary>
		/// Gets the data from this component, serialized to the specified type. The data is
		/// retrieved from the base component, serialized with JSON, and reconstituted as type
		/// T in the memory space of the caller.
		/// 
		/// The target type must exist and be a [JsonObject] in both this assembly and the
		/// target component's assembly.
		/// 
		/// This method is somewhat slow and memory intensive, and should be used sparingly.
		/// </summary>
		/// <typeparam name="T">The data type to retrieve and into which to convert.</typeparam>
		/// <param name="defValue">The default value if the instance data is unset.</param>
		/// <returns>The data, or defValue if the instance data has not been set or cannot be serialized.</returns>
		public T GetInstanceDataSerialized<T>(T defValue = default) {
			var remoteData = InstanceData;
			T result = defValue;
			using (var buffer = new MemoryStream(1024)) {
				try {
					var writer = new StreamWriter(buffer, Encoding.UTF8);
					SerializationSettings.Serialize(writer, remoteData);
					writer.Flush();
					buffer.Position = 0L;
					var reader = new StreamReader(buffer, Encoding.UTF8);
					if (SerializationSettings.Deserialize(reader, typeof(T)) is T decoded)
						result = decoded;
				} catch (JsonException e) {
					PUtil.LogError("Unable to serialize instance data for component " + ID +
						":");
					PUtil.LogException(e);
					result = defValue;
				}
			}
			return result;
		}

		/// <summary>
		/// Gets the shared data between components with this ID as a specific type. Only works
		/// if the type is shared across all mods (in some shared assembly's memory space) such
		/// as types in System or the base game.
		/// </summary>
		/// <typeparam name="T">The data type to retrieve.</typeparam>
		/// <param name="defValue">The default value if the shared data is unset.</param>
		/// <returns>The data, or defValue if the shared data has not been set.</returns>
		public T GetSharedData<T>(T defValue = default) {
			if (!(PRegistry.Instance.GetSharedData(ID) is T outData))
				outData = defValue;
			return outData;
		}

		/// <summary>
		/// Gets the shared data between components with this ID, serialized to the specified
		/// type. The shared data is retrieved, serialized with JSON, and reconstituted as type
		/// T in the memory space of the caller.
		/// 
		/// The target type must exist and be a [JsonObject] in both this assembly and the
		/// target component's assembly.
		/// 
		/// This method is somewhat slow and memory intensive, and should be used sparingly.
		/// </summary>
		/// <typeparam name="T">The data type to retrieve and into which to convert.</typeparam>
		/// <param name="defValue">The default value if the shared data is unset.</param>
		/// <returns>The data, or defValue if the shared data has not been set or cannot be serialized.</returns>
		public T GetSharedDataSerialized<T>(T defValue = default) {
			var remoteData = PRegistry.Instance.GetSharedData(ID);
			T result = defValue;
			using (var buffer = new MemoryStream(1024)) {
				try {
					SerializationSettings.Serialize(new StreamWriter(buffer, Encoding.UTF8),
						remoteData);
					buffer.Position = 0L;
					if (SerializationSettings.Deserialize(new StreamReader(buffer,
							Encoding.UTF8), typeof(T)) is T decoded)
						result = decoded;
				} catch (JsonException e) {
					PUtil.LogError("Unable to serialize shared data for component " + ID +
						":");
					PUtil.LogException(e);
					result = defValue;
				}
			}
			return result;
		}

		/// <summary>
		/// Gets the assembly which provides this component.
		/// </summary>
		/// <returns>The assembly which owns this component.</returns>
		public virtual Assembly GetOwningAssembly() {
			return GetType().Assembly;
		}

		/// <summary>
		/// Initializes this component. Only called on the version that is selected as the
		/// latest. Component initialization order is undefined, so anything relying on another
		/// component cannot be used until PostInitialize.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching if necessary.</param>
		public abstract void Initialize(Harmony plibInstance);

		/// <summary>
		/// Invokes the Process method on all registered components of this type.
		/// </summary>
		/// <param name="operation">The operation to pass to Process.</param>
		/// <param name="args">The arguments to pass to Process.</param>
		protected void InvokeAllProcess(uint operation, object args) {
			var allComponents = PRegistry.Instance.GetAllComponents(ID);
			if (allComponents != null)
				foreach (var component in allComponents)
					component.Process(operation, args);
		}

		/// <summary>
		/// Gets a HarmonyMethod instance for manual patching using a method from this class.
		/// </summary>
		/// <param name="name">The method name.</param>
		/// <returns>A reference to that method as a HarmonyMethod for patching.</returns>
		public HarmonyMethod PatchMethod(string name) {
			return new HarmonyMethod(GetType(), name);
		}

		/// <summary>
		/// Initializes this component. Only called on the version that is selected as the
		/// latest. Other components have been initialized when this method is called.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching if necessary.</param>
		public virtual void PostInitialize(Harmony plibInstance) { }

		/// <summary>
		/// Called on demand by the initialized instance to run processing in all other
		/// instances.
		/// </summary>
		/// <param name="operation">The operation to perform. The meaning of this parameter
		/// varies by component.</param>
		/// <param name="args">The arguments for processing.</param>
		public virtual void Process(uint operation, object args) { }

		/// <summary>
		/// Registers this component into the list of versions available for forwarding. This
		/// method is thread safe. If this component instance is already registered, it will
		/// not be registered again.
		/// </summary>
		/// <returns>true if the component was registered, or false if it was already registered.</returns>
		protected bool RegisterForForwarding() {
			bool result = false;
			lock (candidateLock) {
				if (!registered) {
					PUtil.InitLibrary(false);
					PRegistry.Instance.AddCandidateVersion(this);
					registered = result = true;
				}
			}
			return result;
		}

		/// <summary>
		/// Sets the shared data between components with this ID. Only works if the type is
		/// shared across all mods (in some shared assembly's memory space) such as types in
		/// System or the base game.
		/// </summary>
		/// <param name="value">The new value for the shared data.</param>
		public void SetSharedData(object value) {
			PRegistry.Instance.SetSharedData(ID, value);
		}
	}
}
