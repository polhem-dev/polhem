using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Connectors;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The state and rules of a grid column's filter panel, which every UI package renders with its own markup.
    /// </summary>
    /// <remarks>
    /// Opening the panel fills it with what was entered before. Applying first tries the entry on a filter state of its
    /// own and, when the filters cannot read it, shows an error and sends nothing; the real state changes only through
    /// the host, which reads the list again. Clearing applies an empty entry. Escape closes the panel and drops what was
    /// typed and not applied. <c>GridColumnFilterBehaviorTests</c> (tests/Polhem.Web.Blazor.BehaviorTests) checks these
    /// rules; the MudBlazor and Fluent UI packages each run it over their own filter.
    /// </remarks>
    public abstract class GridColumnFilterBase : ComponentBase
    {
        /// <summary>Gets or sets the column.</summary>
        [Parameter, EditorRequired]
        public LayoutColumn Column { get; set; } = default!;

        /// <summary>Gets or sets the schema field of the column.</summary>
        [Parameter, EditorRequired]
        public FormField Field { get; set; } = default!;

        /// <summary>Gets or sets what the filter takes.</summary>
        [Parameter, EditorRequired]
        public ColumnFilterKind Kind { get; set; }

        /// <summary>Gets or sets the list's filters, which hold what was entered before.</summary>
        [Parameter, EditorRequired]
        public ListFilterState Filters { get; set; } = default!;

        /// <summary>Gets or sets the callback raised with a value the filters can read.</summary>
        [Parameter]
        public EventCallback<ColumnFilterChange> Applied { get; set; }

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets whether the panel is open.</summary>
        protected bool IsOpen { get; private set; }

        /// <summary>Gets whether the last entry was refused.</summary>
        protected bool IsInvalid { get; private set; }

        /// <summary>Gets or sets the value, or the start of a range.</summary>
        protected string First { get; set; } = string.Empty;

        /// <summary>Gets or sets the end of a range.</summary>
        protected string Second { get; set; } = string.Empty;

        /// <summary>Gets whether the column is filtered now.</summary>
        protected bool IsFiltered => Filters.IsFiltered(Column.FieldName);

        /// <summary>Gets the accessible name of the button that opens the panel.</summary>
        protected string ButtonLabel
            => string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.FilterColumn), Column.Caption);

        /// <summary>Gets whether a range is of dates.</summary>
        protected bool IsDateRange => Kind is ColumnFilterKind.Date or ColumnFilterKind.DateTime;

        /// <summary>
        /// Gets the choices of a Boolean or list filter as value and text, starting with an empty choice that means no
        /// filter.
        /// </summary>
        protected IEnumerable<KeyValuePair<string, string>> Choices
        {
            get
            {
                yield return new(string.Empty, string.Empty);
                if (Kind == ColumnFilterKind.Boolean)
                {
                    yield return new(bool.TrueString, Text(PolhemUIText.True));
                    yield return new(bool.FalseString, Text(PolhemUIText.False));
                    yield break;
                }
                foreach (var item in Field.ListItems ?? []) yield return new(item.Value, item.Text);
            }
        }

        /// <summary>Returns the text a choice shows for its value.</summary>
        /// <param name="value">The choice's value.</param>
        /// <returns>The text, or the value itself when no choice has it.</returns>
        protected string ChoiceText(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (Kind == ColumnFilterKind.Boolean)
                return Text(string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase) ? PolhemUIText.True : PolhemUIText.False);
            return Field.ListItems?.FirstOrDefault(i => string.Equals(i.Value, value, StringComparison.Ordinal))?.Text ?? value;
        }

        /// <summary>Returns a framework text in the user's language.</summary>
        /// <param name="key">The text's key in <see cref="PolhemUIText"/>.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>Opens the panel, filled with what was entered before, or closes it.</summary>
        protected virtual void Toggle()
        {
            IsOpen = !IsOpen;
            IsInvalid = false;
            (First, Second) = Filters.GetEntered(Column.FieldName);
        }

        /// <summary>Closes the panel and drops what was typed and not applied.</summary>
        protected void Close() => IsOpen = false;

        /// <summary>Closes the panel on Escape.</summary>
        /// <param name="e">The key event.</param>
        protected void OnKeyDown(KeyboardEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            if (e.Key == "Escape") Close();
        }

        /// <summary>
        /// Sends the entry when the filters can read it and closes the panel; otherwise marks it invalid and keeps the
        /// panel open.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation before the entry is sent.</param>
        /// <returns>The operation.</returns>
        protected async Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!new ListFilterState().TrySet(Field, Kind, First, Second))
            {
                IsInvalid = true;
                return;
            }
            IsOpen = false;
            await Applied.InvokeAsync(new ColumnFilterChange(Field, Kind, First, Second)).ConfigureAwait(true);
        }

        /// <summary>Applies an empty entry, which removes the column's filter.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before the entry is sent.</param>
        /// <returns>The operation.</returns>
        protected Task ClearAsync(CancellationToken cancellationToken = default)
        {
            First = string.Empty;
            Second = string.Empty;
            return ApplyAsync(cancellationToken);
        }
    }
}
