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
Suggested command:
```powershell
"$(ILMergeConsolePath)" /ndebug /out:$(TargetName)Merged.dll $(TargetName).dll PLib.dll /targetplatform:v2,C:/Windows/Microsoft.NET/Framework64/v2.0.50727
```

This helps ensure that each mod gets the version of PLib that it was built against, reducing the risk of breakage due to PLib changes.
However, some parts of PLib need to be patched only once, or rely on having the latest version.
To handle this problem, PLib uses *auto-superseding*, which only loads and patches those portions of PLib after all mods have loaded, using the latest version of PLib on the system in any mod.
Example log information showing this feature in action:
```
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

### Initialization

Initialize PLib by calling `PUtil.LogModInit()` in `OnLoad`. PLib *must* be initialized before using most of PLib functionality.

It will emit your mod's `AssemblyFileVersion` to the log. It is not `AssemblyVersion`, because  changing `AssemblyVersion` breaks any explicit references to the assembly by name. (Ever wonder why .NET 3.5 still uses the .NET 2.0 version string?)

### Options

Provides utilities for reading and writing config files, as well as editing configs in-game via the mod menu.

#### Reading/writing config files

To read, use `PLib.Options.POptions.ReadSettings<T>()` where T is the type that the config file will be deserialized to. By default, PLib assumes the config file is located in the mod assembly directory and is named `config.json`.

To write, use `PLib.Options.POptions.WriteSettings<T>(T settings)`, where again T is the settings type.

#### Registering for the config screen

PLib.Options automatically displays config menus for mods that are registered. You can register an mod by invoking `POptions.RegisterOptions(Type settingtype)` in `OnLoad`. The argument should be the type of the class your mod uses for its options, and must be JSON serializable. Newtonsoft.Json is bundled with the game and can be referenced.

Fields must be a property, not a member, and should be annotated with `PLib.Option(string displaytext, [string tooltip=""])` to be visible in the mod config menu. Currently supported types are: `int`, `float`, `string`, `bool`.

#### Range limits

`int` and `float` options can have validation in the form of a range limit. Annotating the property with `PLib.Limit(double min, double max)`.

#### Example

```cs
using Newtonsoft.Json;
using PeterHan;
using PeterHan.PLib.Options;

// ...

[JsonObject(MemberSerialization.OptIn)]
public class PrintingPodRechargeSettings
{
    [PLib.Option("Wattage", "How many watts you can use before exploding.")]
    [PLib.Limit(1, 50000)]
    [JsonProperty]
    public float Watts { get; set; }

    public PrintingPodRechargeSettings()
    {
        Watts = 10000f; // defaults to 10000, e.g. if the config doesn't exist
    }
}
```

```cs
using PeterHan;

// ...

public static class Mod_OnLoad
{
    public static void OnLoad()
    {
        PLib.PUtil.LogModInit();
        PLib.POptions.RegisterOptions(typeof(TestModSetting));
    }
}
```

This is how it looks in the mod menu:

![mod menu example screenshot](https://i.imgur.com/1S1i9ru.png)



### Actions

Register actions by using `PAction.Register(string, LocString, PKeyBinding)` in `OnLoad`.
The identifier should be unique to the action used and should include the mod name to avoid conflicts with other mods.
If multiple mods register the same action identifier, only the first will receive a valid `PAction`.
The returned `PAction` object has a `GetKAction` method which can be used to retrieve an `Action` that works in standard Klei functions.
The `PKeyBinding` is used to specify the default key binding.

### Lighting

Register lighting types by using `PLightShape.Register(string, CastLight)` in `OnLoad`.
The identifier should be unique to the light pattern that will be registered.
If multiple mods register the same lighting type identifier, all will receive a valid `PLightShape` object but only the first mod loaded will be used to render it.
The returned `PLightShape` object has a `GetKLightShape` method which can be used to retrieve a `LightShape` that works in standard `Light2D` functions.
The `CastLight` specifies a callback in your mod that can handle drawing the light. It needs the signature
```c
void CastLight(GameObject source, LightingArgs args);
```
The mod will receive the source of the light (with the `Light2D` component) and an object encapsulating the lighting arguments.
See the `LightingArgs` class for more details.