using Polhem.Api.Contracts.System;
using Polhem.Definition.Organization;

namespace Polhem.Business.System
{
    /// <summary>
    /// Output result for retrieving the current company's department tree.
    /// </summary>
    public sealed class GetDepartmentTreeResult : BusinessResult, IGetDepartmentTreeResponse
    {
        /// <summary>
        /// Gets or sets the current company's department tree (<c>null</c> when no company is entered).
        /// </summary>
        public DepartmentTree? Tree { get; set; }
    }
}
