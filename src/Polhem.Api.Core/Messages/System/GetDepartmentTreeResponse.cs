using Polhem.Api.Contracts.System;
using Polhem.Definition.Organization;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the get department tree operation. The tree is a typed object, written by the
    /// envelope's JSON on the Plain format and otherwise by the body codec the request declares.
    /// </summary>
    public sealed class GetDepartmentTreeResponse : ApiResponse, IGetDepartmentTreeResponse
    {
        /// <summary>
        /// Gets or sets the current company's department tree (<c>null</c> when no company is entered).
        /// </summary>
        public DepartmentTree? Tree { get; set; }
    }
}
