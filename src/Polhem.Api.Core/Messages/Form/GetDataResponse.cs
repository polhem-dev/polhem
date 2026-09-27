using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form GetData operation.
    /// </summary>
    public sealed class GetDataResponse : ApiResponse, IGetDataResponse
    {
        /// <summary>
        /// Gets or sets the loaded <c>DataSet</c>; <c>null</c> when no row
        /// matches <c>RowId</c>.
        /// </summary>
        public DataSet? DataSet { get; set; }
    }
}
