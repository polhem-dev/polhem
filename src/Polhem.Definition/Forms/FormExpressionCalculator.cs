using System.Collections.Concurrent;
using System.Data;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Expressions;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Evaluates a form's field expressions and rules against its <see cref="DataSet"/> / <see cref="DataRow"/>
    /// with a shared <see cref="IExpressionEvaluator"/>, rounding computed numeric fields through
    /// <see cref="NumberFormatResolver"/>. Shared by the server (before save/delete) and UI clients
    /// (live preview) so a field computed on the client yields the same result the server writes on
    /// save. Like <see cref="FormRowDefaults"/>, this is schema-driven <see cref="DataRow"/> logic with
    /// no server-only dependency, so both sides delegate here from a single place.
    /// </summary>
    /// <remarks>
    /// The full-set methods (<see cref="ApplyFieldExpressions"/> / <see cref="ValidateRules"/>) drive
    /// the server's before-save/before-delete pass over an entire data set. The row-level methods
    /// (<see cref="ApplyComputedRow"/> / <see cref="ApplyDefaultRow"/>) recompute a single row for live
    /// client preview and report which fields actually changed, and <see cref="BuildDependencyMap"/>
    /// exposes the "which edited field forces which computed field to recompute" graph the client uses
    /// to gate recomputation.
    /// </remarks>
    public sealed partial class FormExpressionCalculator
    {
        private readonly IExpressionEvaluator _evaluator;

        /// <summary>
        /// Cached per expression: the variable names it actually references.
        /// </summary>
        /// <remarks>
        /// Cached because <see cref="IExpressionEvaluator.GetReferencedVariables"/> parses the
        /// expression under a lock — calling it per row would cost more than it saves.
        /// </remarks>
        private readonly ConcurrentDictionary<string, string[]> _referencedVariables = new(StringComparer.Ordinal);

        /// <summary>
        /// Narrows the row's variable map to the names the expression actually references.
        /// </summary>
        /// <remarks>
        /// The evaluator builds one parameter per entry handed to it and the engine binds every one,
        /// so the cost of an evaluation tracks the <b>column count</b>, not the complexity of the
        /// expression. A 30-column table evaluating <c>a + b + c</c> paid for 30 parameters on every
        /// row. Measured on 30 columns / 5 computed fields / 1000 rows: 44.6 ms with the full map
        /// versus 7.1 ms with only the referenced names.
        /// <para>
        /// Falls back to the full map if the names cannot be determined, so a parse failure surfaces
        /// from the evaluation itself exactly as it did before rather than from here.
        /// </para>
        /// </remarks>
        /// <param name="expression">The expression about to be evaluated.</param>
        /// <param name="variables">The row's full variable map.</param>
        private Dictionary<string, object?> NarrowVariables(
            string expression, Dictionary<string, object?> variables)
        {
            string[] names;
            try
            {
                names = _referencedVariables.GetOrAdd(expression,
                    e => [.. _evaluator.GetReferencedVariables(e)]);
            }
            catch (ExpressionEvaluationException)
            {
                return variables;
            }

            var narrowed = new Dictionary<string, object?>(names.Length, StringComparer.Ordinal);
            foreach (var name in names)
            {
                // An unresolved name is left out rather than defaulted: the evaluator must still
                // report it as unknown, which is what the degrade path keys off.
                if (variables.TryGetValue(name, out var value)) { narrowed[name] = value; }
            }
            return narrowed;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FormExpressionCalculator"/>.
        /// </summary>
        /// <param name="evaluator">The expression evaluator (compiles and caches per expression).</param>
        public FormExpressionCalculator(IExpressionEvaluator evaluator)
        {
            ArgumentNullException.ThrowIfNull(evaluator);
            _evaluator = evaluator;
        }

        /// <summary>
        /// Evaluates the default-value expressions of every new (<see cref="DataRowState.Added"/>) row of a
        /// freshly built data set and writes each result, replacing whatever the row was seeded with: the
        /// per-type seed of <see cref="FormRowDefaults"/> and a literal <see cref="FormField.DefaultValue"/>.
        /// This is the server's new-record pass; <see cref="ApplyDefaultRow"/> is its client-side
        /// counterpart for a single row.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The new-record data set (mutated in place).</param>
        /// <param name="timeZoneId">The user's IANA time zone id, seen by the <c>Today()</c> helper; blank means UTC.</param>
        /// <remarks>
        /// <c>Now()</c> is evaluated on the <see cref="DateTimeBasis.Utc"/> basis, because a server-side data
        /// set is in UTC (ADR-032 D3). Computed fields are left to the save pass and to the client's live
        /// computation.
        /// </remarks>
        public void ApplyNewRowDefaults(FormSchema schema, DataSet dataSet, string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(dataSet);

            if (schema.Tables == null) { return; }

            foreach (var formTable in schema.Tables)
            {
                if (formTable.Fields == null) { continue; }
                var dataTable = FindDataTable(dataSet, formTable.TableName);
                if (dataTable == null) { continue; }
                var defaultFields = formTable.Fields
                    .Where(f => StringUtilities.IsNotEmpty(f.DefaultValueExpression)).ToList();
                if (defaultFields.Count == 0) { continue; }

                foreach (DataRow row in dataTable.Rows)
                {
                    if (row.RowState != DataRowState.Added) { continue; }
                    ApplyDefaults(row, formTable, defaultFields, timeZoneId, DateTimeBasis.Utc, overwrite: true);
                }
            }
        }

        /// <summary>
        /// Fills default-value expressions on new rows and recomputes value-expression fields on
        /// new/changed rows, per table. This is the server's before-save field pass.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The data set to apply expressions to.</param>
        /// <param name="roundingContext">The rounding context for computed numeric fields.</param>
        /// <param name="timeZoneId">The user's IANA time zone id, seen by the <c>Today()</c> helper; blank means UTC.</param>
        /// <exception cref="InvalidOperationException">
        /// A computed quantity or weight field has no <see cref="FormField.UnitField"/>, or a computed amount
        /// resolves against a company with no default currency.
        /// </exception>
        /// <remarks>
        /// <c>Now()</c> is evaluated on the <see cref="DateTimeBasis.Utc"/> basis. This pass runs on the
        /// server, where the data set is in UTC (ADR-032 D3), so a user-zone reading would be written off
        /// by the user's offset.
        /// <para>
        /// Unlike <see cref="ApplyNewRowDefaults"/>, this pass fills a default-value field only while it is
        /// empty: by the time a row is saved, a non-empty value may be one the user entered.
        /// </para>
        /// </remarks>
        public void ApplyFieldExpressions(FormSchema schema, DataSet dataSet, RoundingContext roundingContext,
            string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(dataSet);
            ArgumentNullException.ThrowIfNull(roundingContext);

            if (schema.Tables == null) { return; }

            foreach (var formTable in schema.Tables)
            {
                if (formTable.Fields == null) { continue; }
                var dataTable = FindDataTable(dataSet, formTable.TableName);
                if (dataTable == null) { continue; }
                ApplyTableFieldExpressions(formTable, dataTable, schema, roundingContext, timeZoneId, DateTimeBasis.Utc);
            }
        }

        /// <summary>
        /// Applies the default-value and value expressions of one table to its data rows: fills defaults on
        /// added rows and recomputes value-expression fields on added/modified rows.
        /// </summary>
        private void ApplyTableFieldExpressions(FormTable formTable, DataTable dataTable, FormSchema schema,
            RoundingContext roundingContext, string timeZoneId, DateTimeBasis basis)
        {
            var defaultFields = formTable.Fields!
                .Where(f => StringUtilities.IsNotEmpty(f.DefaultValueExpression)).ToList();
            var computedFields = formTable.Fields!
                .Where(f => StringUtilities.IsNotEmpty(f.ValueExpression)).ToList();
            if (defaultFields.Count == 0 && computedFields.Count == 0) { return; }

            foreach (DataRow row in dataTable.Rows)
            {
                var state = row.RowState;
                if (state is DataRowState.Deleted or DataRowState.Detached) { continue; }

                if (state == DataRowState.Added && defaultFields.Count > 0)
                    ApplyDefaults(row, formTable, defaultFields, timeZoneId, basis, overwrite: false);

                if (state is DataRowState.Added or DataRowState.Modified && computedFields.Count > 0)
                    ApplyComputed(row, formTable, schema, computedFields, roundingContext, timeZoneId, basis);
            }
        }

        /// <summary>
        /// Recomputes every value-expression field on a single row (in declaration order so a field may
        /// reference an earlier computed field) and reports the fields whose value actually changed.
        /// Used by clients for live preview; a value equal to the current cell is not rewritten, so no
        /// spurious change is reported.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="formTable">The row's form table.</param>
        /// <param name="row">The row to recompute.</param>
        /// <param name="roundingContext">The rounding context for computed numeric fields.</param>
        /// <returns>The names of the fields whose value changed (empty when nothing changed).</returns>
        /// <param name="timeZoneId">
        /// The user's IANA time zone id, seen by the <c>Today()</c> and <c>Now()</c> helpers; blank means UTC.
        /// <c>Now()</c> uses the user's zone here because a client-side data set is held in it.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// A computed quantity or weight field has no <see cref="FormField.UnitField"/>, or a computed amount
        /// resolves against a company with no default currency.
        /// </exception>
        public IReadOnlyList<string> ApplyComputedRow(FormSchema schema, FormTable formTable, DataRow row,
            RoundingContext roundingContext, string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(roundingContext);

            if (formTable.Fields == null) { return []; }
            var computedFields = formTable.Fields
                .Where(f => StringUtilities.IsNotEmpty(f.ValueExpression)).ToList();
            if (computedFields.Count == 0) { return []; }

            return ApplyComputed(row, formTable, schema, computedFields, roundingContext, timeZoneId, DateTimeBasis.UserZone);
        }

        /// <summary>
        /// Evaluates each default-value expression field on a newly created row and writes the result,
        /// replacing whatever the row was seeded with (the per-type seed of <see cref="FormRowDefaults"/> or a
        /// literal <see cref="FormField.DefaultValue"/>), and reports the fields whose value changed. Used by
        /// clients when a new row is created; <see cref="ApplyNewRowDefaults"/> is the server's counterpart.
        /// </summary>
        /// <remarks>
        /// Call it only on a row the user has not edited yet: it does not check whether a value was entered.
        /// </remarks>
        /// <param name="formTable">The row's form table.</param>
        /// <param name="row">The new row to seed.</param>
        /// <returns>The names of the fields whose value changed (empty when none).</returns>
        /// <param name="timeZoneId">
        /// The user's IANA time zone id, seen by the <c>Today()</c> and <c>Now()</c> helpers; blank means UTC.
        /// <c>Now()</c> uses the user's zone here because a client-side data set is held in it.
        /// </param>
        public IReadOnlyList<string> ApplyDefaultRow(FormTable formTable, DataRow row, string timeZoneId = "")
        {
            ArgumentNullException.ThrowIfNull(formTable);
            ArgumentNullException.ThrowIfNull(row);

            if (formTable.Fields == null) { return []; }
            var defaultFields = formTable.Fields
                .Where(f => StringUtilities.IsNotEmpty(f.DefaultValueExpression)).ToList();
            if (defaultFields.Count == 0) { return []; }

            return ApplyDefaults(row, formTable, defaultFields, timeZoneId, DateTimeBasis.UserZone, overwrite: true);
        }

        /// <summary>
        /// Builds the "edited source field → dependent computed fields" map for a table: for each
        /// value-expression field, the identifiers it references become keys pointing to that computed
        /// field. Clients use it to decide whether an edit forces a recompute. Keys compare
        /// case-insensitively, matching <see cref="DataTable"/> column lookup semantics.
        /// </summary>
        /// <param name="formTable">The form table to analyze.</param>
        /// <returns>A map from source field name to the computed fields that reference it.</returns>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> BuildDependencyMap(FormTable formTable)
        {
            ArgumentNullException.ThrowIfNull(formTable);

            var builder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (formTable.Fields == null)
                return s_emptyDependencyMap;

            foreach (var field in formTable.Fields)
            {
                if (StringUtilities.IsEmpty(field.ValueExpression)) { continue; }
                foreach (var source in _evaluator.GetReferencedVariables(field.ValueExpression))
                {
                    if (!builder.TryGetValue(source, out var dependents))
                    {
                        dependents = [];
                        builder[source] = dependents;
                    }
                    if (!dependents.Contains(field.FieldName, StringComparer.OrdinalIgnoreCase))
                        dependents.Add(field.FieldName);
                }
            }

            var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in builder)
                map[pair.Key] = pair.Value;
            return map;
        }

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> s_emptyDependencyMap =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Writes each default-value field on a new row, returning the names of the fields whose value
        /// changed. With <paramref name="overwrite"/> the expression replaces any seeded value (a row just
        /// created); without it only an empty field is filled (a row about to be saved).
        /// </summary>
        private List<string> ApplyDefaults(DataRow row, FormTable formTable, List<FormField> defaultFields, string timeZoneId,
            DateTimeBasis basis, bool overwrite)
        {
            var changed = new List<string>();
            var variables = BuildVariables(row, formTable);
            foreach (var field in defaultFields)
            {
                if (!row.Table.Columns.Contains(field.FieldName)) { continue; }
                if (!overwrite && !IsEmptyValue(row[field.FieldName])) { continue; }
                // Evaluated without a forced return type, then coerced: the engine's value domain and the
                // DataSet's differ for dates — `Today()` yields a DateOnly while the cell holds a
                // DateTime (ADR-032 D12, ADR-031) — and forcing the cell's type at parse time would
                // make the engine reject its own helper. `CoerceValue` performs every widening the
                // return type used to, plus that one.
                var value = _evaluator.Evaluate<object?>(field.DefaultValueExpression,
                    NarrowVariables(field.DefaultValueExpression, variables), timeZoneId, basis);
                var newValue = value is null ? (object)DBNull.Value : ExpressionPolicy.CoerceValue(value, field.DbType);
                if (Equals(newValue, row[field.FieldName])) { continue; }
                row[field.FieldName] = newValue;
                changed.Add(field.FieldName);
            }
            return changed;
        }

        /// <summary>
        /// Recomputes each value-expression field on a row. Numeric results are rounded through the
        /// number subsystem; the local variable snapshot is updated after each field so later
        /// expressions in the same row observe earlier computed values (dependency chains authored in
        /// declaration order). A result equal to the current cell is not rewritten, so live-preview
        /// callers see no spurious change.
        /// </summary>
        private List<string> ApplyComputed(DataRow row, FormTable formTable, FormSchema schema,
            List<FormField> computedFields, RoundingContext roundingContext, string timeZoneId, DateTimeBasis basis)
        {
            var changed = new List<string>();
            var variables = BuildVariables(row, formTable);
            foreach (var field in computedFields)
            {
                if (!row.Table.Columns.Contains(field.FieldName)) { continue; }

                // Coerced after evaluation rather than forced at parse time — see ApplyDefaults.
                var result = ExpressionPolicy.CoerceValue(
                    _evaluator.Evaluate<object?>(field.ValueExpression,
                        NarrowVariables(field.ValueExpression, variables), timeZoneId, basis), field.DbType);

                if (result is decimal numeric)
                {
                    EnsureUnitBound(field, formTable);
                    result = NumberFormatResolver.RoundByKind(
                        numeric, field.NumberKind, roundingContext, ResolveRefCode(field, schema, variables));
                }

                var newValue = result ?? (object)DBNull.Value;
                if (!Equals(newValue, row[field.FieldName]))
                {
                    row[field.FieldName] = newValue;
                    changed.Add(field.FieldName);
                }
                variables[field.FieldName] = result;
            }
            return changed;
        }

        /// <summary>
        /// Throws when a quantity or weight field has no <see cref="FormField.UnitField"/>. Its decimals
        /// come only from its unit, so rounding it without one would apply a decimal count that no unit
        /// chose. A value that needs no unit should not carry either kind.
        /// </summary>
        private static void EnsureUnitBound(FormField field, FormTable formTable)
        {
            if (NumberKindProfile.GetDecimalsSource(field.NumberKind) != DecimalsSource.Unit) { return; }
            if (StringUtilities.IsNotEmpty(field.UnitField)) { return; }

            throw new InvalidOperationException(
                $"Field '{formTable.TableName}.{field.FieldName}' is a {field.NumberKind} field but has no UnitField.");
        }

        /// <summary>
        /// Builds the expression variable map for a row, coercing each column to a non-null value of its
        /// field's CLR type. Variables are keyed by the schema field's declared name (its casing), not by
        /// the <see cref="DataColumn"/> name: expressions reference fields by their declared name and the
        /// engine's identifiers are case-sensitive, so keying by the column name would bind the expression
        /// to whatever casing the in-memory DataSet happens to store. That casing has changed once
        /// already — <c>AddColumn</c> stored column names uppercased before ADR-029
        /// and lowercases them now — and keying by the column name left <c>quantity</c> unresolved against
        /// a <c>QUANTITY</c> column. Columns with no schema field fall back to their column name (nothing
        /// references them).
        /// </summary>
        private static Dictionary<string, object?> BuildVariables(DataRow row, FormTable formTable)
        {
            var variables = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DataColumn column in row.Table.Columns)
            {
                var field = ResolveField(formTable, column.ColumnName);
                var name = field?.FieldName ?? column.ColumnName;
                var dbType = field?.DbType ?? DbTypeConverter.ToFieldDbType(column.DataType);
                variables[name] = ExpressionPolicy.CoerceValue(row[column], dbType);
            }
            return variables;
        }

        /// <summary>
        /// Resolves the schema field for a column by name (case-insensitive, matching
        /// <see cref="DataColumnCollection"/> lookup), or null when the table has no such field.
        /// </summary>
        private static FormField? ResolveField(FormTable formTable, string columnName)
        {
            return formTable.Fields != null && formTable.Fields.Contains(columnName)
                ? formTable.Fields[columnName] : null;
        }

        /// <summary>
        /// Resolves the reference code (currency for amounts, unit for quantities/weights) used to pick
        /// decimal places when rounding a computed numeric field; null for other kinds.
        /// </summary>
        private static string? ResolveRefCode(FormField field, FormSchema schema,
            Dictionary<string, object?> variables)
        {
            string? codeField = field.NumberKind switch
            {
                NumberKind.Amount => StringUtilities.IsNotEmpty(field.CurrencyField)
                    ? field.CurrencyField : schema.CurrencyField,
                NumberKind.Quantity or NumberKind.Weight => field.UnitField,
                _ => null,
            };
            if (StringUtilities.IsEmpty(codeField)) { return null; }
            return variables.TryGetValue(codeField!, out var value) ? value?.ToString() : null;
        }

        /// <summary>
        /// Returns the data table with the given name, or null when the data set has no such table.
        /// </summary>
        private static DataTable? FindDataTable(DataSet dataSet, string tableName)
        {
            return dataSet.Tables.Contains(tableName) ? dataSet.Tables[tableName] : null;
        }

        /// <summary>
        /// Returns true when a value is null, <see cref="DBNull"/>, or an empty string.
        /// </summary>
        private static bool IsEmptyValue(object? value)
        {
            if (value is null || value is DBNull) { return true; }
            return value is string s && s.Length == 0;
        }
    }
}
