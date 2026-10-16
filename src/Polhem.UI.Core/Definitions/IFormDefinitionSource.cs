using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.UI.Core.Definitions
{
    /// <summary>
    /// Supplies the definitions a form view is built from.
    /// </summary>
    /// <remarks>
    /// The implementation decides the language: a view asks for the definitions of the current UI culture and does
    /// not localize them itself. Every instance returned must be the caller's own, because a view degrades the
    /// layout in place for the user's permissions.
    /// </remarks>
    public interface IFormDefinitionSource
    {
        /// <summary>
        /// Retrieves the form schema of <paramref name="progId"/>.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the layout a record of <paramref name="progId"/> renders from.
        /// </summary>
        /// <param name="progId">The program identifier, which doubles as the layout identifier.</param>
        /// <param name="schema">The schema returned by <see cref="GetFormSchemaAsync"/>, already localized.</param>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        /// <returns>A layout the caller owns.</returns>
        Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the currency master, or <c>null</c> when none is available.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the unit master, or <c>null</c> when none is available.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the fetch.</param>
        Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default);
    }
}
