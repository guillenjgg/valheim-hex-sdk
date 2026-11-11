using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Timers;

namespace HexValheimSdk.Configuration
{
    /// <summary>
    /// Watches a mod's configuration file and reloads it after a quiet period following file changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create this object during plugin initialization after binding and initially saving settings.
    /// Keep it for the plugin's lifetime and call <see cref="Dispose"/> when unloading.
    /// BepInEx's threading helper routes notifications and reloads to Unity's main thread.
    /// </para>
    /// <para>
    /// Reloading updates configuration entries; the consuming mod applies their gameplay effects.
    /// Reload failures are logged without automatic retries. This class does not perform configuration
    /// migrations or save the configuration file.
    /// </para>
    /// </remarks>
    public sealed class ConfigFileWatcher : IDisposable
    {
        private readonly ConfigFile _configFile;
        private readonly ManualLogSource _manualLogSource;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _reloadTimer;
        private readonly Stopwatch _timeSinceChange = new Stopwatch();
        private readonly object _syncRoot = new object();
        private readonly int _reloadDelayMilliseconds;
        private bool _reloadPending;
        private bool _disposed;

        /// <summary>
        /// Starts watching the supplied configuration file for changes, creation, and renaming.
        /// </summary>
        /// <param name="configFile">The consuming mod's existing configuration file.</param>
        /// <param name="manualLogSource">The consuming mod's BepInEx logger.</param>
        /// <param name="reloadDelayMilliseconds">
        /// The required quiet period after the latest file notification, in milliseconds.
        /// Must be greater than zero. Defaults to 500.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="configFile"/> or <paramref name="manualLogSource"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="reloadDelayMilliseconds"/> is zero or negative.
        /// </exception>
        /// <remarks>
        /// Requires an initialized BepInEx Unity environment and an existing configuration directory.
        /// The configuration file and logger remain owned by the caller.
        /// Each new file notification restarts the wait before reloading.
        /// </remarks>
        public ConfigFileWatcher(ConfigFile configFile, ManualLogSource manualLogSource, int reloadDelayMilliseconds = 500)
        {
            _configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
            _manualLogSource = manualLogSource ?? throw new ArgumentNullException(nameof(manualLogSource));

            if (reloadDelayMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reloadDelayMilliseconds));
            }

            _reloadDelayMilliseconds = reloadDelayMilliseconds;

            string directory = Path.GetDirectoryName(_configFile.ConfigFilePath);
            string fileName = Path.GetFileName(_configFile.ConfigFilePath);

            _watcher = new FileSystemWatcher(directory, fileName);

            _reloadTimer = new Timer(_reloadDelayMilliseconds);
            _reloadTimer.AutoReset = false;
            _reloadTimer.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _reloadTimer.Elapsed += OnReloadTimerElapsed;

            _watcher.Changed += ReadConfigValues;
            _watcher.Created += ReadConfigValues;
            _watcher.Renamed += ReadConfigValues;
            _watcher.IncludeSubdirectories = true;
            _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _reloadPending = true;

                _timeSinceChange.Restart();

                _reloadTimer.Stop();
                _reloadTimer.Interval = _reloadDelayMilliseconds;
                _reloadTimer.Start();
            }
        }

        private void OnReloadTimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_syncRoot)
            {
                if (_disposed || !_reloadPending)
                {
                    return;
                }

                // Stopping a timer cannot retract an Elapsed callback already queued.
                long remaining = _reloadDelayMilliseconds - _timeSinceChange.ElapsedMilliseconds;

                if (remaining > 0)
                {
                    _reloadTimer.Stop();
                    _reloadTimer.Interval = remaining;
                    _reloadTimer.Start();

                    return;
                }

                _reloadPending = false;

                if (!File.Exists(_configFile.ConfigFilePath))
                {
                    return;
                }

                try
                {
                    _manualLogSource.LogDebug("Reloading configuration: " + _configFile.ConfigFilePath);

                    _configFile.Reload();
                }
                catch (Exception exception)
                {
                    _manualLogSource.LogError("Could not reload configuration: " + exception);
                }
            }
        }

        /// <summary>
        /// Cancels pending reloads, stops watching, and releases the watcher and timer.
        /// </summary>
        /// <remarks>
        /// Calling this method more than once is safe. It does not save or dispose the supplied
        /// configuration file or dispose the supplied logger. A reload already in progress may finish.
        /// </remarks>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _reloadPending = false;

                _reloadTimer.Stop();
                _reloadTimer.Elapsed -= OnReloadTimerElapsed;

                _reloadTimer.Dispose();

                _watcher.EnableRaisingEvents = false;

                _watcher.Changed -= ReadConfigValues;
                _watcher.Created -= ReadConfigValues;
                _watcher.Renamed -= ReadConfigValues;

                _watcher.Dispose();
            }
        }
    }
}
