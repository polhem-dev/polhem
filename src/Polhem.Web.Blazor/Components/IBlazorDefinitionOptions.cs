namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The host's settings for how components assemble definitions, so a UI package can read them without referencing
    /// a host package.
    /// </summary>
    public interface IBlazorDefinitionOptions
    {
        /// <summary>
        /// Gets the deployment's default language, the last hop of the language fall-back chain. Blank drops the hop.
        /// </summary>
        string DefaultLanguage { get; }

        /// <summary>
        /// Gets a value indicating whether a component without a loader of its own assembles definitions through a
        /// <see cref="Polhem.Api.Client.Definitions.FormDefinitionLoader"/>; otherwise it renders them as stored.
        /// </summary>
        bool UseDefinitionLoader { get; }
    }
}
