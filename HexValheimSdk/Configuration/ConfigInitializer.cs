using BepInEx.Configuration;
using System;

namespace HexValheimSdk.Configuration
{
    /// <summary>Initializes a mod's settings with one final save and optional migrations.</summary>
    public static class ConfigInitializer
    {
        /// <summary>Migrates, binds common and custom settings, and saves once after successful binding.</summary>
        /// <param name="configFile">The mod's configuration file, before binding any settings.</param>
        /// <param name="bindSettings">Binds the mod's settings using the supplied file.</param>
        /// <param name="migrations">Optional append-only migration plan.</param>
        /// <returns>The common settings bound for this mod.</returns>
        /// <exception cref="ArgumentNullException">The file or binding callback is null.</exception>
        /// <remarks>
        /// Restores SaveOnConfigSet even if binding or saving fails. Failure does not roll back
        /// in-memory bindings, but no final save is attempted if binding throws. Create the watcher afterwards.
        /// </remarks>
        public static CommonConfigSettings Initialize(ConfigFile configFile, Action<ConfigFile> bindSettings, ConfigMigrationPlan migrations = null)
        {
            if (configFile == null)
            {
                throw new ArgumentNullException(nameof(configFile));
            }

            if (bindSettings == null)
            {
                throw new ArgumentNullException(nameof(bindSettings));
            }

            bool saveOnConfigSet = configFile.SaveOnConfigSet;

            configFile.SaveOnConfigSet = false;

            try
            {
                migrations?.Prepare(configFile);

                var commonSettings = new CommonConfigSettings(configFile);

                bindSettings(configFile);

                configFile.Save();

                return commonSettings;
            }
            finally
            {
                configFile.SaveOnConfigSet = saveOnConfigSet;
            }
        }
    }
}
