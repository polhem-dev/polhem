using System.Globalization;
using System.Text;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Finds the detail aggregate calls of an expression (<c>Sum(Table.field)</c>, <c>Count(Table)</c>,
    /// <c>Min(Table.field)</c>, <c>Max(Table.field)</c>) and replaces each with a variable, so the expression engine
    /// sees an ordinary variable and the aggregate is computed by the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The match is syntactic and does not consult a schema, so its result can be cached per expression: whether
    /// the table and the field exist is checked where the aggregate is computed or validated. The engine registers
    /// no function by these names, so a call of this shape has no other meaning. A call of another shape, such as
    /// <c>Math.Max(a, b)</c> (a member access) or <c>Max(a, b)</c> (two arguments), is left as written.
    /// </para>
    /// <para>
    /// String and character literals are copied as they are, so text that only looks like a call is not rewritten.
    /// </para>
    /// </remarks>
    internal static class DetailAggregateRewriter
    {
        /// <summary>
        /// The prefix of the variables that stand in for aggregates. Field names are snake_case English and never
        /// start with two underscores, so the variables cannot shadow a field.
        /// </summary>
        public const string VariablePrefix = "__agg";

        /// <summary>
        /// Rewrites the aggregate calls of an expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The rewritten expression and its aggregates.</returns>
        public static DetailAggregateExpression Rewrite(string expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            if (!MayHoldAggregate(expression)) { return new DetailAggregateExpression(expression, []); }

            var aggregates = new List<DetailAggregate>();
            var builder = new StringBuilder(expression.Length);
            int i = 0;
            while (i < expression.Length)
            {
                char c = expression[i];
                if (c is '"' or '\'')
                {
                    int end = SkipLiteral(expression, i);
                    builder.Append(expression, i, end - i);
                    i = end;
                    continue;
                }
                if (!IsIdentifierStart(c))
                {
                    builder.Append(c);
                    i++;
                    continue;
                }

                int nameEnd = ReadIdentifier(expression, i);
                var name = expression[i..nameEnd];
                if (!IsMemberAccess(expression, i) && TryReadCall(expression, name, nameEnd, out var call, out int callEnd))
                {
                    var aggregate = aggregates.Find(a => a.Function == call.Function
                        && a.TableName == call.TableName && a.FieldName == call.FieldName);
                    if (aggregate is null)
                    {
                        aggregate = call with { VariableName = VariablePrefix + aggregates.Count.ToString(CultureInfo.InvariantCulture) };
                        aggregates.Add(aggregate);
                    }
                    builder.Append(aggregate.VariableName);
                    i = callEnd;
                    continue;
                }
                builder.Append(name);
                i = nameEnd;
            }
            return aggregates.Count == 0
                ? new DetailAggregateExpression(expression, [])
                : new DetailAggregateExpression(builder.ToString(), aggregates);
        }

        // A cheap pre-check, so an expression with no aggregate name is not scanned at all.
        private static bool MayHoldAggregate(string expression)
        {
            return expression.Contains("Sum", StringComparison.Ordinal)
                || expression.Contains("Count", StringComparison.Ordinal)
                || expression.Contains("Min", StringComparison.Ordinal)
                || expression.Contains("Max", StringComparison.Ordinal);
        }

        // Reads `(Table)` or `(Table.field)` after an aggregate name. The shape must match the function exactly:
        // Count takes a table, the others a table and a field.
        private static bool TryReadCall(string expression, string name, int position, out DetailAggregate call,
            out int end)
        {
            call = null!;
            end = position;
            DetailAggregateFunction function;
            switch (name)
            {
                case "Sum": function = DetailAggregateFunction.Sum; break;
                case "Count": function = DetailAggregateFunction.Count; break;
                case "Min": function = DetailAggregateFunction.Min; break;
                case "Max": function = DetailAggregateFunction.Max; break;
                default: return false;
            }

            int i = SkipWhitespace(expression, position);
            if (i >= expression.Length || expression[i] != '(') { return false; }
            i = SkipWhitespace(expression, i + 1);
            if (i >= expression.Length || !IsIdentifierStart(expression[i])) { return false; }
            int tableEnd = ReadIdentifier(expression, i);
            var table = expression[i..tableEnd];
            i = SkipWhitespace(expression, tableEnd);

            var field = string.Empty;
            if (i < expression.Length && expression[i] == '.')
            {
                i = SkipWhitespace(expression, i + 1);
                if (i >= expression.Length || !IsIdentifierStart(expression[i])) { return false; }
                int fieldEnd = ReadIdentifier(expression, i);
                field = expression[i..fieldEnd];
                i = SkipWhitespace(expression, fieldEnd);
            }
            if (i >= expression.Length || expression[i] != ')') { return false; }
            if ((function == DetailAggregateFunction.Count) != (field.Length == 0)) { return false; }

            call = new DetailAggregate(function, table, field, string.Empty);
            end = i + 1;
            return true;
        }

        // True when the identifier at `start` follows a `.`, as in `Math.Max`.
        private static bool IsMemberAccess(string expression, int start)
        {
            int i = start - 1;
            while (i >= 0 && char.IsWhiteSpace(expression[i])) { i--; }
            return i >= 0 && expression[i] == '.';
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

        private static int SkipWhitespace(string expression, int i)
        {
            while (i < expression.Length && char.IsWhiteSpace(expression[i])) { i++; }
            return i;
        }

        private static int ReadIdentifier(string expression, int start)
        {
            int i = start + 1;
            while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_')) { i++; }
            return i;
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
    }
}
