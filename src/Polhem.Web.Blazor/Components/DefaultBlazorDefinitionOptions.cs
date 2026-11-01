namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The options a component falls back to in a host that registered no <see cref="IBlazorDefinitionOptions"/>.
    /// </summary>
    internal sealed class DefaultBlazorDefinitionOptions : IBlazorDefinitionOptions
    {
        internal static readonly DefaultBlazorDefinitionOptions Instance = new();

        /// <inheritdoc />
        public string DefaultLanguage => string.Empty;

        /// <inheritdoc />
        public bool UseDefinitionLoader => true;
    }
}
