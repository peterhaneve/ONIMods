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

using System.Collections.Generic;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// An interface used for both local and remote PLib registry instances.
	/// </summary>
	public interface IPLibRegistry {
		/// <summary>
		/// Data shared between mods in key value pairs.
		/// </summary>
		IDictionary<string, object> ModData { get; }

		/// <summary>
		/// Adds a candidate version of a forwarded component.
		/// </summary>
		/// <param name="instance">The instance of the component to add.</param>
		void AddCandidateVersion(PForwardedComponent instance);

		/// <summary>
		/// Gets the latest version of a forwarded component of PLib (or another mod).
		/// </summary>
		/// <param name="id">The component ID to look up.</param>
		/// <returns>The latest version of that component, or a forwarded proxy of the
		/// component if functionality is provided by another mod.</returns>
		PForwardedComponent GetLatestVersion(string id);

		/// <summary>
		/// Gets the shared data for a particular component.
		/// </summary>
		/// <param name="id">The component ID that holds the data.</param>
		/// <returns>The shared data for components with that ID, or null if no component by
		/// that name was found, or if the data is unset.</returns>
		object GetSharedData(string id);

		/// <summary>
		/// Gets all registered forwarded components for the given ID.
		/// </summary>
		/// <param name="id">The component ID to look up.</param>
		/// <returns>All registered components with that ID, with forwarded proxies for any
		/// whose functionality is provided by another mod.</returns>
		IEnumerable<PForwardedComponent> GetAllComponents(string id);

		/// <summary>
		/// Sets the shared data for a particular component.
		/// </summary>
		/// <param name="id">The component ID that holds the data.</param>
		/// <param name="data">The new shared data value.</param>
		void SetSharedData(string id, object data);
	}
}
