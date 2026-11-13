using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Collections;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the FormSchema-driven <c>Save</c> operation.
    /// </summary>
    public sealed class SaveResult : BusinessResult, ISaveResponse
    {
        private Dictionary<string, int> _affectedRows = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the freshly re-loaded <c>DataSet</c> after Save.
        /// </summary>
        public DataSet? DataSet { get; set; }

        /// <summary>
        /// Gets or sets the per-table affected-row counts (table name → rows
        /// touched).
        /// </summary>
        public Dictionary<string, int> AffectedRows
        {
            get => _affectedRows;
            set => _affectedRows = value.WithIgnoreCaseKeys();
        }

        /// <inheritdoc />
        IReadOnlyDictionary<string, int> ISaveResponse.AffectedRows => AffectedRows;
    }
}
