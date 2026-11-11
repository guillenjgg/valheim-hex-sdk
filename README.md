# HexValheimSdk

A .NET Standard 2.0 library for reusable Valheim mod configuration helpers.
Consumers still reference BepInEx and their required Unity/game assemblies.

## Initialize settings

Bind settings before starting the watcher. Initialization temporarily disables
save-on-bind, creates the common settings, calls your binding callback, saves once,
and restores the previous save preference even when an exception occurs.

```csharp
using BepInEx.Configuration;
using HexValheimSdk.Configuration;
using UnityEngine;

private CommonConfigSettings _commonSettings;
private ConfigFileWatcher _configWatcher;
private ConfigEntry<KeyboardShortcut> _interactKey;
private ConfigEntry<float> _interactDistance;

// In the plugin's Awake():
_commonSettings = ConfigInitializer.Initialize(Config, file =>
{
    var climbing = new ConfigCategory(file, "Climbing");

    _interactKey = climbing.Bind("InteractKey", new KeyboardShortcut(KeyCode.E),
        "Shortcut used to climb a ledge. Supports modifiers.");

    _interactDistance = climbing.Bind("InteractDistance", 2f,
        "Maximum interaction distance in meters.",
        new AcceptableValueRange<float>(0.1f, 10f));
});

_configWatcher = new ConfigFileWatcher(Config, Logger);
_configWatcher.Reloaded += (sender, args) =>
{
    Logger.LogInfo("Configuration reloaded from disk.");
};

// In the plugin's OnDestroy():
_configWatcher?.Dispose();
```

The common settings are `General.IsModEnabled` (true) and
`Development.EnableDebugLogging` (false). Each mod has independent saved values.
Mods must implement feature disabling and check the debug setting themselves;
binding these entries does not disable patches or filter BepInEx logger calls.

The watcher debounces file notifications for 500 ms by default, reloads through
BepInEx's main-thread synchronization helper, and logs failures without retrying.
`Reloaded` fires after successful watcher reloads, not edits made directly through
Configuration Manager. Use an entry's `SettingChanged` event to observe both paths.
Exceptions in one `Reloaded` subscriber are logged without skipping the others.

## Display metadata

Reuse one `ConfigCategory` per section. Settings appear in declaration order within
the category. Higher `Order` numbers appear first. Common settings reserve
`int.MaxValue`; automatic category ordering starts at `int.MaxValue - 1`.
This controls Configuration Manager display, not category headers or `.cfg` order.

```csharp
var development = new ConfigCategory(Config, "Development");
var diagnostics = development.Bind("Diagnostics", false, "Extra diagnostics.",
    acceptableValues: null,
    attributes: new ConfigurationManagerAttributes
    {
        IsAdvanced = true,
        DispName = "Detailed diagnostics"
    });
```

Metadata also supports `Browsable`, `ReadOnly`, `Category`, percentage sliders,
reset-button overrides, converters, and custom drawers. Custom drawers execute in
Unity's OnGUI context; don't set both drawer callbacks. An explicit `Order`
overrides automatic ordering. The category copies the metadata before tagging it.
No ConfigurationManager.dll reference is required; without that plugin, display
metadata is ignored while the settings continue to work.

## Versioned migrations

Declare a plan before initializing settings. Reserve a schema marker and append
steps in release order. A file without a marker is version zero. Do not reorder or
remove released steps. Each step executes once for that file's recorded version.

```csharp
var migrations = new ConfigMigrationPlan(
    new ConfigDefinition("Development", "ConfigurationVersion"),
    step => step.MoveSetting(
        new ConfigDefinition("General", "Distance"),
        new ConfigDefinition("Climbing", "InteractDistance")),
    step => step.RemoveSetting(
        new ConfigDefinition("Development", "OldTestControl")));

_commonSettings = ConfigInitializer.Initialize(Config, file =>
{
    var climbing = new ConfigCategory(file, "Climbing");
    _interactDistance = climbing.Bind("InteractDistance", 2f,
        "Maximum interaction distance in meters.");
}, migrations);

_configWatcher = new ConfigFileWatcher(Config, Logger,
    reloadDelayMilliseconds: 500, migrations: migrations);
```

Use `MoveSetting` for renames and section moves. Existing destination values win.
Use its optional converter or `ConvertSetting` for type/unit changes; numeric
converters should use invariant culture and throw on invalid input. Explicit
saved values are preserved when defaults change. Unrelated unknown entries are
preserved; only explicit `RemoveSetting` operations delete them.

Migrations run on saved unbound values before binding. The same plan handles old
files supplied during external reloads. Migration callbacks run on a snapshot;
if they fail, the original saved file and live entries are not overwritten.
Future, negative, or malformed versions are rejected without saving. Successful
reload upgrades save the new schema; ordinary reloads don't save. Missing keys
retain live values, matching BepInEx reload behavior. Normal BepInEx deserialization
and range rules still apply when migrated values are assigned.

Migration-aware reloads raise the watcher's `Reloaded` event, not BepInEx's
`ConfigFile.ConfigReloaded`. Individual changed entries still raise `SettingChanged`.
Migration access to unbound saved values relies on BepInEx 5's private
`OrphanedEntries` property and fails explicitly if it changes. Keep initialization
and manual migration calls on the main thread, before or outside active reloads.

## Build and checks

```powershell
dotnet build HexValheimSdk/HexValheimSdk.csproj
dotnet build tests/ConfigurationChecks/ConfigurationChecks.csproj
& tests/ConfigurationChecks/bin/Debug/net48/ConfigurationChecks.exe
```

The checks use the real BepInEx DLL referenced from the local mod libraries and
write fixture files inside their build output. They cover initialization,
metadata, startup/reload migration, precedence, repeat migration, failure
preservation, and schema validation. Unity watcher timing and UI behavior require
an in-game test.

XML documentation is generated beside the SDK DLL for IntelliSense. Keep the XML
beside the DLL used as a development reference; players only need the DLL.

## References

- [BepInEx configuration implementation](https://github.com/BepInEx/BepInEx/blob/v5.4.22/BepInEx/Configuration/ConfigFile.cs)
- [Configuration Manager metadata](https://github.com/BepInEx/BepInEx.ConfigurationManager#overriding-default-configuration-manager-behavior)
