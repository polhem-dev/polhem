namespace Polhem.Core.Exceptions
{
    /// <summary>
    /// Thrown when a value the caller sent cannot be read as the type it stands for, such as a filter value that is not
    /// a valid date for the field it is compared with.
    /// </summary>
    /// <remarks>
    /// The JSON-RPC transport sends it as <c>InvalidParams</c> (<c>-32602</c>) with its message, so throw it only with
    /// text that names what the caller sent: a field name and a type, never a table, a column or a server detail. It
    /// derives from <see cref="ArgumentException"/>, so an in-process caller that catches that type still sees it.
    /// </remarks>
    public sealed class InvalidRequestValueException : ArgumentException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestValueException"/> class with the specified message.
        /// </summary>
        /// <param name="message">The message naming the value that could not be read.</param>
        public InvalidRequestValueException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestValueException"/> class with the specified message
        /// and a reference to the underlying cause.
        /// </summary>
        /// <param name="message">The message naming the value that could not be read.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public InvalidRequestValueException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
