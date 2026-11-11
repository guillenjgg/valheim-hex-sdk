using BepInEx.Configuration;
using System;

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

        /// <summary>Whether to show this setting. Null uses the manager's default.</summary>
        public bool? Browsable;

        /// <summary>Whether to require the manager's advanced-settings filter.</summary>
        public bool? IsAdvanced;

        /// <summary>Whether to show the value without allowing edits through the manager.</summary>
        public bool? ReadOnly;

        /// <summary>Optional display name; does not rename the saved key.</summary>
        public string DispName;

        /// <summary>Optional display category; does not move the saved section.</summary>
        public string Category;

        /// <summary>Optional tooltip override. Prefer the ConfigDescription text.</summary>
        public string Description;

        /// <summary>Whether to show a range slider as a percentage.</summary>
        public bool? ShowRangeAsPercent;

        /// <summary>Overrides the default value offered by the manager's reset button.</summary>
        public object DefaultValue;

        /// <summary>Whether to hide the reset button.</summary>
        public bool? HideDefaultButton;

        /// <summary>Whether to hide the setting label, typically for a custom drawer.</summary>
        public bool? HideSettingName;

        /// <summary>Optional Unity OnGUI drawer. Do not combine with CustomHotkeyDrawer.</summary>
        public Action<ConfigEntryBase> CustomDrawer;

        /// <summary>Optional input-aware drawer. Do not combine with CustomDrawer.</summary>
        public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

        /// <summary>Converts values to text in the manager's editor.</summary>
        public Func<object, string> ObjToStr;

        /// <summary>Converts text to values in the manager's editor.</summary>
        public Func<string, object> StrToObj;

        /// <summary>Draws an editor that can claim keyboard input while editing.</summary>
        /// <param name="setting">The setting being edited.</param>
        /// <param name="isCurrentlyAcceptingInput">Set true while capturing input; reset to false afterwards.</param>
        public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);

        internal ConfigurationManagerAttributes WithDefaultOrder(int order)
        {
            var copy = (ConfigurationManagerAttributes)MemberwiseClone();

            copy.Order = Order ?? order;

            return copy;
        }
    }
}
