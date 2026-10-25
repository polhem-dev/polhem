using System.Data;
using Polhem.Core.Expressions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Expressions;
using Polhem.UI.Core;

namespace Polhem.Web.Blazor.Server.DataObjects
{
    /// <summary>
    /// Client-side live recomputation of a form's computed fields (<see cref="FormField.ValueExpression"/>)
    /// and new-row default expressions (<see cref="FormField.DefaultValueExpression"/>) for this head, the
    /// counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.DataObjects.FormLiveComputation</c>. The recompute rules are
    /// <see cref="FormLiveComputationEngine"/>, which both heads share; this type supplies this head's
    /// defaults. <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/> wires it to its data object.
    /// </summary>
    /// <remarks>
    /// The default evaluator is one <see cref="DynamicExpressoEvaluator"/> shared by every circuit: its
    /// compile cache is concurrent, and a helper's per-call time zone travels in thread-static state. The
    /// time zone is not taken from process-wide state, because this head serves several users from one
    /// process: pass the circuit's own, for example the signed-in client's
    /// <see cref="Polhem.Api.Client.ApiSessionCredentials.UserTimeZoneId"/>.
    /// </remarks>
    public sealed class FormLiveComputation
    {
        private static readonly Lazy<DynamicExpressoEvaluator> s_sharedEvaluator = new(() => new DynamicExpressoEvaluator());

        private readonly FormLiveComputationEngine _engine;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLiveComputation"/>.
        /// </summary>
        /// <param name="schema">The form schema whose expressions drive recomputation.</param>
        /// <param name="roundingContext">
        /// The rounding context for computed numeric fields; <c>null</c> uses framework-default decimal places.
        /// </param>
        /// <param name="evaluator">The expression evaluator; <c>null</c> uses the shared default.</param>
        /// <param name="timeZoneProvider">
        /// Returns the signed-in user's IANA time zone id; <c>null</c>, or a blank result, means UTC. Called on
        /// every evaluation, so a sign-in after construction takes effect.
        /// </param>
        public FormLiveComputation(FormSchema schema, RoundingContext? roundingContext = null,
            IExpressionEvaluator? evaluator = null, Func<string>? timeZoneProvider = null)
        {
            _engine = new FormLiveComputationEngine(
                schema,
                evaluator ?? s_sharedEvaluator.Value,
                timeZoneProvider ?? (static () => string.Empty),
                roundingContext);
        }

        /// <inheritdoc cref="FormLiveComputationEngine.IsRecomputing"/>
        public bool IsRecomputing => _engine.IsRecomputing;

        /// <inheritdoc cref="FormLiveComputationEngine.IsDegraded"/>
        public bool IsDegraded => _engine.IsDegraded;

        /// <inheritdoc cref="FormLiveComputationEngine.Degradation"/>
        public LiveComputationDegradation? Degradation => _engine.Degradation;

        /// <inheritdoc cref="FormLiveComputationEngine.Degraded"/>
        public event EventHandler? Degraded
        {
            add => _engine.Degraded += value;
            remove => _engine.Degraded -= value;
        }

        /// <inheritdoc cref="FormLiveComputationEngine.Recompute"/>
        public IReadOnlyList<string> Recompute(string tableName, string changedField, DataRow row)
            => _engine.Recompute(tableName, changedField, row);

        /// <inheritdoc cref="FormLiveComputationEngine.ApplyDefaults"/>
        public IReadOnlyList<string> ApplyDefaults(string tableName, DataRow row)
            => _engine.ApplyDefaults(tableName, row);

        /// <inheritdoc cref="FormLiveComputationEngine.InitializeNewRow"/>
        public IReadOnlyList<string> InitializeNewRow(string tableName, DataRow row)
            => _engine.InitializeNewRow(tableName, row);
    }
}
