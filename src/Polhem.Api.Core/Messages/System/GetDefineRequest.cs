using Polhem.Definition;
using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the get definition operation.
    /// </summary>
    public class GetDefineRequest : ApiRequest, IGetDefineRequest
    {
        /// <summary>
        /// Gets or sets the definition type.
        /// </summary>
        public DefineType DefineType { get; set; }

        /// <summary>
        /// Gets or sets the optional filter keys.
        /// </summary>
        public string[]? Keys { get; set; } = null;
    }
}
