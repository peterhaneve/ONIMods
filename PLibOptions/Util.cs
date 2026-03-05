
namespace PeterHan.PLib.Options;

public class Util {
	// Returns true if the specified mod is installed & enabled
	public static bool IsModEnabled(string staticID) {
		foreach(var mod in Global.Instance.modManager.mods) {
			if (mod.staticID == staticID && mod.IsActive()) {
				return true;
			}
		}

		return false;
	}
}
