using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form Save operation.
    /// </summary>
    public sealed class SaveResponse : ApiResponse, ISaveResponse
    {
        private Dictionary<string, int> _affectedRows = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the freshly re-loaded <c>DataSet</c>; merges in
        /// server-generated columns and resets all surviving rows to
        /// <c>RowState == Unchanged</c>.
        /// </summary>
        public DataSet? DataSet { get; set; }

        /// <summary>
        /// Gets or sets the per-table affected-row counts (table name → rows
        /// touched).
        /// </summary>
        public Dictionary<string, int> AffectedRows
        {
            get => _affectedRows;
            // A reader that replaces the dictionary, as the wire codecs do, must not lose the case-insensitive keys.
            set => _affectedRows = Equals(value?.Comparer, StringComparer.OrdinalIgnoreCase)
                ? value!
                : new(value ?? [], StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        IReadOnlyDictionary<string, int> ISaveResponse.AffectedRows => AffectedRows;
    }
}
