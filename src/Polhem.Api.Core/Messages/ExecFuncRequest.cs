using Polhem.Api.Contracts;

namespace Polhem.Api.Core.Messages
{
    /// <summary>
    /// API request type for executing a custom method.
    /// </summary>
    public sealed class ExecFuncRequest : ApiRequest, IExecFuncRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecFuncRequest"/> class.
        /// </summary>
        public ExecFuncRequest()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecFuncRequest"/> class with the specified function identifier.
        /// </summary>
        /// <param name="funcId">The custom method identifier.</param>
        public ExecFuncRequest(string funcId)
        {
            FuncId = funcId;
        }

        /// <summary>
        /// Gets or sets the custom method identifier.
        /// </summary>
        public string FuncId { get; set; } = string.Empty;
    }
}
