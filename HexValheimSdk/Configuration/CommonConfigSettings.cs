using BepInEx.Configuration;
using System;

namespace HexValheimSdk.Configuration
{
    /// <summary>
    /// Shared setting definitions bound to an individual mod's configuration file.
    /// Consuming mods are responsible for applying these settings to their behavior.
    /// </summary>
    /// <remarks>
    /// Create one instance per mod, using that mod's existing <see cref="ConfigFile"/>.
    /// Values are independent between mods. Bind these settings before creating a
    /// <see cref="ConfigFileWatcher"/>. This class does not watch files or apply gameplay or logging behavior.
    /// </remarks>
    public sealed class CommonConfigSettings
    {
        /// <summary>
        /// Gets the setting that controls whether the consuming mod's features are enabled.
        /// </summary>
        /// <remarks>
        /// Bound to General.IsModEnabled with a default of true. The consuming mod must check
        /// its value or subscribe to its SettingChanged event; this setting does not disable patches automatically.
        /// Its Configuration Manager order is int.MaxValue, placing it before lower-priority settings in General.
        /// </remarks>
        public ConfigEntry<bool> IsModEnabled { get; }

        /// <summary>
        /// Gets the setting that controls the consuming mod's optional debug logging.
        /// BepInEx logging filters still apply.
        /// </summary>
        /// <remarks>
        /// Bound to Development.EnableDebugLogging with a default of false. The consuming mod
        /// must check this value before emitting optional debug messages. It does not filter direct logger calls.
        /// Its Configuration Manager order is int.MaxValue within Development.
        /// </remarks>
        public ConfigEntry<bool> EnableDebugLogging { get; }

        /// <summary>
        /// Binds the common settings without replacing existing saved values.
        /// </summary>
        /// <param name="configFile">The consuming mod's configuration file in which to bind the settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configFile"/> is null.</exception>
        /// <remarks>
        /// Saving during binding follows the supplied file's SaveOnConfigSet setting.
        /// </remarks>
        public CommonConfigSettings(ConfigFile configFile)
        {
            if (configFile == null)
            {
                throw new ArgumentNullException(nameof(configFile));
            }

            IsModEnabled = configFile.Bind(
                "General",
                "IsModEnabled",
                true,
                new ConfigDescription(
                    "Enables this mod's features. The mod determines whether changes take effect immediately or require a restart.",
                    null,
                    new ConfigurationManagerAttributes { Order = int.MaxValue }));

            EnableDebugLogging = configFile.Bind(
                "Development",
                "EnableDebugLogging",
                false,
                new ConfigDescription(
                    "Enables optional debug logging for this mod. BepInEx must also allow Debug messages in the desired log output.",
                    null,
                    new ConfigurationManagerAttributes { Order = int.MaxValue }));
        }
    }
}
