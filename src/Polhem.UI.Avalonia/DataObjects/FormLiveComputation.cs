using System.Data;
using Polhem.Core.Expressions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Expressions;
using Polhem.UI.Core;

namespace Polhem.UI.Avalonia.DataObjects
{
    /// <summary>
    /// Client-side live recomputation of a form's computed fields (<see cref="FormField.ValueExpression"/>)
    /// and new-row default expressions (<see cref="FormField.DefaultValueExpression"/>) for this head. The
    /// recompute rules are <see cref="FormLiveComputationEngine"/>, which every head shares; this type supplies
    /// this head's defaults: a <see cref="DynamicExpressoEvaluator"/> shared by every form in the process, and
    /// the signed-in user's time zone from <see cref="UIServices"/>. The wiring that subscribes
    /// <see cref="FormDataObject.FieldValueChanged"/> and refreshes detail grids lives in
    /// <see cref="Polhem.UI.Avalonia.Views.FormView"/>.
    /// </summary>
    public sealed class FormLiveComputation
    {
        // The evaluator is built to be shared (the server registers one as a singleton): its compile
        // cache is concurrent and a helper's per-call time zone travels in thread-static state.
        private static readonly Lazy<DynamicExpressoEvaluator> s_sharedEvaluator = new(() => new DynamicExpressoEvaluator());

        private readonly FormLiveComputationEngine _engine;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLiveComputation"/>.
        /// </summary>
        /// <param name="schema">The form schema whose expressions drive recomputation.</param>
        /// <param name="roundingContext">
        /// The rounding context for computed numeric fields; <c>null</c> uses framework-default decimal
        /// places (Tier 1).
        /// </param>
        /// <param name="evaluator">
        /// The expression evaluator; <c>null</c> uses one <see cref="DynamicExpressoEvaluator"/> shared by every
        /// form in the process, so an expression compiled when one form opens is not compiled again when the
        /// next one does.
        /// </param>
        public FormLiveComputation(FormSchema schema, RoundingContext? roundingContext = null,
            IExpressionEvaluator? evaluator = null)
        {
            _engine = new FormLiveComputationEngine(
                schema,
                evaluator ?? s_sharedEvaluator.Value,
                static () => UIServices.Get<IUserContext>()?.User?.TimeZone ?? string.Empty,
                roundingContext);
        }

        /// <inheritdoc cref="FormLiveComputationEngine.IsRecomputing"/>
        public bool IsRecomputing => _engine.IsRecomputing;

        /// <inheritdoc cref="FormLiveComputationEngine.IsDegraded"/>
        public bool IsDegraded => _engine.IsDegraded;

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
