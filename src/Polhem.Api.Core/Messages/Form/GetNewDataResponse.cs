using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form GetNewData operation.
    /// </summary>
    public sealed class GetNewDataResponse : ApiResponse, IGetNewDataResponse
    {
        /// <summary>
        /// Gets or sets the blank <c>DataSet</c> skeleton; the master table
        /// carries one <c>Added</c> row seeded with FormSchema defaults and a
        /// server-issued <c>sys_rowid</c>.
        /// </summary>
        public DataSet? DataSet { get; set; }
    }
}
