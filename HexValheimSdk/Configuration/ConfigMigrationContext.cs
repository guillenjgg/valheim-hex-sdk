using BepInEx.Configuration;
using System;
using System.Collections.Generic;

namespace HexValheimSdk.Configuration
{
    /// <summary>Edits serialized saved settings during one versioned migration step.</summary>
    public sealed class ConfigMigrationContext
    {
        private readonly IDictionary<ConfigDefinition, string> _entries;

        internal ConfigMigrationContext(IDictionary<ConfigDefinition, string> entries)
        {
            _entries = entries;
        }

        /// <summary>Moves or renames a saved setting, optionally converting its serialized value.</summary>
        /// <param name="previous">The former section and key.</param>
        /// <param name="current">The new section and key.</param>
        /// <param name="convert">Optional conversion; throw on invalid data to abort migration.</param>
        /// <remarks>An existing destination wins. Missing sources do nothing. The old key is removed.</remarks>
        /// <exception cref="ArgumentNullException">Either definition is null.</exception>
        /// <exception cref="ArgumentException">The source and destination are identical.</exception>
        public void MoveSetting(ConfigDefinition previous, ConfigDefinition current, Func<string, string> convert = null)
        {
            if (previous == null || current == null)
            {
                throw new ArgumentNullException(previous == null ? nameof(previous) : nameof(current));
            }

            if (previous.Equals(current))
            {
                throw new ArgumentException("Use ConvertSetting to change a value without renaming it.", nameof(current));
            }

            if (!_entries.TryGetValue(previous, out string value))
            {
                return;
            }

            if (!_entries.ContainsKey(current))
            {
                _entries[current] = ConvertValue(value, convert);
            }

            _entries.Remove(previous);
        }

        /// <summary>Converts an existing value in place, for example when changing units or types.</summary>
        /// <param name="definition">The setting to convert.</param>
        /// <param name="convert">Converts serialized text; use invariant culture for numeric values.</param>
        /// <exception cref="ArgumentNullException">The definition or converter is null.</exception>
        /// <remarks>Missing settings are left absent, so binding can supply their default.</remarks>
        public void ConvertSetting(ConfigDefinition definition, Func<string, string> convert)
        {
            if (definition == null || convert == null)
            {
                throw new ArgumentNullException(definition == null ? nameof(definition) : nameof(convert));
            }

            if (_entries.TryGetValue(definition, out string value))
            {
                _entries[definition] = ConvertValue(value, convert);
            }
        }

        /// <summary>Explicitly removes an obsolete setting. Unrelated settings are preserved.</summary>
        /// <param name="definition">The section and key to remove.</param>
        /// <exception cref="ArgumentNullException">The definition is null.</exception>
        public void RemoveSetting(ConfigDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _entries.Remove(definition);
        }

        private static string ConvertValue(string value, Func<string, string> convert)
        {
            return convert == null ? value : convert(value)
                ?? throw new InvalidOperationException("A migration converter returned null.");
        }
    }
}
