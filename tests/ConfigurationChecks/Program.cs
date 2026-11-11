using BepInEx;
using BepInEx.Configuration;
using HexValheimSdk.Configuration;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static readonly ConfigDefinition Version = new ConfigDefinition("Development", "ConfigurationVersion");
    private static readonly ConfigDefinition Old = new ConfigDefinition("Old", "Distance");
    private static readonly ConfigDefinition Current = new ConfigDefinition("Climbing", "InteractDistance");
    private static string _root;
    private static int _checks;

    private static int Main()
    {
        _root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);

        // A headless fixture supplies only the path needed by BepInEx's static core configuration.
        typeof(Paths).GetProperty("BepInExConfigPath").GetSetMethod(true).Invoke(null,
            new object[] { Path.Combine(_root, "BepInEx.cfg") });

        try
        {
            CheckInitialization();
            CheckMigration();
            CheckRejectedMigrations();

            Console.WriteLine("Passed " + _checks + " configuration checks against BepInEx " + typeof(ConfigFile).Assembly.GetName().Version);

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);

            return 1;
        }
    }

    private static void CheckInitialization()
    {
        foreach (bool original in new[] { true, false })
        {
            var config = NewFile("initialize-" + original);

            config.SaveOnConfigSet = original;

            CommonConfigSettings common = ConfigInitializer.Initialize(config, file =>
            {
                Assert(!file.SaveOnConfigSet, "Saving disabled inside binding callback");

                var category = new ConfigCategory(file, "Climbing");
                var attributes = new ConfigurationManagerAttributes { IsAdvanced = true, DispName = "Distance" };
                ConfigEntry<float> distance = category.Bind("Distance", 2f, "Distance", new AcceptableValueRange<float>(0.1f, 10f), attributes);
                ConfigEntry<bool> second = category.Bind("Second", true, "Second");
                var firstMetadata = distance.Description.Tags.OfType<ConfigurationManagerAttributes>().Single();
                var secondMetadata = second.Description.Tags.OfType<ConfigurationManagerAttributes>().Single();

                Assert(firstMetadata.Order > secondMetadata.Order, "Declaration order is descending");
                Assert(firstMetadata.IsAdvanced == true && firstMetadata.DispName == "Distance", "Custom display metadata retained");
                Assert(attributes.Order == null, "Caller metadata is not mutated");

                distance.Value = 50f;

                Assert(distance.Value == 10f, "BepInEx range validation preserved");
            });

            Assert(config.SaveOnConfigSet == original, "Save preference restored after success");
            Assert(common.IsModEnabled.Value && !common.EnableDebugLogging.Value, "Common settings initialized");
            Assert(File.Exists(config.ConfigFilePath), "Successful initialization saved file");

            var failed = NewFile("failure-" + original);

            failed.SaveOnConfigSet = original;

            Expect<InvalidOperationException>(() => ConfigInitializer.Initialize(failed, file =>
            {
                file.Bind("Test", "Value", true);

                throw new InvalidOperationException("Fixture binding failure");
            }));

            Assert(failed.SaveOnConfigSet == original, "Save preference restored after failure");
            Assert(!File.Exists(failed.ConfigFilePath), "Failed initialization did not save");
        }
    }

    private static ConfigMigrationPlan Plan()
    {
        return new ConfigMigrationPlan(Version,
            step => step.MoveSetting(Old, Current),
            step => step.ConvertSetting(Current, value =>
                (float.Parse(value, CultureInfo.InvariantCulture) / 100f).ToString(CultureInfo.InvariantCulture)),
            step => step.RemoveSetting(new ConfigDefinition("Old", "Obsolete")));
    }

    private static void CheckMigration()
    {
        var config = NewFile("migration");

        File.WriteAllText(config.ConfigFilePath, "[Old]\nDistance = 250\nObsolete = true\n[Unrelated]\nKeep = player-value\n");

        config = new ConfigFile(config.ConfigFilePath, false);
        ConfigEntry<float> distance = null;
        ConfigMigrationPlan plan = Plan();

        ConfigInitializer.Initialize(config, file => distance = file.Bind(Current, 9f), plan);

        Assert(distance.Value == 2.5f, "Startup move and unit conversion preserve player value");
        Assert(!File.ReadAllText(config.ConfigFilePath).Contains("Obsolete ="), "Explicit obsolete setting removed");
        Assert(File.ReadAllText(config.ConfigFilePath).Contains("Keep = player-value"), "Unknown player setting preserved");

        var repeated = new ConfigFile(config.ConfigFilePath, false);
        ConfigEntry<float> repeatedDistance = null;

        ConfigInitializer.Initialize(repeated, file => repeatedDistance = file.Bind(Current, 99f), plan);

        Assert(repeatedDistance.Value == 2.5f, "Repeated startup neither reconverts nor replaces saved value with new default");

        File.WriteAllText(config.ConfigFilePath,
            "[Old]\nDistance = 900\n[Climbing]\nInteractDistance = 375\n[Unrelated]\nKeep = external\n");

        plan.Reload(config);

        Assert(distance.Value == 3.75f, "External legacy reload uses existing destination then converts");
        Assert(config.SaveOnConfigSet, "Reload restores original save preference");
        Assert(File.ReadAllText(config.ConfigFilePath).Contains("Keep = external"), "Reload preserves unrelated saved entries");

        plan.Reload(config);

        Assert(distance.Value == 3.75f, "Repeated reload does not reconvert");

        const string invalidConversion = "[Old]\nDistance = bad-value\n";

        File.WriteAllText(config.ConfigFilePath, invalidConversion);

        Expect<FormatException>(() => plan.Reload(config));

        Assert(distance.Value == 3.75f, "Failed reload conversion leaves live value unchanged");
        Assert(File.ReadAllText(config.ConfigFilePath) == invalidConversion, "Failed reload conversion leaves file unchanged");
        Assert(config.SaveOnConfigSet, "Failed reload conversion keeps save preference");

        config.SaveOnConfigSet = false;

        File.WriteAllText(config.ConfigFilePath,
            "[Development]\nConfigurationVersion = 3\n[Climbing]\nInteractDistance = 4\n");

        plan.Reload(config);

        Assert(distance.Value == 4f && !config.SaveOnConfigSet, "Ordinary reload restores disabled saving");

        config.SaveOnConfigSet = true;

        plan.Reload(config);

        foreach (string version in new[] { "99", "-1", "invalid" })
        {
            string contents = "[Development]\nConfigurationVersion = " + version + "\n[Climbing]\nInteractDistance = 8\n";

            File.WriteAllText(config.ConfigFilePath, contents);

            Expect<InvalidOperationException>(() => plan.Reload(config));

            Assert(distance.Value == 4f, "Rejected reload keeps live value");
            Assert(File.ReadAllText(config.ConfigFilePath) == contents, "Rejected reload keeps file unchanged");
        }
    }

    private static void CheckRejectedMigrations()
    {
        var config = NewFile("bad-conversion");
        const string contents = "[Old]\nDistance = not-a-number\n";

        File.WriteAllText(config.ConfigFilePath, contents);

        config = new ConfigFile(config.ConfigFilePath, false);

        Expect<FormatException>(() => ConfigInitializer.Initialize(config, file => file.Bind(Current, 2f), Plan()));

        Assert(File.ReadAllText(config.ConfigFilePath) == contents, "Failed conversion does not overwrite saved file");
        Assert(config.Count == 0 && config.SaveOnConfigSet, "Failed conversion leaves bindings untouched and restores saving");

        var future = NewFile("future-startup");
        const string futureContents = "[Development]\nConfigurationVersion = 99\n";

        File.WriteAllText(future.ConfigFilePath, futureContents);

        future = new ConfigFile(future.ConfigFilePath, false);

        Expect<InvalidOperationException>(() => ConfigInitializer.Initialize(future, file => file.Bind(Current, 2f), Plan()));

        Assert(File.ReadAllText(future.ConfigFilePath) == futureContents, "Future startup schema is not overwritten");
    }

    private static ConfigFile NewFile(string name)
    {
        return new ConfigFile(Path.Combine(_root, name + ".cfg"), false);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }

        _checks++;
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            _checks++;

            return;
        }

        throw new Exception("Expected " + typeof(T).Name);
    }
}
