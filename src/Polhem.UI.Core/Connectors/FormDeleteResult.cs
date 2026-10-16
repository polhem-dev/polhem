using Polhem.Api.Contracts.Form;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// The result of a delete through an <see cref="IFormApiConnector"/> implemented without a Polhem backend.
    /// </summary>
    /// <param name="RowsAffected">The number of rows deleted.</param>
    public sealed record FormDeleteResult(int RowsAffected) : IDeleteResponse;
}
