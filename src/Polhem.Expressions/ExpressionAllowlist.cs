using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Polhem.Core.Expressions;

namespace Polhem.Expressions
{
    /// <summary>
    /// What an expression from an untrusted author may use, and the size limits it must stay within: the single source
    /// of the allowlist <see cref="DynamicExpressoEvaluator"/> enforces through <see cref="IExpressionAllowlist"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The members are chosen for form logic: comparing and computing field values, dates and text. Anything that
    /// reaches types (<c>GetType</c>, <c>typeof</c>), queries sequences, or allocates in proportion to an argument
    /// (<c>PadLeft(n)</c>, <c>new string(c, n)</c>) is left out. <c>DateTime.Now</c>, <c>Today</c> and <c>UtcNow</c>
    /// are left out as well: they read the server's clock and zone, while the <c>Now()</c>, <c>Today()</c> and
    /// <c>UtcNow()</c> helpers follow the user's zone and the data's time basis (ADR-032 D12). An editor that lists
    /// members through <see cref="DynamicExpressoEvaluator.IsAllowedMember"/> therefore offers the helpers instead.
    /// </para>
    /// <para>
    /// The size limits keep an expression from exhausting the parser: a deep enough nesting overflows its stack, which
    /// ends the process instead of throwing.
    /// </para>
    /// </remarks>
    internal static class ExpressionAllowlist
    {
        /// <summary>The longest expression text accepted.</summary>
        public const int MaxLength = 1000;

        /// <summary>The deepest nesting of parentheses and brackets accepted.</summary>
        public const int MaxDepth = 16;

        /// <summary>The longest string literal accepted.</summary>
        public const int MaxStringLiteralLength = 200;

        /// <summary>The types a value in an allowed expression may have: variables, literals, results and conversions.</summary>
        public static readonly IReadOnlySet<Type> ValueTypes = new HashSet<Type>
        {
            typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
            typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(string),
            typeof(DateTime), typeof(DateOnly), typeof(TimeSpan), typeof(DayOfWeek), typeof(Guid), typeof(byte[]),
            typeof(object), typeof(object[]),
        };

        // Members by declaring type and name; every overload of an allowed name is allowed. Operators (`op_*`) of the
        // value types are allowed separately.
        private static readonly Dictionary<Type, HashSet<string>> s_members = new()
        {
            [typeof(string)] =
            [
                "Length", "Contains", "StartsWith", "EndsWith", "Trim", "TrimStart", "TrimEnd", "ToUpper", "ToLower",
                "ToUpperInvariant", "ToLowerInvariant", "Substring", "IndexOf", "IsNullOrEmpty", "IsNullOrWhiteSpace",
                "Concat", "Equals", "CompareOrdinal", "Empty",
            ],
            [typeof(DateTime)] =
            [
                "Year", "Month", "Day", "Hour", "Minute", "Second", "Date", "DayOfWeek", "DayOfYear", "AddDays",
                "AddMonths", "AddYears", "AddHours", "AddMinutes", "MinValue", "MaxValue", "Equals", "CompareTo",
                "DaysInMonth",
            ],
            [typeof(DateOnly)] =
            [
                "Year", "Month", "Day", "DayOfWeek", "DayOfYear", "DayNumber", "AddDays", "AddMonths", "AddYears",
                "MinValue", "MaxValue", "Equals", "CompareTo",
            ],
            [typeof(TimeSpan)] =
            [
                "Days", "Hours", "Minutes", "Seconds", "TotalDays", "TotalHours", "TotalMinutes", "TotalSeconds",
            ],
            [typeof(Math)] = ["Abs", "Round", "Floor", "Ceiling", "Truncate", "Max", "Min", "Sign", "PI"],
            [typeof(Guid)] = ["Empty", "Equals"],
            [typeof(Convert)] = ["ToDecimal", "ToInt32", "ToInt64", "ToDouble", "ToString", "ToBoolean"],
            [typeof(object)] = ["ToString", "Equals"],
        };

        // The numeric, Boolean and character types share their small surface.
        private static readonly HashSet<string> s_scalarMembers = ["ToString", "Equals", "CompareTo", "MaxValue", "MinValue"];

        private static readonly HashSet<Type> s_scalarTypes =
        [
            typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
            typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        ];

        /// <summary>
        /// Whether an expression from an untrusted author may use <paramref name="member"/>: a listed member, an
        /// operator of a value type, or a constructor of <see cref="DateTime"/> or <see cref="DateOnly"/> that takes
        /// whole numbers.
        /// </summary>
        public static bool IsAllowed(MemberInfo member)
        {
            ArgumentNullException.ThrowIfNull(member);
            var type = member.DeclaringType;
            if (type is null) { return false; }
            if (Nullable.GetUnderlyingType(type) is { } underlying)
                return ValueTypes.Contains(underlying) && member.Name is "Value" or "HasValue" or "GetValueOrDefault";
            if (member is ConstructorInfo constructor)
            {
                return (type == typeof(DateTime) || type == typeof(DateOnly))
                    && constructor.GetParameters().All(parameter => parameter.ParameterType == typeof(int));
            }
            if (member is MethodInfo { IsSpecialName: true } method && method.Name.StartsWith("op_", StringComparison.Ordinal))
                return ValueTypes.Contains(type);
            if (s_members.TryGetValue(type, out var names) && names.Contains(member.Name)) { return true; }
            return s_scalarTypes.Contains(type) && s_scalarMembers.Contains(member.Name);
        }

        /// <summary>Whether a value of <paramref name="type"/> may appear in an allowed expression.</summary>
        public static bool IsValueType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return ValueTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);
        }

        /// <summary>
        /// Checks the size of an expression's text before it is parsed; <c>null</c> when it is within the limits.
        /// </summary>
        public static ExpressionViolation? CheckSize(string expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            if (expression.Length > MaxLength)
            {
                return new ExpressionViolation(string.Create(CultureInfo.InvariantCulture,
                    $"The expression is longer than {MaxLength} characters."), null);
            }

            int depth = 0;
            int i = 0;
            while (i < expression.Length)
            {
                char c = expression[i];
                if (c is '"' or '\'')
                {
                    int start = i;
                    i = SkipLiteral(expression, i);
                    if (c == '"' && i - start - 2 > MaxStringLiteralLength)
                    {
                        return new ExpressionViolation(string.Create(CultureInfo.InvariantCulture,
                            $"A text literal is longer than {MaxStringLiteralLength} characters."), start);
                    }
                    continue;
                }
                if (c is '(' or '[')
                {
                    depth++;
                    if (depth > MaxDepth)
                    {
                        return new ExpressionViolation(string.Create(CultureInfo.InvariantCulture,
                            $"The expression nests deeper than {MaxDepth} levels."), i);
                    }
                }
                else if (c is ')' or ']')
                {
                    depth = Math.Max(0, depth - 1);
                }
                i++;
            }
            return null;
        }

        /// <summary>The offset of the first occurrence of <paramref name="name"/> as a whole word; <c>null</c> when it does not occur.</summary>
        public static int? FindWord(string expression, string name)
        {
            var match = Regex.Match(expression, $@"(?<![\w]){Regex.Escape(name)}(?![\w])", RegexOptions.None,
                TimeSpan.FromSeconds(1));
            return match.Success ? match.Index : null;
        }

        private static int SkipLiteral(string expression, int start)
        {
            char quote = expression[start];
            int i = start + 1;
            while (i < expression.Length)
            {
                if (expression[i] == '\\') { i += 2; continue; }
                if (expression[i] == quote) { return i + 1; }
                i++;
            }
            return expression.Length;
        }
    }
}
