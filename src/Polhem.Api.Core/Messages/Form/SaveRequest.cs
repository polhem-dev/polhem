using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form Save operation.
    /// </summary>
    public sealed class SaveRequest : ApiRequest, ISaveRequest
    {
        /// <summary>
        /// Gets or sets the <c>DataSet</c> to persist. Each row's
        /// <c>RowState</c> dispatches to INSERT / UPDATE / DELETE on the
        /// server.
        /// </summary>
        public DataSet? DataSet { get; set; }
    }
}
