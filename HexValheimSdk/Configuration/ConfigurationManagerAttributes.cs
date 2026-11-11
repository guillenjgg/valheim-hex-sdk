namespace HexValheimSdk.Configuration
{
    /// <summary>
    /// Supplies optional display metadata to BepInEx Configuration Manager through configuration tags.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass an instance to the tags argument of BepInEx's ConfigDescription constructor.
    /// Configuration Manager recognizes the class name and public field names through reflection,
    /// so no reference to ConfigurationManager.dll is required. Other configuration tools may ignore it.
    /// </para>
    /// <para>
    /// This type controls menu display only; it does not reorder sections or entries in saved files.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var description = new ConfigDescription(
    ///     "Maximum climbing height.",
    ///     new AcceptableValueRange&lt;float&gt;(0.5f, 5f),
    ///     new ConfigurationManagerAttributes { Order = 100 });
    /// var height = config.Bind("Climbing", "MaximumHeight", 2f, description);
    /// </code>
    /// </example>
    public sealed class ConfigurationManagerAttributes
    {
        // Configuration Manager's reflection contract requires a field, not a property.
        /// <summary>
        /// The setting's priority within its Configuration Manager category.
        /// Higher numbers appear first. Null leaves the manager's default ordering unchanged.
        /// </summary>
        /// <remarks>
        /// The manager's default order is zero. This value does not control category ordering.
        /// </remarks>
        public int? Order;
    }
}
