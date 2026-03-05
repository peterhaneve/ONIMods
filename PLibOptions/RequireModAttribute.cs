
using PeterHan.PLib.Core;
using System;

namespace PeterHan.PLib.Options {
	/// An attribute placed on an option property for a class used as mod options in order to
	/// show or hide it for an arbitrary condition.. If the option is hidden, the value
	// currently in the options file is preserved unchanged when reading or writing.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true,
		Inherited = true)]
	public sealed class RequireModAttribute : Attribute {
		/// <summary>
		/// The Mod to check.
		/// </summary>
		public string ModStaticID { get; }

		/// <summary>
		/// Annotates an option field as requiring the specified Mod. The [Option]
		// attribute must also be present to be displayed at all.
		/// </summary>
		/// <param name="cond">The Mod required to be present
		public RequireModAttribute(string modStaticIDIn) {
			this.ModStaticID = modStaticIDIn;
		}

		public override string ToString() {
			return "RequireMod[ModStaticID={0}]".F(ModStaticID);
		}
	}
}
