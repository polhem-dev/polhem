using System.Reflection;
using DynamicExpresso;
using DynamicExpresso.Exceptions;
using Polhem.Core.Expressions;

namespace Polhem.Expressions
{
    public sealed partial class DynamicExpressoEvaluator
    {
        // The delegates of the helper functions; calling one of them is allowed. Built on first use: the order in which
        // the static fields of the two partial files initialize is not defined, so it cannot be a field initializer
        // that reads `s_helperFunctions`.
        private static IReadOnlySet<Delegate>? s_helperDelegates;

        private static IReadOnlySet<Delegate> HelperDelegates => s_helperDelegates ??=
            s_helperFunctions.SelectMany(helper => helper.MoreOverloads.Prepend(helper.Function)).ToHashSet();

        /// <inheritdoc />
        /// <remarks>
        /// The allowlist is fixed: the variables, the helper functions in <see cref="HelperFunctions"/>, operators,
        /// literals, and the members <see cref="IsAllowedMember"/> accepts. An expression longer than 1,000 characters,
        /// nested deeper than 16 parentheses or brackets, or holding a text literal longer than 200 characters is
        /// reported before it is parsed.
        /// </remarks>
        public IReadOnlyList<ExpressionViolation> FindViolations(string expression,
            IReadOnlyDictionary<string, Type> variableTypes, Type returnType)
        {
            ArgumentNullException.ThrowIfNull(expression);
            ArgumentNullException.ThrowIfNull(variableTypes);
            ArgumentNullException.ThrowIfNull(returnType);
            if (string.IsNullOrWhiteSpace(expression)) { return []; }
            if (ExpressionAllowlist.CheckSize(expression) is { } tooLarge) { return [tooLarge]; }

            var parameters = variableTypes.Select(pair => new Parameter(pair.Key, pair.Value)).ToArray();
            Lambda lambda;
            try
            {
                lock (_parseLock)
                {
                    lambda = _interpreter.Parse(expression, returnType, parameters);
                }
            }
            catch (Exception ex) when (ex is ParseException or InvalidOperationException)
            {
                // Not a violation: Validate reports an expression that does not parse.
                return [];
            }
            var violations = AllowlistVisitor.Find(lambda.Expression, expression, HelperDelegates).ToList();
            violations.AddRange(FindShadowedNames(expression, variableTypes));
            return violations;
        }

        // A variable named like a type or a helper function is read as a variable here, but at run time only the names
        // the expression is found to reference are passed in, and that search reads such a name as the type or the
        // function. The check and the run would then parse different expressions, so the name is refused.
        private List<ExpressionViolation> FindShadowedNames(string expression, IReadOnlyDictionary<string, Type> variableTypes)
        {
            var reserved = ReferencedTypes.Keys.Concat(s_helperFunctions.Select(helper => helper.Name))
                .ToHashSet(StringComparer.Ordinal);
            var violations = new List<ExpressionViolation>();
            foreach (var name in variableTypes.Keys.Where(reserved.Contains))
            {
                if (ExpressionAllowlist.FindWord(expression, name) is int position)
                {
                    violations.Add(new ExpressionViolation(
                        $"The field '{name}' has the name of a type or a function, so an expression cannot use it.", position));
                }
            }
            return violations;
        }

        /// <summary>
        /// Whether an expression from an untrusted author may use <paramref name="member"/>, so an editor can offer
        /// only what <see cref="FindViolations"/> accepts.
        /// </summary>
        /// <param name="member">A field, property, method or constructor.</param>
        /// <returns><see langword="true"/> when the member is on the allowlist.</returns>
        public static bool IsAllowedMember(MemberInfo member) => ExpressionAllowlist.IsAllowed(member);

        /// <summary>
        /// Whether an expression from an untrusted author may call <paramref name="function"/>, so an editor or a model
        /// is offered only the helpers <see cref="FindViolations"/> accepts.
        /// </summary>
        /// <param name="function">A function of <see cref="HelperFunctions"/>.</param>
        /// <returns><see langword="true"/> when the function is on the allowlist.</returns>
        public static bool IsAllowedFunction(ExpressionFunction function)
        {
            ArgumentNullException.ThrowIfNull(function);
            return ExpressionAllowlist.IsAllowedFunction(function.Name);
        }
    }
}
