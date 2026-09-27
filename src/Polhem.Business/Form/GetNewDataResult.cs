using System.Data;
using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the FormSchema-driven <c>GetNewData</c> operation.
    /// </summary>
    public sealed class GetNewDataResult : BusinessResult, IGetNewDataResponse
    {
        /// <summary>
        /// Gets or sets the blank <c>DataSet</c> skeleton.
        /// </summary>
        public DataSet? DataSet { get; set; }
    }
}
