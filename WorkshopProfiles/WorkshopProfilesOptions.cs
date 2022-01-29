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

using PeterHan.PLib.Options;
using System.Collections.Generic;

namespace PeterHan.WorkshopProfiles {
	/// <summary>
	/// Contains the list of buildings to which Workshop Profiles will always add a profile
	/// screen.
	/// </summary>
	public sealed class WorkshopProfilesOptions : SingletonOptions<WorkshopProfilesOptions> {
		/// <summary>
		/// The building prefab IDs where an option will always be added.
		/// </summary>
		public List<string> AddToBuildings { get; set; }

		public WorkshopProfilesOptions() { }

		/// <summary>
		/// Sets the default buildings to force add if none are on the list.
		/// </summary>
		public void PopulateDefaults() {
			if (AddToBuildings == null)
				AddToBuildings = new List<string>(32) {
					RanchStationConfig.ID,
					FarmStationConfig.ID,
					PowerControlStationConfig.ID
				};
		}

		public override string ToString() {
			return "WorkshopProfilesOptions(" + AddToBuildings.ToString() + ")";
		}
	}
}
