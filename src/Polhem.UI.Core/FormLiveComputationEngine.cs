using System.Data;
using Polhem.Core;
using Polhem.Core.Expressions;
using Polhem.Definition;
using Polhem.Definition.Forms;

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
    public sealed class FormLiveComputationEngine
    {
        private readonly FormSchema _schema;
        private readonly Func<string> _timeZoneProvider;
        private readonly FormExpressionCalculator _calculator;
        private readonly RoundingContext _roundingContext;
        // Dependency maps are built lazily per table (parse-once) and reused for every edit.
        private readonly Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> _dependencyByTable
            = new(StringComparer.OrdinalIgnoreCase);
        private bool _recomputing;
        private bool _degraded;

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
        public bool IsDegraded => _degraded;

        /// <summary>
        /// Recomputes the computed fields of <paramref name="row"/> when <paramref name="changedField"/> is a
        /// source that some computed field depends on, returning the fields whose value changed. A no-op
        /// (returns empty) when a recompute is already in progress, when the changed field is itself a
        /// computed field, or when nothing depends on it.
        /// </summary>
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
            if (_recomputing || _degraded) { return []; }

            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            // A computed field changing is the result of a recompute, never a trigger for one.
            if (IsComputedField(formTable, changedField)) { return []; }

            return RunGuarded(() =>
            {
                // Skip the whole pass when no computed field references the changed field.
                if (!GetDependencyMap(formTable).ContainsKey(changedField)) { return []; }
                return _calculator.ApplyComputedRow(_schema, formTable, row, _roundingContext, _timeZoneProvider());
            });
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

            if (_degraded) { return []; }
            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            return RunGuarded(() => _calculator.ApplyDefaultRow(formTable, row, _timeZoneProvider()));
        }

        /// <summary>
        /// Prepares a freshly created <paramref name="row"/> for display: evaluates its default-value
        /// expressions (replacing the seeded values), then recomputes all computed fields so any default feeding a
        /// computed field is reflected at once. Returns the fields whose value changed. The whole pass runs
        /// under the re-entrancy guard, so the write-backs raise no nested recompute.
        /// </summary>
        /// <param name="tableName">The name of the table the row belongs to (master or detail).</param>
        /// <param name="row">The new row to initialize.</param>
        /// <returns>The names of the fields that were filled or computed.</returns>
        public IReadOnlyList<string> InitializeNewRow(string tableName, DataRow row)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentNullException.ThrowIfNull(row);

            if (_degraded) { return []; }
            var formTable = _schema.Tables?.GetOrDefault(tableName);
            if (formTable?.Fields is null) { return []; }

            return RunGuarded(() =>
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
        }

        /// <summary>
        /// Runs a recompute pass under the re-entrancy guard, degrading to a no-op on a deterministic
        /// failure. A definition-authored expression that fails to parse/evaluate
        /// (<see cref="ExpressionEvaluationException"/>) or a cell value that cannot be coerced to its
        /// field type (<see cref="FormatException"/> / <see cref="InvalidCastException"/> /
        /// <see cref="OverflowException"/> — for example a malformed GUID key stored as text) would
        /// otherwise surface inside a data object's change handler and break the form, so it disables
        /// preview for the session and lets the server compute the fields authoritatively on save.
        /// </summary>
        private IReadOnlyList<string> RunGuarded(Func<IReadOnlyList<string>> compute)
        {
            _recomputing = true;
            try
            {
                return compute();
            }
            catch (ExpressionEvaluationException)
            {
                Degrade();
                return [];
            }
            catch (FormatException)
            {
                Degrade();
                return [];
            }
            catch (InvalidCastException)
            {
                Degrade();
                return [];
            }
            catch (OverflowException)
            {
                Degrade();
                return [];
            }
            finally
            {
                _recomputing = false;
            }
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
        private void Degrade() => _degraded = true;

        private static bool IsComputedField(FormTable formTable, string fieldName)
        {
            if (formTable.Fields is null || !formTable.Fields.Contains(fieldName)) { return false; }
            return StringUtilities.IsNotEmpty(formTable.Fields[fieldName].ValueExpression);
        }
    }
}
