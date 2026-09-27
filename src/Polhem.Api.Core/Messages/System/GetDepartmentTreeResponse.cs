using Polhem.Api.Contracts.System;
using Polhem.Definition.Organization;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the get department tree operation. The tree is a typed object,
    /// serialised as JSON on the Plain wire format and MessagePack otherwise.
    /// </summary>
    public sealed class GetDepartmentTreeResponse : ApiResponse, IGetDepartmentTreeResponse
    {
        /// <summary>
        /// Gets or sets the current company's department tree (<c>null</c> when no company is entered).
        /// </summary>
        public DepartmentTree? Tree { get; set; }
    }
}
