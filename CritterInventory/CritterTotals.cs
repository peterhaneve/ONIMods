using PeterHan.PLib;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Stores the total quantity of critters available and the quantity reserved for errands.
	/// 
	/// While this could be a struct, it would get copied a lot.
	/// </summary>
	sealed class CritterTotals {
		/// <summary>
		/// The number of critters available to be used (total minus reserved).
		/// </summary>
		public int Available {
			get {
				return Total - Reserved;
			}
		}
		/// <summary>
		/// Returns true if any critters were found.
		/// </summary>
		public bool HasAny {
			get {
				return Total > 0;
			}
		}
		/// <summary>
		/// The number of critters of this type "reserved" for Wrangle or Attack errands.
		/// </summary>
		public int Reserved { get; set; }
		/// <summary>
		/// The total number of critters of this type.
		/// </summary>
		public int Total { get; set; }

		public CritterTotals() {
			Reserved = 0;
			Total = 0;
		}
		/// <summary>
		/// Adds another critter total to this object.
		/// </summary>
		/// <param name="other">The critter totals to add.</param>
		public void Add(CritterTotals other) {
			Reserved += other.Reserved;
			Total += other.Total;
		}
		public override string ToString() {
			return "Total: {0:D} Reserved: {1:D}".F(Total, Reserved);
		}
	}
}
