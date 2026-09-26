using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the FormSchema-driven <c>GetData</c> operation.
    /// </summary>
    public class GetDataResult : BusinessResult, IGetDataResponse
    {
        /// <summary>
        /// Gets or sets the loaded <c>DataSet</c>; <c>null</c> when no row
        /// matches the requested <c>RowId</c>.
        /// </summary>
        public DataSet? DataSet { get; set; }
    }
}
