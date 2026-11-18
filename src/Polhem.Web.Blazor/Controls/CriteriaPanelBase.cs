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
    public abstract class CriteriaPanelBase : ComponentBase
    {
        private const string DayFormat = "yyyy-MM-dd";
        private const int ValueSlot = 0;
        private const int FromSlot = 1;
        private const int ToSlot = 2;

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

        // What the user typed, kept while it still reads as the row's value, so an input is not rewritten under the
        // cursor (a trailing space the row trims, say). Text that no longer matches the value, because the host
        // changed the row, gives way to the value.
        private readonly Dictionary<(CriteriaRow Row, int Slot), string> _typed = [];

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

        /// <summary>Invoked when a row is added or removed, or what a row holds changes.</summary>
        [Parameter]
        public EventCallback CriteriaChanged { get; set; }

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets the periods a date range offers.</summary>
        protected static IReadOnlyList<RelativeDate> Periods => s_periods;

        /// <summary>Gets the message above the rows, such as the required rows left empty, or <c>null</c>.</summary>
        protected string? Message { get; private set; }

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

        /// <summary>Returns the text a single-value row's input shows.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The text.</returns>
        protected string GetValueText(CriteriaRow row) => Shown(row, ValueSlot, row.Value);

        /// <summary>Returns the text the input of the start of a range shows; a relative period shows its first day.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The text.</returns>
        protected string GetFromText(CriteriaRow row) => Shown(row, FromSlot, ShownRange(row).From);

        /// <summary>Returns the text the input of the end of a range shows; a relative period shows its last day.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The text.</returns>
        protected string GetToText(CriteriaRow row) => Shown(row, ToSlot, ShownRange(row).To);

        /// <summary>Sets a single-value row from what the user typed; blank or unreadable text clears it.</summary>
        /// <param name="row">The row.</param>
        /// <param name="text">The text.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetValueTextAsync(CriteriaRow row, string? text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            Remember(row, ValueSlot, text);
            row.SetValue(Parse(row, text));
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Sets the start of a range from what the user typed, keeping the end shown.</summary>
        /// <param name="row">The row.</param>
        /// <param name="text">The text.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetFromTextAsync(CriteriaRow row, string? text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            var (_, to) = ShownRange(row);
            Remember(row, FromSlot, text);
            row.SetRange(Parse(row, text), to);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Sets the end of a range from what the user typed, keeping the start shown.</summary>
        /// <param name="row">The row.</param>
        /// <param name="text">The text.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetToTextAsync(CriteriaRow row, string? text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            var (from, _) = ShownRange(row);
            Remember(row, ToSlot, text);
            row.SetRange(from, Parse(row, text));
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Sets a date range to a period named relative to today.</summary>
        /// <param name="row">The row.</param>
        /// <param name="period">The period.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetPeriodAsync(CriteriaRow row, RelativeDate period, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            Forget(row);
            row.SetRelativeDate(period);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>
        /// Returns the value of a yes / no row as the choice the editor selects: an empty string for either,
        /// <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The choice.</returns>
        protected static string GetBooleanChoice(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Value switch { true => bool.TrueString, false => bool.FalseString, _ => string.Empty };
        }

        /// <summary>Returns the text of a yes / no choice.</summary>
        /// <param name="choice">An empty string, <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.</param>
        /// <returns>The text.</returns>
        protected string BooleanChoiceText(string? choice)
            => bool.TryParse(choice, out var flag) ? Text(flag ? PolhemUIText.True : PolhemUIText.False) : Text(PolhemUIText.CriteriaAny);

        /// <summary>Sets a yes / no row from the choice the user picked.</summary>
        /// <param name="row">The row.</param>
        /// <param name="choice">An empty string for either, or <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetBooleanChoiceAsync(CriteriaRow row, string? choice, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            row.SetValue(bool.TryParse(choice, out var flag) ? flag : null);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Returns whether a listed value is in a choice row.</summary>
        /// <param name="row">The row.</param>
        /// <param name="item">The listed value.</param>
        /// <returns>Whether it is checked.</returns>
        protected static bool IsChosen(CriteriaRow row, ListItem item)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(item);
            return row.Values.Any(v => SameValue(v.Value, item.Value));
        }

        /// <summary>Adds a listed value to a choice row, or removes it.</summary>
        /// <param name="row">The row.</param>
        /// <param name="item">The listed value.</param>
        /// <param name="chosen">Whether the value is now checked.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task SetChosenAsync(CriteriaRow row, ListItem item, bool chosen, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(item);
            if (chosen)
                row.TryAddValue(item.Value, item.Text);
            else
                foreach (var value in row.Values.Where(v => SameValue(v.Value, item.Value)).ToArray())
                    row.RemoveValue(value.Value);
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Removes a value from a relation row.</summary>
        /// <param name="row">The row.</param>
        /// <param name="value">The value.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task RemoveValueAsync(CriteriaRow row, CriteriaValue value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.RemoveValue(value.Value) ? OnChangedAsync(cancellationToken) : Task.CompletedTask;
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
            Message = exception.Message;
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
                Message = string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.QueryRequiredMissing), string.Join(", ", missing));
                return;
            }
            Message = null;
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

        private DateOnly ReadToday() => Today?.Invoke() ?? DateOnly.FromDateTime(DateTime.Now);

        // A relative period shows its days, so editing one end keeps the other.
        private (object? From, object? To) ShownRange(CriteriaRow row)
        {
            if (row.RelativeDate is not { } period) return (row.From, row.To);
            var (from, to) = period.Resolve(ReadToday(), CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
            return (from, to);
        }

        private string Shown(CriteriaRow row, int slot, object? value)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (_typed.TryGetValue((row, slot), out var typed) && Equals(Parse(row, typed), Normalize(row, value)))
                return typed;
            return Format(value);
        }

        private void Remember(CriteriaRow row, int slot, string? text) => _typed[(row, slot)] = text ?? string.Empty;

        private void Forget(CriteriaRow row)
        {
            foreach (var key in _typed.Keys.Where(k => ReferenceEquals(k.Row, row)).ToArray())
                _typed.Remove(key);
        }

        // What the row would hold for the text, so typed text and the row's value compare.
        private static object? Normalize(CriteriaRow row, object? value)
            => value is string text ? Parse(row, text) : value;

        private static object? Parse(CriteriaRow row, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            switch (row.Field.Kind)
            {
                case CriteriaFieldKind.Number:
                    return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
                case CriteriaFieldKind.Date or CriteriaFieldKind.DateTime:
                    return DateOnly.TryParseExact(text.Trim(), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                        ? day
                        : null;
                default:
                    return text.Trim();
            }
        }

        private static string Format(object? value) => value switch
        {
            null => string.Empty,
            DateOnly day => day.ToString(DayFormat, CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

        private static bool SameValue(object value, string listItemValue)
            => string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), listItemValue, StringComparison.Ordinal);

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
