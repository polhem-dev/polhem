using System.Globalization;
using System.Text;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Finds the detail aggregate calls of an expression (<c>Sum(Table.field)</c>, <c>Count(Table)</c>,
    /// <c>Min(Table.field)</c>, <c>Max(Table.field)</c>, and the conditional <c>Count(Table, condition)</c>,
    /// <c>Any(Table, condition)</c> and <c>All(Table, condition)</c>) and replaces each with a variable, so the
    /// expression engine sees an ordinary variable and the aggregate is computed by the caller.
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
            if (!MayHoldAggregate(expression)) { return new DetailAggregateExpression(expression, [], []); }

            var aggregates = new List<DetailAggregate>();
            var spans = new List<DetailAggregateSpan>();
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
                if (!IsMemberAccess(expression, i)
                    && TryReadCall(expression, name, nameEnd, out var call, out int callEnd, out int conditionStart))
                {
                    var aggregate = aggregates.Find(a => a.Function == call.Function
                        && a.TableName == call.TableName && a.FieldName == call.FieldName && a.Condition == call.Condition);
                    if (aggregate is null)
                    {
                        aggregate = call with { VariableName = VariablePrefix + aggregates.Count.ToString(CultureInfo.InvariantCulture) };
                        aggregates.Add(aggregate);
                    }
                    spans.Add(new DetailAggregateSpan(i, callEnd - i, builder.Length, aggregate.VariableName, conditionStart));
                    builder.Append(aggregate.VariableName);
                    i = callEnd;
                    continue;
                }
                builder.Append(name);
                i = nameEnd;
            }
            return aggregates.Count == 0
                ? new DetailAggregateExpression(expression, [], [])
                : new DetailAggregateExpression(builder.ToString(), aggregates, spans);
        }

        // A cheap pre-check, so an expression with no aggregate name is not scanned at all.
        private static bool MayHoldAggregate(string expression)
        {
            return expression.Contains("Sum", StringComparison.Ordinal)
                || expression.Contains("Count", StringComparison.Ordinal)
                || expression.Contains("Min", StringComparison.Ordinal)
                || expression.Contains("Max", StringComparison.Ordinal)
                || expression.Contains("Any", StringComparison.Ordinal)
                || expression.Contains("All", StringComparison.Ordinal);
        }

        // Reads `(Table)`, `(Table.field)` or `(Table, condition)` after an aggregate name. The shape must match the
        // function exactly: Sum, Min and Max take a table and a field; Count takes a table and an optional condition;
        // Any and All take a table and a condition.
        private static bool TryReadCall(string expression, string name, int position, out DetailAggregate call,
            out int end, out int conditionStart)
        {
            call = null!;
            end = position;
            conditionStart = -1;
            DetailAggregateFunction function;
            switch (name)
            {
                case "Sum": function = DetailAggregateFunction.Sum; break;
                case "Count": function = DetailAggregateFunction.Count; break;
                case "Min": function = DetailAggregateFunction.Min; break;
                case "Max": function = DetailAggregateFunction.Max; break;
                case "Any": function = DetailAggregateFunction.Any; break;
                case "All": function = DetailAggregateFunction.All; break;
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
            var condition = string.Empty;
            if (i < expression.Length && expression[i] == '.')
            {
                i = SkipWhitespace(expression, i + 1);
                if (i >= expression.Length || !IsIdentifierStart(expression[i])) { return false; }
                int fieldEnd = ReadIdentifier(expression, i);
                field = expression[i..fieldEnd];
                i = SkipWhitespace(expression, fieldEnd);
            }
            else if (i < expression.Length && expression[i] == ',')
            {
                int close = FindClosingParenthesis(expression, i + 1);
                if (close < 0) { return false; }
                var raw = expression[(i + 1)..close];
                condition = raw.Trim();
                if (condition.Length == 0) { return false; }
                conditionStart = i + 1 + (raw.Length - raw.TrimStart().Length);
                i = close;
            }
            if (i >= expression.Length || expression[i] != ')') { return false; }

            bool shapeFits = function switch
            {
                DetailAggregateFunction.Count => field.Length == 0,
                DetailAggregateFunction.Any or DetailAggregateFunction.All => field.Length == 0 && condition.Length > 0,
                _ => field.Length > 0,
            };
            if (!shapeFits) { conditionStart = -1; return false; }

            call = new DetailAggregate(function, table, field, string.Empty) { Condition = condition };
            end = i + 1;
            return true;
        }

        // The offset of the parenthesis that closes the call, skipping nested parentheses and literals; -1 when the
        // expression ends first.
        private static int FindClosingParenthesis(string expression, int start)
        {
            int depth = 0;
            int i = start;
            while (i < expression.Length)
            {
                char c = expression[i];
                if (c is '"' or '\'') { i = SkipLiteral(expression, i); continue; }
                if (c == '(') { depth++; }
                else if (c == ')')
                {
                    if (depth == 0) { return i; }
                    depth--;
                }
                i++;
            }
            return -1;
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
