using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Definition.Numbers;

namespace Polhem.Definition.Forms
{
    public sealed partial class FormExpressionCalculator
    {
        /// <summary>
        /// Cached per expression: the expression with its detail aggregates replaced by variables.
        /// </summary>
        private readonly ConcurrentDictionary<string, DetailAggregateExpression> _aggregateExpressions =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Builds the "detail table → aggregated detail fields" map of a schema's master table: for each master
        /// <see cref="FormField.ValueExpression"/> that aggregates a detail table, the table becomes a key and the
        /// aggregated fields its value. Clients use it to decide whether an edit, an added row or a deleted row of a
        /// detail table forces the master row to recompute.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <returns>
        /// A map from detail table name to the detail fields its aggregates read, both compared case-insensitively.
        /// A table aggregated only by <c>Count</c> maps to an empty set: adding or deleting a row matters, editing
        /// one does not.
        /// </returns>
        public static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAggregateDependencyMap(FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);

            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (schema.MasterTable?.Fields is { } fields)
            {
                foreach (var field in fields)
                {
                    if (StringUtilities.IsEmpty(field.ValueExpression)) { continue; }
                    foreach (var aggregate in DetailAggregateRewriter.Rewrite(field.ValueExpression).Aggregates)
                    {
                        if (!map.TryGetValue(aggregate.TableName, out var detailFields))
                        {
                            detailFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            map[aggregate.TableName] = detailFields;
                        }
                        if (aggregate.FieldName.Length > 0) { detailFields.Add(aggregate.FieldName); }
                    }
                }
            }

            var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in map)
                result[pair.Key] = pair.Value;
            return result;
        }

        private DetailAggregateExpression ParseAggregates(string expression)
        {
            return _aggregateExpressions.GetOrAdd(expression, DetailAggregateRewriter.Rewrite);
        }

        /// <summary>
        /// Computes the detail aggregates of an expression into <paramref name="variables"/> and returns the
        /// expression to evaluate: the rewritten one when it holds an aggregate, the original otherwise.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="schema">The form schema.</param>
        /// <param name="formTable">The table the expression is evaluated on; aggregates need the master table.</param>
        /// <param name="dataSet">The data set whose detail rows are aggregated.</param>
        /// <param name="variables">The row's variables, which receive one entry per aggregate.</param>
        /// <param name="roundingContext">
        /// Rounds each detail value to its own field's decimals before it is summed; <c>null</c> takes the values as
        /// they are.
        /// </param>
        /// <exception cref="ExpressionEvaluationException">
        /// An aggregate is used outside the master table, or names a table or field the schema does not have.
        /// </exception>
        private string BindAggregates(string expression, FormSchema schema, FormTable formTable, DataSet? dataSet,
            Dictionary<string, object?> variables, RoundingContext? roundingContext)
        {
            var parsed = ParseAggregates(expression);
            if (!parsed.HasAggregates) { return expression; }
            if (!IsMasterTable(schema, formTable))
                throw new ExpressionEvaluationException(AggregateOutsideMasterMessage(formTable));

            foreach (var aggregate in parsed.Aggregates)
            {
                var (detailTable, field, error) = ResolveAggregate(aggregate, schema);
                if (error is not null) { throw new ExpressionEvaluationException(error); }
                variables[aggregate.VariableName] =
                    ComputeAggregate(aggregate.Function, schema, detailTable!, field, dataSet, roundingContext);
            }
            return parsed.Expression;
        }

        private static object ComputeAggregate(DetailAggregateFunction function, FormSchema schema,
            FormTable detailTable, FormField? field, DataSet? dataSet, RoundingContext? roundingContext)
        {
            var rows = LiveRows(dataSet, detailTable.TableName);
            if (function == DetailAggregateFunction.Count) { return rows.Count; }

            var values = new List<object>(rows.Count);
            foreach (var row in rows)
                values.Add(AggregateValue(row, field!, schema, roundingContext));

            if (function == DetailAggregateFunction.Sum)
            {
                decimal sum = 0m;
                foreach (var value in values)
                    sum += Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return sum;
            }

            if (values.Count == 0) { return ExpressionPolicy.CoerceValue(null, field!.DbType); }
            var comparer = Comparer<object>.Default;
            var best = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                int order = comparer.Compare(values[i], best);
                if (function == DetailAggregateFunction.Min ? order < 0 : order > 0) { best = values[i]; }
            }
            return best;
        }

        /// <summary>
        /// Reads one detail value for an aggregate, coerced to its field's type and, when a rounding context is
        /// given, rounded to the field's own decimals first: the master total is the sum of the values the rows
        /// show, not of their full precision (round-then-sum).
        /// </summary>
        private static object AggregateValue(DataRow row, FormField field, FormSchema schema,
            RoundingContext? roundingContext)
        {
            var cell = row.Table.Columns.Contains(field.FieldName) ? row[field.FieldName] : null;
            var value = ExpressionPolicy.CoerceValue(cell, field.DbType);
            if (roundingContext is not null && value is decimal number)
                return NumberFormatResolver.RoundByKind(number, field.NumberKind, roundingContext,
                    ResolveRowRefCode(field, schema, row));
            return value;
        }

        // The reference code of a detail value, read from its own row the way `ResolveRefCode` reads it from the
        // row's variables when that row's computed fields are rounded.
        private static string? ResolveRowRefCode(FormField field, FormSchema schema, DataRow row)
        {
            string? codeField = field.NumberKind switch
            {
                NumberKind.Amount => StringUtilities.IsNotEmpty(field.CurrencyField)
                    ? field.CurrencyField : schema.CurrencyField,
                NumberKind.Quantity or NumberKind.Weight => field.UnitField,
                _ => null,
            };
            if (codeField is null || codeField.Length == 0 || !row.Table.Columns.Contains(codeField)) { return null; }
            var value = row[codeField];
            return value is DBNull ? null : value.ToString();
        }

        private static List<DataRow> LiveRows(DataSet? dataSet, string tableName)
        {
            var rows = new List<DataRow>();
            if (dataSet is null || !dataSet.Tables.Contains(tableName)) { return rows; }
            foreach (DataRow row in dataSet.Tables[tableName]!.Rows)
            {
                if (row.RowState is DataRowState.Deleted or DataRowState.Detached) { continue; }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Resolves the table and field an aggregate names, or describes why it cannot.
        /// </summary>
        private static (FormTable? Table, FormField? Field, string? Error) ResolveAggregate(DetailAggregate aggregate,
            FormSchema schema)
        {
            var call = DescribeCall(aggregate);
            if (schema.Tables is null || !schema.Tables.Contains(aggregate.TableName))
                return (null, null, $"{call}: the form has no table '{aggregate.TableName}'.");
            var table = schema.Tables[aggregate.TableName];
            if (IsMasterTable(schema, table))
                return (null, null, $"{call}: '{aggregate.TableName}' is the master table; an aggregate reads a detail table.");
            if (aggregate.Function == DetailAggregateFunction.Count) { return (table, null, null); }

            if (table.Fields is null || !table.Fields.Contains(aggregate.FieldName))
                return (null, null, $"{call}: the table '{table.TableName}' has no field '{aggregate.FieldName}'.");
            var field = table.Fields[aggregate.FieldName];
            bool accepted = aggregate.Function == DetailAggregateFunction.Sum
                ? IsNumeric(field.DbType)
                : IsNumeric(field.DbType) || field.DbType is FieldDbType.Date or FieldDbType.DateTime or FieldDbType.Time;
            if (!accepted)
                return (null, null, $"{call}: the field '{field.FieldName}' is {field.DbType}, which {aggregate.Function} cannot aggregate.");
            return (table, field, null);
        }

        /// <summary>
        /// Gets the type an aggregate's variable has, as <see cref="ComputeAggregate"/> produces it.
        /// </summary>
        private static Type AggregateType(DetailAggregateFunction function, FormField? field)
        {
            return function switch
            {
                DetailAggregateFunction.Count => typeof(int),
                DetailAggregateFunction.Sum => typeof(decimal),
                _ => ExpressionPolicy.CoerceValue(null, field!.DbType).GetType(),
            };
        }

        private static bool IsNumeric(FieldDbType dbType)
        {
            return dbType is FieldDbType.Short or FieldDbType.Integer or FieldDbType.Long
                or FieldDbType.Decimal or FieldDbType.Currency;
        }

        private static bool IsMasterTable(FormSchema schema, FormTable formTable)
        {
            return schema.MasterTable is { } master
                && string.Equals(master.TableName, formTable.TableName, StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeCall(DetailAggregate aggregate)
        {
            return aggregate.FieldName.Length == 0
                ? $"{aggregate.Function}({aggregate.TableName})"
                : $"{aggregate.Function}({aggregate.TableName}.{aggregate.FieldName})";
        }

        private static string AggregateOutsideMasterMessage(FormTable formTable)
        {
            return $"An aggregate such as Sum(Table.field) can only be used by the master table; '{formTable.TableName}' is a detail table.";
        }

        // True when a master table's computed fields aggregate a detail table that has a changed row, so an
        // unchanged master row must be recomputed as well.
        private bool AggregatedDetailsChanged(FormSchema schema, FormTable masterTable, DataSet dataSet)
        {
            if (masterTable.Fields is null) { return false; }
            foreach (var field in masterTable.Fields)
            {
                if (StringUtilities.IsEmpty(field.ValueExpression)) { continue; }
                foreach (var aggregate in ParseAggregates(field.ValueExpression).Aggregates)
                {
                    if (schema.Tables is null || !schema.Tables.Contains(aggregate.TableName)) { continue; }
                    var tableName = schema.Tables[aggregate.TableName].TableName;
                    if (!dataSet.Tables.Contains(tableName)) { continue; }
                    foreach (DataRow row in dataSet.Tables[tableName]!.Rows)
                    {
                        if (row.RowState is DataRowState.Added or DataRowState.Modified or DataRowState.Deleted)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
