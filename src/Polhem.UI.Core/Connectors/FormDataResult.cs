using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// A record returned by an <see cref="IFormApiConnector"/> implemented without a Polhem backend: from a new,
    /// load or save call.
    /// </summary>
    /// <param name="DataSet">The record: the master row and its details.</param>
    public sealed record FormDataResult(DataSet? DataSet) : IGetNewDataResponse, IGetDataResponse, ISaveResponse
    {
        /// <summary>
        /// Gets the rows a save affected per table; empty when not reported.
        /// </summary>
        public IReadOnlyDictionary<string, int> AffectedRows { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
