namespace Polhem.Core.Expressions
{
    /// <summary>
    /// Something an expression uses outside the allowlist of <see cref="IExpressionAllowlist"/>.
    /// </summary>
    public sealed class ExpressionViolation
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ExpressionViolation"/>.
        /// </summary>
        /// <param name="message">What is not allowed, in English.</param>
        /// <param name="position">The offset in the expression it was found at; <c>null</c> when it has no single place.</param>
        public ExpressionViolation(string message, int? position)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Message = message;
            Position = position;
        }

        /// <summary>Gets what is not allowed, in English.</summary>
        public string Message { get; }

        /// <summary>
        /// Gets the offset in the expression the violation was found at; <c>null</c> when it has no single place, such
        /// as an expression that is too long.
        /// </summary>
        public int? Position { get; }
    }
}
