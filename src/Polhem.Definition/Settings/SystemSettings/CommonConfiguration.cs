using System.ComponentModel;
using Polhem.Core;
using Polhem.Definition.Attributes;
using Polhem.Core.Serialization;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Common parameters and environment settings.
    /// </summary>
    [Description("Common parameters and environment settings.")]
    [TreeNode("Common")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class CommonConfiguration : IObjectSerializeBase, ISysInfoConfiguration
    {
        /// <summary>
        /// System major version.
        /// </summary>
        [Description("System major version.")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether debug mode is enabled.
        /// </summary>
        [Description("Indicates whether debug mode is enabled.")]
        [DefaultValue(false)]
        public bool IsDebugMode { get; set; } = false;

        /// <summary>
        /// Gets or sets the deployment's default language (a BCP-47 culture such as <c>zh-TW</c> or
        /// <c>en-US</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// One setting with two uses. It is the culture a session receives when the user has no
        /// <c>st_user.culture</c> of their own, and it is the last hop of the language fall-back
        /// chain (<see cref="Language.LanguageFallback"/>): a caption, option set, UI text or message
        /// with no translation in the user's culture or its parents is looked up in this language
        /// before the base text is used. An English user skips this hop and gets the English base
        /// text. Clients receive it with the rest of this configuration.
        /// </para>
        /// <para>
        /// The default is <c>zh-TW</c>. A deployment serving another language sets it explicitly;
        /// an empty value gives users without a culture no session culture, so the client uses its
        /// own UI culture, and drops the fall-back hop, so an untranslated key shows the base text.
        /// </para>
        /// </remarks>
        [Category("Localization")]
        [Description("Default BCP-47 culture: the culture of users without one, and the last language fall-back (e.g. zh-TW, en-US).")]
        [DefaultValue("zh-TW")]
        public string DefaultLanguage { get; set; } = "zh-TW";

        /// <summary>
        /// List of allowed type namespaces for JSON-RPC data transfer (separated by '|').
        /// Only types in these namespaces are allowed for deserialization to ensure security.
        /// Example: Custom.Module|ThirdParty.Dto
        /// Note: Polhem.Core and Polhem.Definition are built-in system namespaces and do not need to be specified.
        /// </summary>
        [Category("API")]
        [Description("List of allowed type namespaces for JSON-RPC data transfer, separated by '|'.")]
        [DefaultValue("")]
        public string AllowedTypeNamespaces { get; set; } = string.Empty;

        /// <summary>
        /// Provides API payload handling options, such as serialization, compression, and encryption.
        /// </summary>
        [Category("API")]
        [Description("Provides API payload handling options, such as serialization, compression, and encryption.")]
        public ApiPayloadOptions ApiPayloadOptions { get; set; } = new ApiPayloadOptions();

        /// <summary>
        /// Object description.
        /// </summary>
        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
