using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The state and rules of the query pane, which every UI package renders with its own markup: condition rows the user
    /// adds one at a time, each a field, an operator and a value editor chosen by the field's kind. It edits
    /// <see cref="Criteria"/> and asks the host to run the query through <see cref="SearchRequested"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Search, and Enter in a value editor, ask only when every required row has a value; otherwise the pane names the
    /// empty ones. Escape and the close button ask the host to close the pane through <see cref="CloseRequested"/>.
    /// Clear removes every row that is not required, empties the required ones and searches.
    /// </para>
    /// <para>
    /// Values are entered as the text of native inputs: numbers in the invariant form an <c>input type="number"</c>
    /// reports, days as <c>yyyy-MM-dd</c>. A date range also offers periods named relative to <see cref="Today"/>; the
    /// row keeps the period, and editing a day afterwards turns it back into fixed days.
    /// </para>
    /// <para>
    /// <c>QueryPaneBehaviorTests</c> (tests/Polhem.Web.Blazor.BehaviorTests) checks these rules; each UI package runs
    /// it over its own pane. The Avalonia <c>CriteriaPanel</c> follows the same rules.
    /// </para>
    /// </remarks>
    public abstract partial class CriteriaPanelBase : ComponentBase
    {

        private static readonly RelativeDate[] s_periods =
        [
            new(RelativeDateKind.Today),
            new(RelativeDateKind.Yesterday),
            new(RelativeDateKind.ThisWeek),
            new(RelativeDateKind.LastWeek),
            new(RelativeDateKind.ThisMonth),
            new(RelativeDateKind.LastMonth),
            new(RelativeDateKind.ThisQuarter),
            new(RelativeDateKind.ThisYear),
            new(RelativeDateKind.LastDays, 7),
            new(RelativeDateKind.LastDays, 30),
        ];

        private string? _message;

        /// <summary>Gets or sets the fields a condition can be added for, in the order offered.</summary>
        [Parameter, EditorRequired]
        public IReadOnlyList<CriteriaField> Fields { get; set; } = [];

        /// <summary>Gets or sets the criteria the pane edits.</summary>
        [Parameter, EditorRequired]
        public QueryCriteria Criteria { get; set; } = default!;

        /// <summary>
        /// Gets or sets how today is read for the date periods; <c>null</c>, the default, uses the server's date.
        /// </summary>
        [Parameter]
        public Func<DateOnly>? Today { get; set; }

        /// <summary>Invoked when the user asks to run the query and every required row has a value.</summary>
        [Parameter]
        public EventCallback SearchRequested { get; set; }

        /// <summary>Invoked when the user closes the pane, with its close button or Escape.</summary>
        [Parameter]
        public EventCallback CloseRequested { get; set; }

        /// <summary>
        /// Gets or sets content shown under the heading, above the rows, such as the saved queries a host offers;
        /// <c>null</c>, the default, shows nothing there.
        /// </summary>
        [Parameter]
        public RenderFragment? HeaderContent { get; set; }

        /// <summary>
        /// Gets or sets a note from the host shown above the rows, such as conditions a saved query could not apply;
        /// a message of the pane's own takes its place.
        /// </summary>
        [Parameter]
        public string? Notice { get; set; }

        /// <summary>Invoked when a row is added or removed, or what a row holds changes.</summary>
        [Parameter]
        public EventCallback CriteriaChanged { get; set; }

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets the periods a date range offers.</summary>
        protected static IReadOnlyList<RelativeDate> Periods => s_periods;

        /// <summary>Gets the message above the rows, such as the required rows left empty, or <c>null</c>.</summary>
        protected string? Message => _message ?? Notice;

        /// <summary>Gets the relation row a lookup is open for, or <c>null</c>.</summary>
        protected CriteriaRow? PickingRow { get; private set; }

        /// <summary>Returns a framework text in the user's language.</summary>
        /// <param name="key">The text's key in <see cref="PolhemUIText"/>.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>Returns the caption of a row, marked when the row is required.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The caption.</returns>
        protected static string RowCaption(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Kind == CriteriaRowKind.Required ? row.Field.Caption + " *" : row.Field.Caption;
        }

        /// <summary>Returns whether a row offers a choice of operator; a field with one operator shows none.</summary>
        /// <param name="row">The row.</param>
        /// <returns>Whether to show the operator.</returns>
        protected static bool ShowsOperator(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Field.Operators.Count > 1;
        }

        /// <summary>Returns whether a row can be removed; a required one cannot.</summary>
        /// <param name="row">The row.</param>
        /// <returns>Whether to offer removing it.</returns>
        protected static bool IsRemovable(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Kind != CriteriaRowKind.Required;
        }

        /// <summary>Returns whether a row is a range of days, which offers the relative periods.</summary>
        /// <param name="row">The row.</param>
        /// <returns>Whether the row is a date range.</returns>
        protected static bool IsDateRange(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Operator == CriteriaOperator.Range && row.Field.Kind is CriteriaFieldKind.Date or CriteriaFieldKind.DateTime;
        }

        /// <summary>Returns the type of the native input a row's value is typed into: <c>number</c>, <c>date</c> or <c>text</c>.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The input type.</returns>
        protected static string InputTypeOf(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Field.Kind switch
            {
                CriteriaFieldKind.Number => "number",
                CriteriaFieldKind.Date or CriteriaFieldKind.DateTime => "date",
                _ => "text",
            };
        }

        /// <summary>Returns the localized name of an operator.</summary>
        /// <param name="operator">The operator.</param>
        /// <returns>The name.</returns>
        protected string OperatorText(CriteriaOperator @operator) => Text(@operator switch
        {
            CriteriaOperator.Contains => PolhemUIText.CriteriaContains,
            CriteriaOperator.StartsWith => PolhemUIText.CriteriaStartsWith,
            CriteriaOperator.Equal => PolhemUIText.CriteriaEqual,
            CriteriaOperator.Range => PolhemUIText.CriteriaRange,
            _ => PolhemUIText.CriteriaIn,
        });

        /// <summary>Returns the name of a period, or the caption of the period menu for <c>null</c>.</summary>
        /// <param name="period">The period, or <c>null</c>.</param>
        /// <returns>The text.</returns>
        protected string PeriodText(RelativeDate? period)
            => period is { } value ? CriteriaRowSummary.PeriodText(value, Text) : Text(PolhemUIText.DatePeriod);

        /// <summary>Adds an optional row for a field.</summary>
        /// <param name="field">The field.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task AddRowAsync(CriteriaField field, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(field);
            Criteria.Add(field);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Removes a row that is not required.</summary>
        /// <param name="row">The row.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task RemoveRowAsync(CriteriaRow row, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            return Criteria.Remove(row) ? OnChangedAsync(cancellationToken) : Task.CompletedTask;
        }

        /// <summary>Changes a row's operator; what was entered stays only between the single-value operators.</summary>
        /// <param name="row">The row.</param>
        /// <param name="operator">The new operator.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task ChangeOperatorAsync(CriteriaRow row, CriteriaOperator @operator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.IsOperatorLocked || row.Operator == @operator) return Task.CompletedTask;
            row.ChangeOperator(@operator);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Opens the lookup of a relation row's program; the UI package renders it while <see cref="PickingRow"/> is set.</summary>
        /// <param name="row">The row.</param>
        protected void BeginPick(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            PickingRow = row;
        }

        /// <summary>Adds the picked record to the row the lookup was open for and closes the lookup.</summary>
        /// <param name="picked">The picked row.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task CommitPickAsync(DataRow picked, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(picked);
            var row = PickingRow;
            PickingRow = null;
            if (row is null || !row.TryAddValue(picked[SysFields.RowId], PickedText(picked))) return Task.CompletedTask;
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Closes the lookup without picking.</summary>
        protected void CancelPick() => PickingRow = null;

        /// <summary>Closes a lookup that could not open and names why above the rows.</summary>
        /// <param name="exception">What went wrong.</param>
        protected void OnPickFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            PickingRow = null;
            _message = exception.Message;
        }

        /// <summary>Asks the host to run the query when every required row has a value; otherwise names the empty ones.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected async Task RequestSearchAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var missing = Criteria.MissingRequired.Select(r => r.Field.Caption).ToArray();
            if (missing.Length > 0)
            {
                _message = string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.QueryRequiredMissing), string.Join(", ", missing));
                return;
            }
            _message = null;
            await SearchRequested.InvokeAsync().ConfigureAwait(true);
        }

        /// <summary>Removes every row that is not required, empties the required ones and searches.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected async Task ClearAndSearchAsync(CancellationToken cancellationToken = default)
        {
            Criteria.Clear();
            _typed.Clear();
            await OnChangedAsync(cancellationToken).ConfigureAwait(true);
            await RequestSearchAsync(cancellationToken).ConfigureAwait(true);
        }

        /// <summary>Asks the host to close the pane.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CloseRequested.InvokeAsync();
        }

        /// <summary>Closes the pane on Escape, unless a lookup over it takes the key.</summary>
        /// <param name="e">The key event.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task OnKeyDownAsync(KeyboardEventArgs e, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(e);
            return e.Key == "Escape" && PickingRow is null ? CloseAsync(cancellationToken) : Task.CompletedTask;
        }

        private Task OnChangedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CriteriaChanged.InvokeAsync();
        }

        private static string PickedText(DataRow picked)
        {
            foreach (var column in new[] { SysFields.Name, SysFields.Id })
            {
                if (picked.Table.Columns.Contains(column)
                    && Convert.ToString(picked[column], CultureInfo.CurrentCulture) is { Length: > 0 } text)
                    return text;
            }
            return Convert.ToString(picked[SysFields.RowId], CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
