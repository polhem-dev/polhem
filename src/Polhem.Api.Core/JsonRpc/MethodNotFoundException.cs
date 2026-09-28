namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Thrown by <see cref="JsonRpcExecutor"/> when the action part of a <c>progId.action</c> method
    /// names nothing the business object exposes as an action.
    /// </summary>
    /// <remarks>
    /// A type of its own rather than <see cref="MissingMethodException"/>: that one is also what the
    /// runtime throws from inside a business object when a binding breaks, which is a server fault, and
    /// mapping it by type would report such a fault as a caller mistake. It maps to
    /// <see cref="JsonRpcErrorCode.MethodNotFound"/> through <see cref="JsonRpcErrorContract"/>.
    /// </remarks>
    internal sealed class MethodNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MethodNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The message for the server log; the caller receives a fixed one.</param>
        public MethodNotFoundException(string message)
            : base(message)
        {
        }
    }
}
