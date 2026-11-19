using System.Globalization;
using Polhem.Definition.Collections;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The value editors of <see cref="CriteriaPanelBase"/>: what each input shows, and how what the user typed or
    /// picked becomes the row's value.
    /// </summary>
    public abstract partial class CriteriaPanelBase
    {
        private const string DayFormat = "yyyy-MM-dd";
        private const int ValueSlot = 0;
        private const int FromSlot = 1;
        private const int ToSlot = 2;

        // What the user typed, kept while it still reads as the row's value, so an input is not rewritten under the
        // cursor (a trailing space the row trims, say). Text that no longer matches the value, because the host
        // changed the row, gives way to the value.
        private readonly Dictionary<(CriteriaRow Row, int Slot), string> _typed = [];

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

        /// <summary>
        /// Gets a value indicating whether the row offers "me" (<see cref="CriteriaValue.CurrentUser"/>) and does not
        /// hold it yet, so the pane shows the button that adds it.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns><see langword="true"/> when the button is shown.</returns>
        protected static bool CanAddCurrentUser(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Operator == CriteriaOperator.In && row.Field.OffersCurrentUser && !row.Values.Any(v => v.IsCurrentUser);
        }

        /// <summary>Adds "me" (<see cref="CriteriaValue.CurrentUser"/>) to a relation row that offers it.</summary>
        /// <param name="row">The row.</param>
        /// <param name="cancellationToken">A token that cancels the operation before it starts.</param>
        /// <returns>The operation.</returns>
        protected Task AddCurrentUserAsync(CriteriaRow row, CancellationToken cancellationToken = default)
        {
            if (!CanAddCurrentUser(row)) return Task.CompletedTask;
            row.AddCurrentUser();
            return OnChangedAsync(cancellationToken);
        }

        /// <summary>Returns the text a value of a relation or choice row shows, "me" in the reader's language.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The text.</returns>
        protected string ValueText(CriteriaValue value) => CriteriaRowSummary.ValueText(value, Text);

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
    }
}
