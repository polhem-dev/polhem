using System.Data;
using Polhem.Core;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Definition.Numbers;

namespace Polhem.UI.Core
{
    /// <summary>
    /// Client-side live recomputation of a form's computed fields (<see cref="FormField.ValueExpression"/>)
    /// and new-row default expressions (<see cref="FormField.DefaultValueExpression"/>), delegating to the
    /// shared <see cref="FormExpressionCalculator"/> so a field previewed on the client matches what the
    /// server writes on save. Every UI head drives this same engine; each wires it to its own data object's
    /// change events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A recompute writes its results straight into the bound <see cref="DataRow"/>, which re-raises the data
    /// object's change event. The <see cref="IsRecomputing"/> flag lets the wiring ignore those
    /// self-inflicted echoes, and a computed field is never itself a recompute trigger, so a single edit
    /// produces exactly one recompute pass.
    /// </para>
    /// <para>
    /// The engine takes the expression evaluator and the user's time zone from its caller, so this package
    /// stays free of an expression engine and of process-wide state. Rounding uses the given
    /// <see cref="RoundingContext"/>; an empty one falls back to the per-<see cref="NumberKind"/> default
    /// decimal places, so a preview differs from the saved value only where a company overrides them, and
    /// the server corrects it on save.
    /// </para>
    /// </remarks>
    internal sealed class FormLiveComputationEngine
    {
        private readonly FormSchema _schema;
        private readonly Func<string> _timeZoneProvider;
        private readonly FormExpressionCalculator _calculator;
        private readonly RoundingContext _roundingContext;
        // Dependency maps are built lazily per table (parse-once) and reused for every edit.
        private readonly Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> _dependencyByTable
            = new(StringComparer.OrdinalIgnoreCase);
        // Built lazily, once: which detail tables and fields the master's aggregates read.
        private IReadOnlyDictionary<string, IReadOnlySet<string>>? _aggregateMap;
        private bool _recomputing;
        private LiveComputationDegradation? _degradation;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLiveComputationEngine"/>.
        /// </summary>
        /// <param name="schema">The form schema whose expressions drive recomputation.</param>
        /// <param name="evaluator">The expression evaluator.</param>
        /// <param name="timeZoneProvider">
        /// Returns the signed-in user's IANA time zone id, or blank (meaning UTC) before sign-in. It is called
        /// on every evaluation rather than once, because the engine can outlive a sign-in, and a stale zone
        /// would silently date a new row by the previous user's day (ADR-032 D13).
        /// </param>
        /// <param name="roundingContext">
        /// The rounding context for computed numeric fields; <c>null</c> uses framework-default decimal places.
        /// </param>
        public FormLiveComputationEngine(FormSchema schema, IExpressionEvaluator evaluator,
            Func<string> timeZoneProvider, RoundingContext? roundingContext = null)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(evaluator);
            ArgumentNullException.ThrowIfNull(timeZoneProvider);
            _schema = schema;
            _timeZoneProvider = timeZoneProvider;
            _calculator = new FormExpressionCalculator(evaluator);
            _roundingContext = roundingContext ?? new RoundingContext();
        }

        /// <summary>
        /// Gets a value indicating whether a recompute pass is currently writing back computed fields.
        /// Change-event subscribers check this to ignore the writes a recompute produces (re-entrancy guard).
        /// </summary>
        public bool IsRecomputing => _recomputing;

        /// <summary>
        /// Gets a value indicating whether live computation has degraded to a no-op after an evaluation
        /// failure. A definition-authored expression that fails to parse or evaluate is deterministic, so
        /// rather than throw on every keystroke the service disables client-side preview for the session
        /// and lets the server compute the fields authoritatively on save.
        /// </summary>
        public bool IsDegraded => _degradation is not null;

        /// <summary>
        /// Gets what made live computation degrade, or <c>null</c> while it still runs.
        /// </summary>
        public LiveComputationDegradation? Degradation => _degradation;

        /// <summary>
        /// Occurs once, when live computation degrades; <see cref="Degradation"/> holds the cause.
        /// </summary>
        /// <remarks>
        /// Raised on the thread of the call that failed, after the failing pass has ended, so a handler may
        /// show a message or log without the form's change events firing underneath it. A head should tell
        /// the user, because the computed fields stop following their sources until the record is saved.
        /// </remarks>
        public event EventHandler? Degraded;

        /// <summary>
        /// Recomputes the computed fields of <paramref name="row"/> when <paramref name="changedField"/> is a
        /// source that some computed field depends on, returning the fields whose value changed. A no-op
        /// (returns empty) when a recompute is already in progress, when the changed field is itself a
        /// computed field, or when nothing depends on it.
        /// </summary>
        /// <remarks>
        /// When the master adds up the edited field, or a field this recompute changed, with an aggregate such as
        /// <c>Sum(Table.field)</c>, the master row is recomputed too. Its changes are written to the master row (and
        /// raised by the data set's change events) but are not in the returned list, which covers
        /// <paramref name="row"/> only.
        /// </remarks>
        /// <param name="tableName">The name of the table the row belongs to (master or detail).</param>
        /// <param name="changedField">The field the user (or a write-back) just changed.</param>
        /// <param name="row">The row to recompute.</param>
        /// <returns>The names of the computed fields whose value changed.</returns>
        public IReadOnlyList<string> Recompute(string tableName, string changedField, DataRow row)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentException.ThrowIfNullOrWhiteSpace(changedField);
            ArgumentNullException.ThrowIfNull(row);

            // A recompute's own write-backs re-raise the change event; do not recurse. Once degraded,
            // preview stays off for the session (the server computes on save).
            if (_recomputing || IsDegraded) { return []; }

            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            // A computed field changing is the result of a recompute, never a trigger for one on its own row. A
            // master total over it still follows, as when a field-change callback writes it.
            if (IsComputedField(formTable, changedField))
            {
                if (AggregatesRead(formTable, changedField, [])) { RecomputeMaster(row.Table.DataSet); }
                return [];
            }

            var changed = RunGuarded(formTable, () =>
            {
                // Skip the whole pass when no computed field references the changed field.
                if (!GetDependencyMap(formTable).ContainsKey(changedField)) { return []; }
                return _calculator.ApplyComputedRow(_schema, formTable, row, _roundingContext, _timeZoneProvider());
            });

            // A master total over this table follows the edit, and the values the edit recomputed.
            if (AggregatesRead(formTable, changedField, changed))
                RecomputeMaster(row.Table.DataSet);
            return changed;
        }

        /// <summary>
        /// Evaluates the default-value expressions of a freshly created <paramref name="row"/>, replacing the
        /// per-type seed and any literal default the row carries, and returns the fields whose value changed.
        /// Matches what the server's <c>GetNewData</c> does for a new master row.
        /// </summary>
        /// <param name="tableName">The name of the table the row belongs to (master or detail).</param>
        /// <param name="row">The new row to seed; call this before the user edits it.</param>
        /// <returns>The names of the fields whose value changed.</returns>
        public IReadOnlyList<string> ApplyDefaults(string tableName, DataRow row)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentNullException.ThrowIfNull(row);

            if (IsDegraded) { return []; }
            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            return RunGuarded(formTable, () => _calculator.ApplyDefaultRow(formTable, row, _timeZoneProvider()));
        }

        /// <summary>
        /// Prepares a freshly created <paramref name="row"/> for display: evaluates its default-value
        /// expressions (replacing the seeded values), then recomputes all computed fields so any default feeding a
        /// computed field is reflected at once. Returns the fields whose value changed. The whole pass runs
        /// under the re-entrancy guard, so the write-backs raise no nested recompute. When the master adds up the
        /// row's table, the master row is recomputed as well; its changes are not in the returned list.
        /// </summary>
        /// <param name="tableName">The name of the table the row belongs to (master or detail).</param>
        /// <param name="row">The new row to initialize.</param>
        /// <returns>The names of the fields that were filled or computed.</returns>
        public IReadOnlyList<string> InitializeNewRow(string tableName, DataRow row)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentNullException.ThrowIfNull(row);

            if (IsDegraded) { return []; }
            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            var result = RunGuarded(formTable, () =>
            {
                var changed = new List<string>();
                changed.AddRange(_calculator.ApplyDefaultRow(formTable, row, _timeZoneProvider()));
                foreach (var field in _calculator.ApplyComputedRow(_schema, formTable, row, _roundingContext, _timeZoneProvider())
                             .Where(field => !changed.Contains(field, StringComparer.OrdinalIgnoreCase)))
                {
                    changed.Add(field);
                }
                return changed;
            });
            // A new row counts towards the master's totals at once.
            if (IsAggregated(formTable))
                RecomputeMaster(row.Table.DataSet);
            return result;
        }

        /// <summary>
        /// Recomputes the master row after rows of a detail table were removed or put back: a row deleted, or a row
        /// edit cancelled. Edits and added rows need no call; <see cref="Recompute"/> and
        /// <see cref="InitializeNewRow"/> already recompute the master when the table is aggregated.
        /// </summary>
        /// <param name="tableName">The detail table whose rows changed.</param>
        /// <param name="dataSet">The form's data set.</param>
        /// <returns>
        /// The master fields whose value changed; empty when the master adds up nothing from this table, when the
        /// computation is degraded, or during another recompute.
        /// </returns>
        public IReadOnlyList<string> RecomputeAggregates(string tableName, DataSet dataSet)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentNullException.ThrowIfNull(dataSet);

            if (_recomputing || IsDegraded) { return []; }
            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable is null || !IsAggregated(formTable)) { return []; }
            return RecomputeMaster(dataSet);
        }

        // Recomputes the computed fields of the master row, aggregates included, in a guard of its own so a
        // failure is reported against the master table.
        private IReadOnlyList<string> RecomputeMaster(DataSet? dataSet)
        {
            if (dataSet is null || IsDegraded) { return []; }
            var masterTable = _schema.MasterTable;
            if (masterTable?.Fields is null || !dataSet.Tables.Contains(masterTable.TableName)) { return []; }
            var master = dataSet.Tables[masterTable.TableName]!.Rows.Cast<DataRow>()
                .FirstOrDefault(r => r.RowState is not (DataRowState.Deleted or DataRowState.Detached));
            if (master is null) { return []; }
            return RunGuarded(masterTable,
                () => _calculator.ApplyComputedRow(_schema, masterTable, master, _roundingContext, _timeZoneProvider()));
        }

        private IReadOnlyDictionary<string, IReadOnlySet<string>> AggregateMap
            => _aggregateMap ??= FormExpressionCalculator.BuildAggregateDependencyMap(_schema);

        private bool IsAggregated(FormTable formTable) => AggregateMap.ContainsKey(formTable.TableName);

        // True when the master adds up a field of this table that the edit changed, directly or through the row's
        // own recompute.
        private bool AggregatesRead(FormTable formTable, string changedField, IReadOnlyList<string> recomputed)
        {
            if (IsDegraded || !AggregateMap.TryGetValue(formTable.TableName, out var fields)) { return false; }
            return fields.Contains(changedField) || recomputed.Any(fields.Contains);
        }

        /// <summary>
        /// Runs a recompute pass under the re-entrancy guard, degrading to a no-op on a deterministic
        /// failure. A definition-authored expression that fails to parse/evaluate
        /// (<see cref="ExpressionEvaluationException"/>) or a cell value that cannot be coerced to its
        /// field type (<see cref="FormatException"/> / <see cref="InvalidCastException"/> /
        /// <see cref="OverflowException"/> — for example a malformed GUID key stored as text) would
        /// otherwise surface inside a data object's change handler and break the form, so it disables
        /// preview for the session, records the cause in <see cref="Degradation"/>, and lets the server
        /// compute the fields authoritatively on save.
        /// </summary>
        private IReadOnlyList<string> RunGuarded(FormTable formTable, Func<IReadOnlyList<string>> compute)
        {
            LiveComputationDegradation? degradation = null;
            IReadOnlyList<string> changed;
            _recomputing = true;
            try
            {
                changed = compute();
            }
            catch (Exception ex) when (ex is ExpressionEvaluationException or FormatException
                                           or InvalidCastException or OverflowException)
            {
                degradation = LiveComputationDegradation.From(formTable, ex);
                changed = [];
            }
            finally
            {
                _recomputing = false;
            }

            // Raised outside the guard so a handler that touches the form sees a settled engine.
            if (degradation is not null) { Degrade(degradation); }
            return changed;
        }

        private IReadOnlyDictionary<string, IReadOnlyList<string>> GetDependencyMap(FormTable formTable)
        {
            if (!_dependencyByTable.TryGetValue(formTable.TableName, out var map))
            {
                map = _calculator.BuildDependencyMap(formTable);
                _dependencyByTable[formTable.TableName] = map;
            }
            return map;
        }

        // Latches live computation off after a deterministic evaluation failure (a bad definition-authored
        // expression). The form stays usable and the server computes the affected fields on save.
        private void Degrade(LiveComputationDegradation degradation)
        {
            if (_degradation is not null) { return; }
            _degradation = degradation;
            Degraded?.Invoke(this, EventArgs.Empty);
        }

        private static bool IsComputedField(FormTable formTable, string fieldName)
        {
            if (formTable.Fields is null || !formTable.Fields.Contains(fieldName)) { return false; }
            return StringUtilities.IsNotEmpty(formTable.Fields[fieldName].ValueExpression);
        }
    }
}
