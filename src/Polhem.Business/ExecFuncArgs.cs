using Polhem.Api.Contracts;

namespace Polhem.Business
{
    /// <summary>
    /// Input arguments for executing a custom method.
    /// </summary>
    public sealed class ExecFuncArgs : BusinessArgs, IExecFuncRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecFuncArgs"/> class.
        /// </summary>
        public ExecFuncArgs()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecFuncArgs"/> class with the specified function identifier.
        /// </summary>
        /// <param name="funcId">The custom method identifier.</param>
        public ExecFuncArgs(string funcId)
        {
            FuncId = funcId;
        }

        /// <summary>
        /// Gets or sets the custom method identifier.
        /// </summary>
        public string FuncId { get; set; } = string.Empty;
    }
}
