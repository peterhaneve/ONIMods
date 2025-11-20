# PLib

PLib is Peter's library for making mods. All mods in this repository depend on PLib via [ILMerge](https://github.com/dotnet/ILMerge).

PLib 4.0 is now modular, allowing mods to only include and merge the components that they use, along with the much reduced PLib Core library.
PLib 4.0 is not backwards compatible with PLib 2.0 and 3.0, but both can be run side by side (if that was possible).
PLib 4.0 and up supports [Harmony 2.0 for the new game versions merging the vanilla game and Spaced Out! DLC](https://forums.kleientertainment.com/forums/topic/130712-oni-is-upgrading-to-harmony-20/), but PLib 3.0 and lower do not.

## Obtaining

### Source

PLib can be checked out and compiled from source (tested on Visual Studio Community 2019).
If Oxygen Not Included is installed in a different location, make a copy of `Directory.Build.Props.default` named `Directory.Build.Props.user` and update the game folder paths appropriately.

### Binary

DLL releases for major versions are available in the [releases](https://github.com/peterhaneve/ONIMods/releases) page.
**Only the complete library is available from NuGet, which is sufficient for most users.**
Individual components of the library are also available in the releases section.

### NuGet

PLib is available as a [NuGet package](https://www.nuget.org/packages/PLib/).

## Usage

PLib must be included with mods that depend on it.
The best method to do this is to use ILMerge or ILRepack and add the PLib project or DLL as a reference in the mod project.
ILMerge is available as a [NuGet package](https://www.nuget.org/packages/ilmerge) and is best used as a post-build command.
Suggested command:
```powershell
"$(ILMergeConsolePath)" /ndebug /out:$(TargetName)Merged.dll $(TargetName).dll PLib.dll /targetplatform:v4,C:\Windows\Microsoft.NET\Framework64\v4.0.30319
```

This helps ensure that each mod uses the version of PLib that it was built against, reducing the risk of breakage due to PLib changes.
Note that if using ILMerge, all dependencies of your mod *and PLib* must be added as references to the project.
Avoid merging Unity assemblies with the compiled DLL, and turn off *Copy Local* on all other library DLLs such as `0Harmony` and `Assembly-CSharp`.
PLib can also be packaged separately as a DLL with an individual mod, but on Mac and Linux this may lead to version issues.

Some parts of PLib need to be patched only once, or rely on having the latest version.
To handle this problem, PLib uses *forwarded components*, which only loads the components and patches that are in use, using the latest version of each component available across all installed mods.
Only one instance of each registered forwarded component is instantiated, even if multiple mods have the same version, although which one is used in case of a tie is unspecified.
Custom forwarded components in mod code can be implemented by subclassing `PeterHan.PLib.Core.PForwardedComponent`.

### Initialization

Initialize PLib by calling `PUtil.InitLibrary(bool)` in `OnLoad`.
PLib *must* be initialized before using most of PLib functionality, but instantiating most PLib components will now also initialize PLib if necessary.

It will emit the mod's `AssemblyFileVersion` to the log if the `bool` parameter is true, which can aid with debugging.
Using the `AssemblyVersion` instead is discouraged, because changing `AssemblyVersion` breaks any explicit references to the assembly by name.
(Ever wonder why .NET 3.5 still uses the .NET 2.0 version string?)

## Core

The `PLib.Core` component is required by all other PLib components.
It contains only a minimal patch manager and general utilities that do not require any patches at runtime, such as Detours, reflection utilities, and basic game helpers.

## User Interface

The `PLib.UI` component is used to create custom user interfaces, in the same style as the base game.
It requires `PLib.Core`.
For more details on the classes in this component, see the XML documentation.

### Side Screens

Add a side screen class in a postfix patch on `DetailsScreen.OnPrefabInit` using `PUIUtils.AddSideScreenContent<T>()`.
The type parameter should be a custom class which extends `SideScreenContent`.
The optional argument can be used to set an existing UI `GameObject` as the UI to be displayed, either created using PLib UI or custom creation.

If the argument is `null`, create the UI in the `OnPrefabInit` of the side screen content class, and use `AddTo(gameObject, 0)` on the root PPanel to add it to the side screen content.
A reference to the UI `GameObject` created by `AddTo` should also be stored in the `ContentContainer` property of the side screen content class.
Note that `SetTarget` is called on the very first object selected *before* `OnPrefabInit` runs for the first time.
Make sure that this case is handled in code, and that `OnPrefabInit` refreshes the UI after it is built to match the current target object.

## Options

The `PLib.Options` component provides utilities for reading and writing config files, as well as editing configs in-game via the mod menu.
It requires `PLib.UI` and `PLib.Core`.

#### Reading/writing config files

To read, use `PLib.Options.POptions.ReadSettings<T>()` where T is the type that the config file will be deserialized to.
In PLib 4.0, the type will be associated with the assembly that defines that type, not the calling assembly like PLib 2.0 and 3.0.
By default, PLib will place the config file in the mod assembly directory, named `config.json`, and will give each archived version its own configuration.

The `ConfigFile` attribute can be used to modify the name of the configuration file and enable auto-indenting to improve human readability.
If the `UseSharedConfigLocation` flag is set, the configuration file will be saved in a location that survives updating or reinstalling the mod; the name of the mod's primary assembly will be used for the folder name.

To write, use `PLib.Options.POptions.WriteSettings<T>(T settings)`, where again T is the settings type.

#### Registering for the config screen

PLib.Options adds configuration menus to the Mods screen for mods that are registered.
Register a mod by using `POptions.RegisterOptions(UserMod2, Type settingsType)` in `OnLoad`.
Creating a new `POptions` instance is required to use this method, but only one `POptions` instance should be created per mod.

The argument should be the type of the class the mod uses for its options, and must be JSON serializable.
`Newtonsoft.Json` is bundled with the game and can be referenced.

The class used for mod options can also contain a `ModInfo([string url=""], [string image=""])` annotation to display additional mod information.
**Note that the title from PLib 2.0 and 3.0 is no longer part of this attribute**, as this functionality has been moved to the Klei `mod.yaml` file.
The URL can be used to specify a custom website for the mod's home page; if left empty, it defaults to the Steam Workshop page for the mod.
The image, if specified, will attempt to load a preview image (best size is 192x192) with that name from the mod's data folder and display it in the settings dialog.

Each option must be a property, not a member, and should be annotated with `Option(string displaytext, [string tooltip=""])` to be visible in the mod config menu.
Currently supported types are: `int`, `int?`, `float`, `float?`, `string`, `bool`, `Color`, `Color32`, and `Enum`.
If a property is a read-only `System.Action`, a button will be created that will execute the returned action if clicked.
If a property is of type `LocText`, no matter what it returns, the text in `displaytext` will be displayed as a full-width label with no input field.
If a property is of a user-defined type, PLib will check the public properties of that type -- if any of them have `Option` attributes, the property will be rendered as its own category with each of the inner options grouped inside.
If a valid localization string key name is used for `displaytext` (such as `STRINGS.YOURMOD.OPTIONS.YOUROPTION`), the localized value of that string from the strings database is used as the display text.

To support types not in the predefined list, the `[DynamicOption(Type)]` attribute can be added to specify the type of an `IOptionsEntry` handler class that can display the specified type.
If the type used as the handler has a constructor with the same signature as one of the predefined options entry classes, the arguments matching those parameters will be passed to it.

#### Categories

The optional third parameter of `Option` allows setting a custom category for the option to group related options together.
The category name is displayed as the title for the section.
If a valid localization string key name is used for the category (such as `STRINGS.YOURMOD.OPTIONS.YOURCATEGORY`), the localized value of that string from the strings database is used as the title.
All options inside a nested custom options class are placed under a category matching the title of the declaring `Option` property.

#### Range limits

`int`, `int?`, `float` and `float?` options can have validation in the form of a range limit.
Annotate the property with `PLib.Options.Limit(double min, double max)`.
If `PLib.Options.Limit` is used on a `string` field, the `max` will be used as the maximum string length for the option value.
Note that users can still enter values outside of the range manually in the configuration file.

#### Example

```cs
using Newtonsoft.Json;
using PeterHan.PLib.Options;

// ...

[JsonObject(MemberSerialization.OptIn)]
[ModInfo("https://www.github.com/peterhaneve/ONIMods")]
public class TestModSettings
{
    [Option("Wattage", "How many watts you can use before exploding.")]
    [Limit(1, 50000)]
    [JsonProperty]
    public float Watts { get; set; }

    public TestModSettings()
    {
        Watts = 10000f; // defaults to 10000, e.g. if the config doesn't exist
    }
}
```

```cs
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

// ...

public sealed class ModLoad : KMod.UserMod2
{
    public static void OnLoad()
    {
        PUtil.InitLibrary(false);
        new POptions().RegisterOptions(typeof(TestModSettings));
    }
}
```

This is how it looks in the mod menu:

![mod menu example screenshot](https://raw.githubusercontent.com/peterhaneve/ONIMods/PLib4.13/Docs/modoptions.png)

## Actions

The `PLib.Actions` component is used to register actions to be executed on user input.
It requires `PLib.Core`.
Actions created by this component can be rebound in the game options.

Register actions by using `PActionManager.CreateAction(string, LocString, PKeyBinding)` in `OnLoad`.
Creating a new `PActionManager` instance is required to use this method, but only one `PActionManager` instance should be created per mod.

The identifier should be unique to the action used and should include the mod name to avoid conflicts with other mods.
If multiple mods register the same action identifier, only the first will receive a valid `PAction`.
The returned `PAction` object has a `GetKAction` method which can be used to retrieve an `Action` that works in standard Klei functions.
The `PKeyBinding` is used to specify the default key binding.

Note that the game can change the values in the `Action` enum.
Instead of using the built-in `Action.NumActions` to denote "no action", consider using `PAction.MaxAction` instead which will use the correct value at runtime if necessary.

## Lighting

The `PLib.Lighting` component allows `Light2D` objects to emit light in a custom shape and intensity falloff.
It requires `PLib.Core`.

Register lighting types by using `PLightManager.Register(string, CastLight)` in `OnLoad`.
Creating a new `PLightManager` instance is required to use this method, but only one `PLightManager` instance should be created per mod.

The identifier should be unique to the light pattern that will be registered.
If multiple mods register the same lighting type identifier, all will receive a valid `ILightShape` object but only the first mod to register that shape will be used to render it.
The returned `ILightShape` object has a `GetKLightShape` method which can be used to retrieve a `LightShape` that works in standard `Light2D` functions.
The `CastLight` specifies a callback in the mod that can handle drawing the light. It needs the signature
```c
void CastLight(LightingArgs args);
```
The mod will receive an object encapsulating the lighting arguments, including the source of the light with the `Light2D` component and the starting cell.
See the `LightingArgs` class for more details.

## Buildings

The `PLib.Buildings` component abstracts several details of adding new buildings.
It requires `PLib.Core`.

Register a new building by using `PBuildingManager.Register(PBuilding)` in `OnLoad`.
Creating a new `PBuildingManager` instance is required to use this method, but only one `PBuildingManager` instance should be created per mod.

The `PBuilding` instance should be created only once, in `OnLoad`.
The building name, description, and effect can all be specified directly, or left empty to use the default localization string keys for each string.
The tech tree location and build menu location can also be specified without needing other patches.

## Database

The `PLib.Database` component deals with non-building operations involving the game database, translation, and codex files.
It requires `PLib.Core`.

### Translations

Register a mod for translation by using `PLocalization.Register()` in `OnLoad`.
Creating a new `PLocalization` instance is required to use this method, but only one `PLocalization` instance should be created per mod.

All classes in the mod assembly with `public static` `LocString` fields will be eligible for translation.
Translation files need to be placed in the `translations` folder in the mod directory, named as the target language code (*zh-CN* for example) and ending with the `.po` extension.
Note that the translation only occurs after all mods load, so avoid referencing the `LocString` fields during class initialization or `OnLoad` as they may not yet be localized at that time.

### Codex Entries

Register a mod for codex loading by using `PCodexManager.RegisterCreatures()` and/or `PCodexManager.RegisterPlants()`.
Creating a new `PCodexManager` instance is required to use this method, but only one `PCodexManager` instance should be created per mod.

The codex files will be loaded using the same structure as the base game: a `codex` folder must exist in the mod directory, with `Creatures` and `Plants` subfolders containing the codex data.

## Forwarded Components

PLib is built on forwarded components, classes inheriting from `PForwardedComponent` to allow communication across mods.
Forwarded components must declare a `Version` that should be incremented each time any change is made.
Each instance must `RegisterForForwarding` itself, either when constructed or when it is first used.

PLib will collect the registered component instances; one and only one (arbitrarily selected) of the components with the most recent version will have its `Initialize` method executed.
This is the ideal location to perform game patches with the latest version.
All forwarded components will share the same shared data object from `GetSharedData` and `SetSharedData` which can be used to store state; it should only contain types visible to all mods (base game types and System types).
Each component also has its own `InstanceData` field; this can be collected by the latest version with `GetInstanceData` by iterating all copies with `PRegistry.Instance.GetAllComponents`.

Forwarded components can also request each copy to perform some work; calling `InvokeAllProcess` will call the `Process` method on each instance with the specified operation code.
