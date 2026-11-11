using BepInEx.Configuration;
using System;

namespace HexValheimSdk.Configuration
{
    /// <summary>
    /// Binds settings into one configuration section and assigns their Configuration Manager display order.
    /// </summary>
    /// <remarks>
    /// Reuse one instance per section during plugin initialization. Settings bound through this instance
    /// appear in declaration order within their category. Common settings reserve int.MaxValue and
    /// appear before these settings. This class does not control category header order or saved-file order.
    /// Creating a category does not create an empty section; the first binding creates the section.
    /// Instances are intended for use on the plugin initialization thread, not concurrent binding.
    /// </remarks>
    /// <example>
    /// <code>
    /// var climbing = new ConfigCategory(Config, "Climbing");
    /// var height = climbing.Bind("MaximumHeight", 2f, "Maximum climbing height.",
    ///     new AcceptableValueRange&lt;float&gt;(0.5f, 5f));
    /// var cost = climbing.Bind("StaminaCost", 10f, "Stamina used when climbing.");
    /// </code>
    /// </example>
    public sealed class ConfigCategory
    {
        private readonly ConfigFile _configFile;
        private long _nextOrder = int.MaxValue - 1;

        /// <summary>
        /// Gets the section name used for all settings bound through this category.
        /// </summary>
        public string Section { get; }

        /// <summary>
        /// Creates a category for a section in the consuming mod's configuration file.
        /// </summary>
        /// <param name="configFile">The consuming mod's existing configuration file.</param>
        /// <param name="section">The section name, such as Climbing or General.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        /// <exception cref="ArgumentException">The section name is empty, whitespace, or invalid for BepInEx.</exception>
        public ConfigCategory(ConfigFile configFile, string section)
        {
            _configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));

            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (string.IsNullOrWhiteSpace(section))
            {
                throw new ArgumentException("A category must have a section name.", nameof(section));
            }

            // Let BepInEx validate its own section-name rules without binding a setting.
            Section = new ConfigDefinition(section, "SectionValidation").Section;
        }

        /// <summary>
        /// Binds a setting in this section with automatic Configuration Manager display ordering.
        /// </summary>
        /// <typeparam name="T">The BepInEx-supported setting value type.</typeparam>
        /// <param name="key">The setting's unique key within this section.</param>
        /// <param name="defaultValue">The value used when no saved value exists.</param>
        /// <param name="description">Player-facing text explaining the setting.</param>
        /// <param name="acceptableValues">Optional BepInEx value range or allowed-value list.</param>
        /// <returns>The configuration entry, whose Value reflects subsequent configuration changes.</returns>
        /// <exception cref="ArgumentNullException">The key or description is null.</exception>
        /// <exception cref="ArgumentException">The key is invalid for BepInEx.</exception>
        /// <exception cref="InvalidOperationException">All available display priorities have been assigned.</exception>
        /// <remarks>
        /// Bind each key once. If already bound, BepInEx returns the existing entry and retains its metadata.
        /// Existing saved values are preserved. Saving follows the file's SaveOnConfigSet setting.
        /// Without Configuration Manager, the settings still function but the ordering tag has no effect.
        /// </remarks>
        public ConfigEntry<T> Bind<T>(string key, T defaultValue, string description, AcceptableValueBase acceptableValues = null)
        {
            return Bind(key, defaultValue, description, acceptableValues, null);
        }

        /// <summary>Binds a setting with custom display metadata and automatic ordering unless explicitly overridden.</summary>
        /// <typeparam name="T">The BepInEx-supported value type.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <param name="defaultValue">The value used when no saved value exists.</param>
        /// <param name="description">Player-facing description.</param>
        /// <param name="acceptableValues">Optional range or list; pass null for none.</param>
        /// <param name="attributes">Optional display metadata. A copy is tagged, leaving this instance unchanged.</param>
        /// <returns>The bound configuration entry.</returns>
        /// <remarks>Uses the same validation and binding rules as the simpler Bind overload.</remarks>
        public ConfigEntry<T> Bind<T>(string key, T defaultValue, string description,
            AcceptableValueBase acceptableValues, ConfigurationManagerAttributes attributes)
        {
            if (_nextOrder < int.MinValue)
            {
                throw new InvalidOperationException("No configuration display priorities remain in this category.");
            }

            var metadata = new ConfigDescription(
                description,
                acceptableValues,
                (attributes ?? new ConfigurationManagerAttributes()).WithDefaultOrder((int)_nextOrder));

            ConfigEntry<T> entry = _configFile.Bind(Section, key, defaultValue, metadata);

            _nextOrder--;

            return entry;
        }
    }
}
