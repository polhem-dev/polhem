using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using DynamicExpresso;
using DynamicExpresso.Exceptions;
using Polhem.Core;
using Polhem.Core.Expressions;

namespace Polhem.Expressions
{
    /// <summary>
    /// An <see cref="IExpressionEvaluator"/> backed by DynamicExpresso. Expressions are parsed and
    /// compiled once, cached by their text and parameter signature, then invoked per row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMPORTANT: every expression is compiled to a <see cref="Func{T, TResult}"/> of <c>object?[]</c> to
    /// <c>object?</c>, whatever its variables are, and never to the typed delegate DynamicExpresso would build.
    /// Without dynamic code, <see cref="LambdaExpression.Compile()"/> interprets the expression but still has to
    /// produce a delegate of the lambda's exact signature. On iOS (Mono, AOT-only) a signature with more than two
    /// parameters needs a <c>DynamicMethod</c> thunk, which throws <see cref="ExecutionEngineException"/>; under NativeAOT
    /// a signature that includes a value type has no native code. The fixed signature needs neither: it is a
    /// closed generic type known at compile time, and it takes one reference-type parameter.
    /// <c>InterpretedInvokerGateTests</c> fails when an interpreted expression ends up behind a runtime-generated
    /// thunk.
    /// </para>
    /// <para>
    /// The evaluator exposes the supplied variables, primitive types, a small set of common types
    /// (such as <see cref="Math"/>), and the helper functions registered in the constructor. A
    /// <em>type name</em> that was not registered — <c>File</c>, <c>Assembly</c>, <c>Process</c> —
    /// is an unknown identifier and fails at parse time.
    /// </para>
    /// <para>
    /// WARNING: that is not the same as a security sandbox, and this class must not be treated as
    /// one. Member access on a value is resolved by reflection, and <c>GetType()</c> is a public
    /// member of <see cref="object"/>, so any variable in scope is a starting point for
    /// <c>someField.GetType().Assembly</c> and onward through the reflection API. Upstream
    /// DynamicExpresso states the same limitation: it is not built to evaluate untrusted input.
    /// </para>
    /// <para>
    /// What keeps this safe in practice is the source of the expressions, not the parser:
    /// expressions live in definition files, and writing a definition is a deployment-time
    /// operation (<c>SystemBusinessObject.SaveDefine</c> is <c>LocalOnly</c>). Anything that would let a remote
    /// or lower-privileged caller supply expression text turns this into remote code execution on
    /// the server, so that boundary is the control — keep it intact.
    /// </para>
    /// </remarks>
    public sealed class DynamicExpressoEvaluator : IExpressionEvaluator
    {
        private readonly Interpreter _interpreter;
        private readonly ConcurrentDictionary<string, Func<object?[], object?>> _cache = new(StringComparer.Ordinal);
        private readonly object _parseLock = new();
        private readonly bool _preferInterpretation;

        /// <summary>
        /// Initializes a new instance of <see cref="DynamicExpressoEvaluator"/>.
        /// </summary>
        public DynamicExpressoEvaluator()
            : this(preferInterpretation: false)
        {
        }

        /// <summary>
        /// Initializes a new instance that interprets its expressions even where the runtime can compile them.
        /// </summary>
        /// <param name="preferInterpretation">
        /// <c>true</c> to take the path a runtime without dynamic code takes, so a desktop test can inspect it.
        /// </param>
        internal DynamicExpressoEvaluator(bool preferInterpretation)
        {
            _preferInterpretation = preferInterpretation;
            // Default options register primitive and common types (Math, Convert, ...) but no
            // reflection, IO, or arbitrary type loading — those remain unknown identifiers.
            _interpreter = new Interpreter(InterpreterOptions.Default);
            RegisterHelperFunctions(_interpreter);
        }

        /// <summary>
        /// Gets the types whose members an expression can reach: the types the interpreter references by
        /// name, and the return type of every helper function.
        /// </summary>
        /// <remarks>
        /// Exposed for <c>TrimmerDescriptorGateTests</c>, which checks that the embedded
        /// <c>ILLink.Descriptors.xml</c> preserves every one of them. DynamicExpresso finds their members by
        /// reflection, which the trimmer cannot see.
        /// </remarks>
        internal IReadOnlyCollection<Type> ExposedTypes =>
            _interpreter.ReferencedTypes.Select(reference => reference.Type)
                .Concat(s_helperFunctions.Select(helper => helper.Function.Method.ReturnType))
                .ToHashSet();

        /// <summary>
        /// Gets the compiled delegates in the cache.
        /// </summary>
        /// <remarks>
        /// Exposed for <c>InterpretedInvokerGateTests</c>, which checks that none of them is a runtime-generated
        /// thunk when the evaluator interprets.
        /// </remarks>
        internal IReadOnlyCollection<Delegate> CompiledInvokers => _cache.Values.ToArray();

        /// <summary>
        /// The time zone the helper functions read, for the duration of one <c>Evaluate</c> call.
        /// </summary>
        /// <remarks>
        /// DynamicExpresso binds a helper to a fixed delegate at registration, so a per-call zone has
        /// to reach the delegate through captured state. This field is that channel, and it is a
        /// confined implementation detail: the public contract takes the zone as an argument
        /// (ADR-032 D13), and this evaluator is a singleton whose helpers must therefore not hold one
        /// user's zone.
        ///
        /// <c>[ThreadStatic]</c> rather than <c>AsyncLocal</c> because the window is one synchronous
        /// <c>lambda.Invoke</c> — no await intervenes, so the value cannot leak to another logical
        /// call. Keeping the zone out of the cache key is the point: one compiled lambda serves every
        /// user regardless of zone.
        /// </remarks>
        // IDE1006 is suppressed here. Naming rules in `.editorconfig` cannot see attributes, so
        // there is no way to express "a `[ThreadStatic]` field takes the `t_` prefix". The prefix
        // follows the .NET runtime's own convention, and it earns its place: it tells the reader at
        // a glance that the field is per-thread rather than process-wide. An `_` prefix would make
        // it read as an instance field.
#pragma warning disable IDE1006
        [ThreadStatic]
        private static string? t_timeZoneId;

        // The basis travels through the same channel as the zone. `Now()` depends on the basis of the
        // data set being evaluated, which only the caller knows (ADR-032 D12).
        [ThreadStatic]
        private static DateTimeBasis t_basis;
#pragma warning restore IDE1006

        /// <summary>
        /// The curated helper functions available to every expression.
        /// </summary>
        private static readonly (string Name, Delegate Function)[] s_helperFunctions =
        [
            // `Today()` yields a calendar day in the user's zone (ADR-032 D12). It is a DateOnly
            // like every other date in the framework; `ExpressionPolicy.CoerceValue` widens it when
            // the result lands in a DataSet cell, the one place a date must be a DateTime.
            ("Today", (Func<DateOnly>)(() => FrameworkClock.Today(t_timeZoneId ?? string.Empty))),
            // `Now()` follows the basis of the data set being evaluated, not the user's zone alone, so the
            // value it writes or compares shares the basis of the cells around it: UTC in the server's
            // pre-save pass, the user's zone in a client preview (ADR-032 D12).
            ("Now", (Func<DateTime>)(() => FrameworkClock.Now(t_timeZoneId ?? string.Empty, t_basis))),
            ("UtcNow", (Func<DateTime>)(() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))),
            ("IsNullOrEmpty", (Func<string?, bool>)string.IsNullOrEmpty),
            ("IsNullOrWhiteSpace", (Func<string?, bool>)string.IsNullOrWhiteSpace),
        ];

        /// <summary>
        /// Registers the curated helper functions available to every expression.
        /// </summary>
        /// <param name="interpreter">The interpreter to register into.</param>
        private static void RegisterHelperFunctions(Interpreter interpreter)
        {
            // Expose Guid so expressions can test key/reference fields (for example
            // `customer_rowid != Guid.Empty`). Guid is a value type with no IO surface.
            interpreter.Reference(typeof(Guid));

            foreach (var (name, function) in s_helperFunctions)
                interpreter.SetFunction(name, function);
        }

        /// <inheritdoc />
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "The overload pair mirrors IExpressionEvaluator, which carries the same suppression. Both overloads gained the trailing basis parameter together (ADR-032 D12), so no caller can rebind from one to the other, which is the hazard RS0026 guards against.")]
        public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> variables, Type returnType,
            string timeZoneId = "", DateTimeBasis basis = DateTimeBasis.UserZone)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            ArgumentNullException.ThrowIfNull(variables);
            ArgumentNullException.ThrowIfNull(returnType);

            // Stable parameter order so the cache key and the compiled signature are deterministic.
            var names = variables.Keys.ToArray();
            Array.Sort(names, StringComparer.Ordinal);

            var invoker = GetOrCompile(expression, returnType, names, variables);

            // Positional, in the sorted name order the invoker was compiled against.
            var arguments = new object?[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                arguments[i] = variables[names[i]] ?? string.Empty;
            }

            return InvokeWithZone(invoker, arguments, timeZoneId, basis);
        }

        /// <inheritdoc />
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "See the justification on the non-generic overload above.")]
        public T Evaluate<T>(string expression, IReadOnlyDictionary<string, object?> variables, string timeZoneId = "",
            DateTimeBasis basis = DateTimeBasis.UserZone)
        {
            var result = Evaluate(expression, variables, typeof(T), timeZoneId, basis);
            return result is null ? default! : (T)result;
        }

        /// <summary>
        /// Invokes a compiled expression with <see cref="t_timeZoneId"/> and <see cref="t_basis"/> set for
        /// the call.
        /// </summary>
        /// <param name="invoker">The compiled expression to invoke.</param>
        /// <param name="arguments">The variable values, in the order the invoker was compiled against.</param>
        /// <param name="timeZoneId">The zone the helper functions should observe for this invocation.</param>
        /// <param name="basis">The data-set basis <c>Now()</c> should observe for this invocation.</param>
        /// <remarks>
        /// Both values are scoped to this invocation only, and whatever an outer call had is restored: a
        /// computed field's expression can be evaluated inside another evaluation. Kept static so the
        /// whole read/write protocol for the ambient fields sits next to the fields themselves.
        /// </remarks>
        private static object? InvokeWithZone(Func<object?[], object?> invoker, object?[] arguments, string timeZoneId,
            DateTimeBasis basis)
        {
            var previousZone = t_timeZoneId;
            var previousBasis = t_basis;
            t_timeZoneId = timeZoneId;
            t_basis = basis;
            try
            {
                return invoker(arguments);
            }
            finally
            {
                t_timeZoneId = previousZone;
                t_basis = previousBasis;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetReferencedVariables(string expression)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            try
            {
                lock (_parseLock)
                {
                    return _interpreter.DetectIdentifiers(expression).UnknownIdentifiers.ToArray();
                }
            }
            catch (ParseException ex)
            {
                throw new ExpressionEvaluationException(
                    $"Failed to parse expression: {ex.Message}", ex) { Expression = expression };
            }
        }

        /// <summary>
        /// Returns the cached compiled expression for the expression/return-type/parameter signature,
        /// compiling and caching it on first use.
        /// </summary>
        private Func<object?[], object?> GetOrCompile(string expression, Type returnType, string[] names,
            IReadOnlyDictionary<string, object?> variables)
        {
            var key = BuildCacheKey(expression, returnType, names, variables);
            return _cache.GetOrAdd(key, _ =>
            {
                var parameters = new Parameter[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var type = variables[names[i]]?.GetType() ?? typeof(object);
                    parameters[i] = new Parameter(names[i], type);
                }
                Lambda lambda;
                try
                {
                    lock (_parseLock)
                    {
                        lambda = _interpreter.Parse(expression, returnType, parameters);
                    }
                }
                catch (ParseException ex)
                {
                    throw new ExpressionEvaluationException(
                        $"Failed to parse expression: {ex.Message}", ex) { Expression = expression };
                }
                // Never `lambda.Invoke` or `lambda.Compile`: both compile the typed delegate that a runtime
                // without dynamic code cannot create (see the class remarks).
                return BuildInvoker(lambda, names).Compile(_preferInterpretation);
            });
        }

        /// <summary>
        /// Wraps a parsed expression in a lambda that takes the variable values as one positional
        /// <c>object?[]</c> and returns the boxed result.
        /// </summary>
        /// <param name="lambda">The parsed expression.</param>
        /// <param name="names">The sorted variable names; a value's position in the argument array is its name's
        /// position here.</param>
        /// <returns>The lambda to compile.</returns>
        /// <remarks>
        /// The parameters the expression uses become block variables, each assigned by unboxing its array slot to
        /// the type it was parsed with, so the body DynamicExpresso built is reused unchanged.
        /// </remarks>
        private static Expression<Func<object?[], object?>> BuildInvoker(Lambda lambda, string[] names)
        {
            var arguments = Expression.Parameter(typeof(object[]), "arguments");
            var variables = new List<ParameterExpression>();
            var statements = new List<Expression>();
            foreach (var parameter in lambda.UsedParameters)
            {
                var index = Array.IndexOf(names, parameter.Name);
                var slot = Expression.ArrayIndex(arguments, Expression.Constant(index));
                variables.Add(parameter.Expression);
                statements.Add(Expression.Assign(parameter.Expression, Expression.Convert(slot, parameter.Type)));
            }
            statements.Add(Expression.Convert(lambda.Expression, typeof(object)));
            return Expression.Lambda<Func<object?[], object?>>(
                Expression.Block(typeof(object), variables, statements), arguments);
        }

        /// <summary>
        /// Builds a deterministic cache key from the expression, its return type, and the ordered
        /// parameter name/type signature.
        /// </summary>
        private static string BuildCacheKey(string expression, Type returnType, string[] names,
            IReadOnlyDictionary<string, object?> variables)
        {
            var builder = new StringBuilder(returnType.FullName)
                .Append('|').Append(expression).Append('|');
            foreach (var name in names)
            {
                var type = variables[name]?.GetType() ?? typeof(object);
                builder.Append(name).Append(':').Append(type.FullName).Append(',');
            }
            return builder.ToString();
        }
    }
}
