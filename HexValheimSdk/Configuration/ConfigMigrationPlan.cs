using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace HexValheimSdk.Configuration
{
    /// <summary>Runs ordered, versioned migrations before binding and when reloading saved settings.</summary>
    /// <remarks>
    /// Steps are versions 1 through N. Append new steps; never remove or reorder released steps.
    /// Files without a version start at zero. Future or malformed versions are rejected without saving.
    /// This adapter uses BepInEx 5's private OrphanedEntries property; unsupported versions fail explicitly.
    /// Use through ConfigInitializer, then pass the same plan to ConfigFileWatcher for external edits.
    /// </remarks>
    public sealed class ConfigMigrationPlan
    {
        private static readonly PropertyInfo OrphanedEntriesProperty = typeof(ConfigFile).GetProperty(
            "OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Action<ConfigMigrationContext>[] _steps;
        private readonly ConfigDefinition _versionDefinition;

        /// <summary>Creates an append-only migration plan with a configurable schema marker.</summary>
        /// <param name="versionDefinition">A reserved section/key for the schema version; do not bind it yourself.</param>
        /// <param name="steps">Migration callbacks, in version order.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">A step is null.</exception>
        public ConfigMigrationPlan(ConfigDefinition versionDefinition, params Action<ConfigMigrationContext>[] steps)
        {
            _versionDefinition = versionDefinition ?? throw new ArgumentNullException(nameof(versionDefinition));
            _steps = (Action<ConfigMigrationContext>[])(steps ?? throw new ArgumentNullException(nameof(steps))).Clone();

            foreach (Action<ConfigMigrationContext> step in _steps)
            {
                if (step == null)
                {
                    throw new ArgumentException("Migration steps cannot be null.", nameof(steps));
                }
            }
        }

        /// <summary>Migrates unbound saved values in memory, then binds the hidden version marker.</summary>
        /// <param name="configFile">The configuration before any settings have been bound.</param>
        /// <remarks>Does not save. ConfigInitializer calls this before binding and saving settings.</remarks>
        /// <exception cref="ArgumentNullException">The file is null.</exception>
        /// <exception cref="InvalidOperationException">Settings are already bound or the schema cannot be migrated.</exception>
        public void Prepare(ConfigFile configFile)
        {
            if (configFile == null)
            {
                throw new ArgumentNullException(nameof(configFile));
            }

            if (configFile.Count != 0)
            {
                throw new InvalidOperationException("Prepare migrations before binding any settings.");
            }

            IDictionary<ConfigDefinition, string> original = GetUnboundEntries(configFile);
            var migrated = new Dictionary<ConfigDefinition, string>(original);

            Migrate(migrated);

            ReplaceEntries(original, migrated);

            bool saveOnConfigSet = configFile.SaveOnConfigSet;

            configFile.SaveOnConfigSet = false;

            try
            {
                configFile.Bind(_versionDefinition, _steps.Length, new ConfigDescription(
                    "Configuration schema version. Managed automatically.", null,
                    new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true }));
            }
            finally
            {
                configFile.SaveOnConfigSet = saveOnConfigSet;
            }
        }

        /// <summary>Reads and migrates the saved file before applying values to existing entries.</summary>
        /// <param name="configFile">The initialized configuration file.</param>
        /// <remarks>
        /// Uses a separate BepInEx file reader so migration failures leave live entries and the file unchanged.
        /// Unknown keys are preserved. Successful upgrades are saved; ordinary reloads are not saved.
        /// Missing bound keys retain their live values, matching BepInEx reload behavior.
        /// Use the watcher's Reloaded event for post-reload work; this method does not raise ConfigFile.ConfigReloaded.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The file is null.</exception>
        /// <exception cref="InvalidOperationException">The file is missing or its schema cannot be migrated.</exception>
        public void Reload(ConfigFile configFile)
        {
            if (configFile == null)
            {
                throw new ArgumentNullException(nameof(configFile));
            }

            if (!System.IO.File.Exists(configFile.ConfigFilePath))
            {
                throw new InvalidOperationException("The configuration file no longer exists.");
            }

            var incoming = new ConfigFile(configFile.ConfigFilePath, false);
            var entries = new Dictionary<ConfigDefinition, string>(GetUnboundEntries(incoming));
            bool upgraded = Migrate(entries);
            IDictionary<ConfigDefinition, string> unbound = GetUnboundEntries(configFile);
            bool saveOnConfigSet = configFile.SaveOnConfigSet;

            configFile.SaveOnConfigSet = false;

            try
            {
                unbound.Clear();

                foreach (KeyValuePair<ConfigDefinition, string> pair in entries)
                {
                    if (((IDictionary<ConfigDefinition, ConfigEntryBase>)configFile).TryGetValue(pair.Key, out ConfigEntryBase entry))
                    {
                        entry.SetSerializedValue(pair.Value);
                    }
                    else
                    {
                        unbound[pair.Key] = pair.Value;
                    }
                }

                if (upgraded)
                {
                    configFile.Save();
                }
            }
            finally
            {
                configFile.SaveOnConfigSet = saveOnConfigSet;
            }
        }

        private bool Migrate(IDictionary<ConfigDefinition, string> entries)
        {
            int version = 0;

            if (entries.TryGetValue(_versionDefinition, out string savedVersion)
                && (!int.TryParse(savedVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out version)
                    || version < 0))
            {
                throw new InvalidOperationException("The saved configuration schema version is invalid.");
            }

            if (version > _steps.Length)
            {
                throw new InvalidOperationException("This configuration was saved by a newer migration plan.");
            }

            var context = new ConfigMigrationContext(entries);

            for (int index = version; index < _steps.Length; index++)
            {
                _steps[index](context);
            }

            entries[_versionDefinition] = _steps.Length.ToString(CultureInfo.InvariantCulture);

            return version < _steps.Length || savedVersion == null;
        }

        private static IDictionary<ConfigDefinition, string> GetUnboundEntries(ConfigFile configFile)
        {
            return OrphanedEntriesProperty?.GetValue(configFile, null) as IDictionary<ConfigDefinition, string>
                ?? throw new InvalidOperationException("This BepInEx version does not expose the expected saved-entry storage.");
        }

        private static void ReplaceEntries(IDictionary<ConfigDefinition, string> destination, IDictionary<ConfigDefinition, string> source)
        {
            destination.Clear();

            foreach (KeyValuePair<ConfigDefinition, string> pair in source)
            {
                destination.Add(pair.Key, pair.Value);
            }
        }
    }
}
