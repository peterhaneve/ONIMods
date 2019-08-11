using UnityEngine;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// A marker class used to annotate additional information regarding the critter
	/// information to be displayed by a ResourceEntry object.
	/// </summary>
	public sealed class CritterResourceInfo : MonoBehaviour {
		/// <summary>
		/// The critter type this ResourceEntry will show.
		/// </summary>
		public CritterType CritterType { get; set; }
	}
}
