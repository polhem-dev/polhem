namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Thrown by the JSON-RPC pipeline when the request parameters are not a Polhem payload, or a
    /// <c>Plain</c> body cannot be read into the type the addressed method takes.
    /// </summary>
    /// <remarks>
    /// Maps to <see cref="JsonRpcErrorCode.InvalidParams"/> through <see cref="JsonRpcErrorContract"/>,
    /// with a fixed message: the parser's own text describes the server's types.
    /// </remarks>
    internal sealed class InvalidParamsException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidParamsException"/> class.
        /// </summary>
        /// <param name="message">The message for the server log; the caller receives a fixed one.</param>
        /// <param name="innerException">The parser failure.</param>
        public InvalidParamsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
