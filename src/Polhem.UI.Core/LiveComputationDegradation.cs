using System.Globalization;
using Microsoft.Extensions.Localization;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.UI.Core
{
    /// <summary>
    /// What made <see cref="FormLiveComputationEngine"/> stop computing: the field whose expression failed,
    /// when it can be told, and the failure itself.
    /// </summary>
    /// <remarks>
    /// <see cref="FormatMessage"/> is the text for the user and names only the field and the reason. The
    /// expression text and the exception are for a log or a developer: <see cref="Expression"/> and
    /// <see cref="Exception"/>.
    /// </remarks>
    public sealed class LiveComputationDegradation
    {
        /// <summary>
        /// Initializes a new <see cref="LiveComputationDegradation"/>.
        /// </summary>
        /// <param name="table">The schema table whose row was being computed.</param>
        /// <param name="field">
        /// The field whose expression failed; <c>null</c> when the failure cannot be traced to one, such as a
        /// cell value that cannot be converted to its field type.
        /// </param>
        /// <param name="exception">The failure that stopped the computation.</param>
        public LiveComputationDegradation(FormTable table, FormField? field, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(exception);
            Table = table;
            Field = field;
            Exception = exception;
        }

        /// <summary>Gets the schema table whose row was being computed.</summary>
        public FormTable Table { get; }

        /// <summary>Gets the field whose expression failed, or <c>null</c> when it cannot be told.</summary>
        public FormField? Field { get; }

        /// <summary>Gets the failure that stopped the computation.</summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the expression that failed, or <c>null</c> when the failure did not come from parsing one.
        /// </summary>
        public string? Expression => (Exception as ExpressionEvaluationException)?.Expression;

        /// <summary>
        /// Gets the short reason: for an expression, the innermost message of the parse failure, which states the
        /// cause (for example that an operator is not defined for two operand types); otherwise the failure's message.
        /// </summary>
        /// <remarks>
        /// The evaluator's own message and the parser's wrapper above it only say that parsing failed and where.
        /// </remarks>
        public string Reason
        {
            get
            {
                if (Exception is not ExpressionEvaluationException) { return Exception.Message; }
                var innermost = Exception;
                while (innermost.InnerException is { } inner) { innermost = inner; }
                return innermost.Message;
            }
        }

        /// <summary>
        /// Gets the text that names the failing field to a user: its caption, or its field name when it has
        /// none; the table's display name when no field can be told.
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (Field is not null)
                    return string.IsNullOrWhiteSpace(Field.Caption) ? Field.FieldName : Field.Caption;
                return string.IsNullOrWhiteSpace(Table.DisplayName) ? Table.TableName : Table.DisplayName;
            }
        }

        /// <summary>
        /// Builds the message that tells the user live computation stopped, in the culture of
        /// <paramref name="localizer"/>.
        /// </summary>
        /// <param name="localizer">The localizer of <see cref="PolhemUIText"/> the head reads its own text from.</param>
        /// <returns>The <see cref="PolhemUIText.LiveComputationStopped"/> text with <see cref="DisplayText"/> and <see cref="Reason"/>.</returns>
        public string FormatMessage(IStringLocalizer localizer)
        {
            ArgumentNullException.ThrowIfNull(localizer);

            string template = PolhemUIText.Get(localizer, PolhemUIText.LiveComputationStopped);
            return string.Format(CultureInfo.CurrentCulture, template, DisplayText, Reason);
        }

        /// <summary>
        /// Traces <paramref name="exception"/> to the field of <paramref name="table"/> whose value or
        /// default-value expression it reports.
        /// </summary>
        internal static LiveComputationDegradation From(FormTable table, Exception exception)
        {
            var expression = (exception as ExpressionEvaluationException)?.Expression;
            var field = string.IsNullOrEmpty(expression) || table.Fields is null
                ? null
                : table.Fields.FirstOrDefault(f =>
                    string.Equals(f.ValueExpression, expression, StringComparison.Ordinal)
                    || string.Equals(f.DefaultValueExpression, expression, StringComparison.Ordinal));
            return new LiveComputationDegradation(table, field, exception);
        }
    }
}
