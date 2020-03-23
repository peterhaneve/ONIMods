# PLib

PLib is Peter's library for making mods. All mods in this repository depend on PLib via [ILMerge](https://github.com/dotnet/ILMerge).

## Obtaining

### Source

PLib can be checked out and compiled from source (tested on Visual Studio Community 2019). Make sure to populate the DLL files from the game in /lib.

### Binary

DLL releases for major versions are available in the [releases](https://github.com/peterhaneve/ONIMods/releases) page.
The lightweight PLib Options library (which includes only the options portion of PLib, and does not perform any forwarding) is also available in the releases section.

### NuGet

PLib is available as a [NuGet package](https://www.nuget.org/packages/PLib/).

## Usage

PLib must be included with mods that depend on it.
The easiest way to do this is to use ILMerge and add the PLib project or DLL as a reference in the mod project.
ILMerge is available as a [NuGet package](https://www.nuget.org/packages/ilmerge) and is best used as a post-build command.
Suggested command:
```powershell
"$(ILMergeConsolePath)" /ndebug /out:$(TargetName)Merged.dll $(TargetName).dll PLib.dll  /targetplatform:v4,C:\Windows\Microsoft.NET\Framework64\v4.0.30319
```

This helps ensure that each mod gets the version of PLib that it was built against, reducing the risk of breakage due to PLib changes.
Note that if using ILMerge, all dependencies of your mod *and PLib* must be added as references to the project.
PLib can also be packaged separately as a DLL with an individual mod.

Some parts of PLib need to be patched only once, or rely on having the latest version.
To handle this problem, PLib uses *auto-superseding*, which only loads and patches those portions of PLib after all mods have loaded, using the latest version of PLib on the system in any mod.
The accessor `PVersion.IsLatestVersion` can be used to determine if the version of PLib in a particular mod is the latest one on the system.
This is not a guarantee that the PLib instance in a particular mod is the loaded instance if multiple mods have this latest version.

### Initialization

Initialize PLib by calling `PUtil.InitLibrary(bool)` in `OnLoad`. PLib *must* be initialized before using most of PLib functionality.

It will emit your mod's `AssemblyFileVersion` to the log. Using the `AssemblyVersion` instead is discouraged, because changing `AssemblyVersion` breaks any explicit references to the assembly by name. (Ever wonder why .NET 3.5 still uses the .NET 2.0 version string?)

## Options

Provides utilities for reading and writing config files, as well as editing configs in-game via the mod menu.

#### Reading/writing config files

To read, use `PLib.Options.POptions.ReadSettings<T>()` where T is the type that the config file will be deserialized to. By default, PLib assumes the config file is located in the mod assembly directory and is named `config.json`.

To write, use `PLib.Options.POptions.WriteSettings<T>(T settings)`, where again T is the settings type.

#### Registering for the config screen

PLib.Options adds configuration menus to the Mods screen for mods that are registered.
Register a mod by invoking `POptions.RegisterOptions(Type settingtype)` in `OnLoad`.
The argument should be the type of the class the mod uses for its options, and must be JSON serializable.
`Newtonsoft.Json` is bundled with the game and can be referenced.

The class used for mod options can also contain a `PLib.ModInfo(string title, [string url=""], [string image=""])` annotation to display additional mod information.
The title overrides the default mod title in the options dialog only.
If a valid localization string key name is used for `title` (such as `STRINGS.YOURMOD.OPTIONS.TITLE`), the localized value of that string from the strings database is used as the display text.
The URL can be used to specify a custom website for the mod's home page; if left empty, it defaults to the Steam Workshop page for the mod.
The image, if specified, will attempt to load a preview image (best size is 192x192) with that name from the mod's data folder and display it in the settings dialog.

Fields must be a property, not a member, and should be annotated with `PLib.Option(string displaytext, [string tooltip=""])` to be visible in the mod config menu.
Currently supported types are: `int`, `float`, `string`, `bool`, and `Enum`.
If a property is a read-only `System.Action`, a button will be created that will execute the returned action if clicked.
If a property is of type `LocText`, no matter what it returns, the text in `displaytext` will be displayed as a full-width label with no input field.
If a valid localization string key name is used for `displaytext` (such as `STRINGS.YOURMOD.OPTIONS.YOUROPTION`), the localized value of that string from the strings database is used as the display text.

#### Categories

The optional third parameter of `Option` allows setting a custom category for the option to group related options together.
The category name is displayed as the title for the section.
If a valid localization string key name is used for the category (such as `STRINGS.YOURMOD.OPTIONS.YOURCATEGORY`), the localized value of that string from the strings database is used as the title.

#### Range limits

`int` and `float` options can have validation in the form of a range limit.
Annotate the property with `PLib.Limit(double min, double max)`.
Note that users can still enter values outside of the range manually in the configuration file.

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

## Gameplay

### Actions

Register actions by using `PAction.Register(string, LocString, PKeyBinding)` in `OnLoad`.
The identifier should be unique to the action used and should include the mod name to avoid conflicts with other mods.
If multiple mods register the same action identifier, only the first will receive a valid `PAction`.
The returned `PAction` object has a `GetKAction` method which can be used to retrieve an `Action` that works in standard Klei functions.
The `PKeyBinding` is used to specify the default key binding.

Note that the game can change the values in the `Action` enum. Instead of using the built-in `Action.NumActions` to denote "no action", consider using `PAction.MaxAction` instead which will use the correct value at runtime if necessary.

### Lighting

Register lighting types by using `PLightShape.Register(string, CastLight)` in `OnLoad`.
The identifier should be unique to the light pattern that will be registered.
If multiple mods register the same lighting type identifier, all will receive a valid `PLightShape` object but only the first mod loaded will be used to render it.
The returned `PLightShape` object has a `GetKLightShape` method which can be used to retrieve a `LightShape` that works in standard `Light2D` functions.
The `CastLight` specifies a callback in the mod that can handle drawing the light. It needs the signature
```c
void CastLight(GameObject source, LightingArgs args);
```
The mod will receive the source of the light (with the `Light2D` component) and an object encapsulating the lighting arguments.
See the `LightingArgs` class for more details.

## User Interface

### Translations

Register a mod for translation by using `PLocalization.Register()` in `OnLoad`.
All classes in the mod assembly with `public static` `LocString` fields will be eligible for translation.
Translation files need to be placed in the `translations` folder in the mod directory, named as the target language code (*zh-CN* for example) and ending with the `.po` extension.
Note that the translation only occurs after all mods load, so avoid referencing the `LocString` fields during class initialization or `OnLoad` as they may not yet be localized at that time.

### Codex Entries

Register a mod for codex loading by using `PCodex.RegisterCreatures()` and/or `PCodex.RegisterPlants()`.
The codex files will be loaded using the same structure as the base game: a `codex` folder must exist in the mod directory, with `Creatures` and `Plants` subfolders containing the codex data.

### Side Screens

Add a side screen class in a postfix patch on `DetailsScreen.OnPrefabInit` using `PUIUtils.AddSideScreenContent<T>()`.
The type parameter should be a custom class which extends `SideScreenContent`.
The optional argument can be used to set an existing UI `GameObject` as the UI to be displayed, either created using PLib UI or custom creation.

If the argument is `null`, create the UI in the `OnPrefabInit` of the side screen content class, and use `AddTo(gameObject, 0)` on the root PPanel to add it to the side screen content.
A reference to the UI `GameObject` created by `AddTo` should also be stored in the `ContentContainer` property of the side screen content class.
Note that `SetTarget` is called on the very first object selected *before* `OnPrefabInit` runs for the first time.
Make sure that this case is handled in code, and that `OnPrefabInit` refreshes the UI after it is built to match the current target object.

### Other

PLib contains a comprehensive UI library for making user interfaces dynamically at runtime. Consult the XML documentation for more details.
