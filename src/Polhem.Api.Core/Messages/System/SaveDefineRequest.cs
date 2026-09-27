using Polhem.Definition;
using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the save definition operation.
    /// </summary>
    public sealed class SaveDefineRequest : ApiRequest, ISaveDefineRequest
    {
        /// <summary>
        /// Gets or sets the definition type.
        /// </summary>
        public DefineType DefineType { get; set; }

        /// <summary>
        /// Gets or sets the definition XML content.
        /// </summary>
        public string Xml { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional filter keys.
        /// </summary>
        public string[]? Keys { get; set; } = null;
    }
}
