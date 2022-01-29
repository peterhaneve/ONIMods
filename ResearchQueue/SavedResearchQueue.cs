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

using KSerialization;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PeterHan.ResearchQueue {
	/// <summary>
	/// Saves the research queue in save games.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class SavedResearchQueue : KMonoBehaviour, ISaveLoadable {
#pragma warning disable IDE0044 // Add readonly modifier
		/// <summary>
		/// The saved research queue.
		/// </summary>
		[Serialize]
		private List<string> techQueue;
#pragma warning restore IDE0044 // Add readonly modifier

		internal SavedResearchQueue() {
			techQueue = new List<string>(16);
		}

		[OnSerializing]
		internal void OnSerializing() {
			// Push the research queue into this object
			var inst = Research.Instance;
			techQueue.Clear();
			if (inst != null)
				foreach (var techInst in inst.GetResearchQueue())
					techQueue.Add(techInst.tech.Id);
		}

		[OnDeserialized]
		internal void OnDeserialized() {
			// Copy the object into the research queue
			var inst = Research.Instance;
			var dbTechs = Db.Get().Techs;
			if (inst != null && techQueue != null) {
				Tech lastTech = null;
				inst.SetActiveResearch(null, true);
				// Add each tech in order
				foreach (var id in techQueue) {
					var tech = dbTechs.Get(id);
					if (tech != null) {
						ResearchQueuePatches.ADD_TECH(inst, tech);
						lastTech = tech;
#if DEBUG
						PUtil.LogDebug("Added tech to queue: {0}".F(tech.Name));
#endif
					}
				}
				// Restart the queue
				inst.SetActiveResearch(lastTech, false);
			}
		}
	}
}
