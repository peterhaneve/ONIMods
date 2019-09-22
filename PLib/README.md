# PLib

PLib is Peter's library for making mods. All mods in this repository depend on PLib via [ILMerge](https://github.com/dotnet/ILMerge).

## Obtaining

### Source

PLib can be checked out and compiled from source (tested on Visual Studio Community 2019). Make sure to populate the DLL files from the game in /lib.

### Binary

DLL releases for major versions are available in the [releases](https://github.com/peterhaneve/ONIMods/releases) page.

## Usage

PLib should be included in your mod via ILMerge.
The easiest way to do this is to add the PLib project or DLL as a reference in your mod project, then to use ILMerge as a post-build command.
Suggested command: ```
"$(ILMergeConsolePath)" /ndebug /out:$(TargetName)Merged.dll $(TargetName).dll PLib.dll /targetplatform:v2,C:/Windows/Microsoft.NET/Framework64/v2.0.50727
```

This helps ensure that each mod gets the version of PLib that it was built against, reducing the risk of breakage due to PLib changes.
However, some parts of PLib need to be patched only once, or rely on having the latest version.
To handle this problem, PLib uses *auto-superseding*, which only loads and patches those portions of PLib after all mods have loaded, using the latest version of PLib on the system in any mod.
Example log information showing this feature in action: ```
[05:29:18.099] [1] [INFO] [PLibPatches] Candidate version 2.4.0.0 from DeselectNewMaterialsMerged
[05:29:18.100] [1] [INFO] [PLib] Mod DeselectNewMaterialsMerged initialized, version 1.0.0.0
[05:29:18.150] [1] [INFO] [PLibPatches] Candidate version 2.3.0.0 from FallingSandMerged
[05:29:18.150] [1] [INFO] [PLib] Mod FallingSandMerged initialized, version 1.2.0.0
[05:29:18.159] [1] [INFO] [PLibPatches] Candidate version 2.1.0.0 from BulkSettingsChangeMerged
[05:29:18.160] [1] [INFO] [PLib] Mod BulkSettingsChangeMerged initialized, version 1.2.0.0
[05:29:18.170] [1] [INFO] [PLibPatches] Candidate version 2.7.0.0 from SweepByTypeMerged
[05:29:18.171] [1] [INFO] [PLib] Mod SweepByTypeMerged initialized, version 1.2.0.0
[05:29:18.183] [1] [INFO] [PLib] Mod FastSaveMerged initialized, version 1.2.0.0
[05:29:18.185] [1] [INFO] [PLib/FastSaveMerged] Registered mod options class FastSaveOptions for FastSaveMerged
[05:29:18.195] [1] [INFO] [PLibPatches] Candidate version 2.0.0.0 from CritterInventoryMerged
[05:29:18.196] [1] [INFO] [PLib] Mod CritterInventoryMerged initialized, version 1.4.0.0
[05:29:18.203] [1] [INFO] [PLibPatches] Candidate version 2.5.0.0 from ClaustrophobiaMerged
[05:29:18.203] [1] [INFO] [PLib] Mod ClaustrophobiaMerged initialized, version 1.8.0.0
[05:29:18.216] [1] [INFO] [PLib/BulkSettingsChangeMerged] Registering 1 key binds
[05:29:18.343] [1] [INFO] [PLibPatches] Using version 2.7.0.0
```

The accessor `PVersion.IsLatestVersion` can be used to determine if the version of PLib in a particular mod is the latest one on the system.
This is not a guarantee that the PLib instance in a particular mod is the loaded instance if multiple mods have this latest version.
