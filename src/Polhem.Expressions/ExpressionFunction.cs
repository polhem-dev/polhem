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
    }
}
