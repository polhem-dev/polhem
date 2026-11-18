using System.Reflection;

namespace Polhem.Expressions
{
    /// <summary>
    /// Describes a helper function that every expression can call, such as <c>Today()</c>.
    /// </summary>
    /// <remarks>
    /// The list is <see cref="DynamicExpressoEvaluator.HelperFunctions"/>, read from the functions the evaluator
    /// registers, so a definition editor can offer them without keeping its own copy.
    /// </remarks>
    public sealed class ExpressionFunction
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ExpressionFunction"/>.
        /// </summary>
        /// <param name="name">The name an expression calls the function by.</param>
        /// <param name="returnType">The type the function returns.</param>
        /// <param name="parameters">The parameters of the function, in order.</param>
        public ExpressionFunction(string name, Type returnType, IReadOnlyList<ParameterInfo> parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(parameters);
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ExpressionFunction"/> with a description.
        /// </summary>
        /// <param name="name">The name an expression calls the function by.</param>
        /// <param name="returnType">The type the function returns.</param>
        /// <param name="parameters">The parameters of the function, in order.</param>
        /// <param name="summary">What the function returns, in one English sentence.</param>
        /// <param name="example">An expression that calls the function.</param>
        public ExpressionFunction(string name, Type returnType, IReadOnlyList<ParameterInfo> parameters, string summary,
            string example)
            : this(name, returnType, parameters)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentNullException.ThrowIfNull(example);
            Summary = summary;
            Example = example;
        }

        /// <summary>
        /// Gets the name an expression calls the function by.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type the function returns.
        /// </summary>
        public Type ReturnType { get; }

        /// <summary>
        /// Gets the parameters of the function, in order.
        /// </summary>
        public IReadOnlyList<ParameterInfo> Parameters { get; }

        /// <summary>
        /// Gets what the function returns, in one English sentence; empty when the function was described without one.
        /// </summary>
        /// <remarks>
        /// A definition editor shows it next to the function and translates it for its own interface; a tool that
        /// writes expressions for a person reads it to choose the function.
        /// </remarks>
        public string Summary { get; } = string.Empty;

        /// <summary>
        /// Gets an expression that calls the function, such as <c>Today().AddDays(7)</c>; empty when the function was
        /// described without one.
        /// </summary>
        /// <remarks>
        /// The example may name form fields, which differ from form to form, so it is an illustration to read, not an
        /// expression to evaluate as it stands.
        /// </remarks>
        public string Example { get; } = string.Empty;
    }
}
