using System.Globalization;
using Polhem.Core;
using Polhem.Definition.Collections;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Code-behind for <c>DynamicForm.razor</c>. Renders the master section(s) of a
    /// <see cref="FormLayout"/> by dispatching each <see cref="LayoutField"/> to the input
    /// element appropriate to its <see cref="ControlType"/>.
    /// </summary>
    /// <remarks>
    /// Only the master area is rendered, and only master fields are bound. The layout's detail grids
    /// (<see cref="FormLayout.Details"/>) are not rendered by this component or by
    /// <see cref="FormPage"/>, so detail rows cannot be viewed or edited in this package;
    /// <see cref="DynamicGrid"/> is a read-only list, which <see cref="FormPage"/> uses for the
    /// record list.
    /// </remarks>
    public sealed partial class DynamicForm : ComponentBase
    {
        private static readonly ListItem[] s_emptyOptions = Array.Empty<ListItem>();

        /// <summary>
        /// Gets or sets the form layout that drives the rendering loop.
        /// </summary>
        [Parameter]
        public FormLayout? Layout { get; set; }

        /// <summary>
        /// Gets or sets the data object that backs two-way binding for each input.
        /// </summary>
        [Parameter]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the inline id prefix applied to every rendered input. Useful for
        /// hosting pages that embed multiple <see cref="DynamicForm"/> instances on the
        /// same page and need unique DOM ids.
        /// </summary>
        [Parameter]
        public string IdPrefix { get; set; } = "polhem-form";

        private IEnumerable<LayoutSection> EnumerateSections()
            => Layout?.Sections ?? Enumerable.Empty<LayoutSection>();

        private static IEnumerable<LayoutField> EnumerateFields(LayoutSection section)
            => section.Fields?.Where(f => f.Visible) ?? Enumerable.Empty<LayoutField>();

        private IEnumerable<ListItem> EnumerateOptions(LayoutField field)
        {
            var formField = DataObject?.GetFormField(field.FieldName);
            return formField?.ListItems ?? (IEnumerable<ListItem>)s_emptyOptions;
        }

        private string FieldInputId(LayoutField field)
            => string.Create(CultureInfo.InvariantCulture, $"{IdPrefix}-{field.FieldName}");

        private string BuildGridStyle()
        {
            var columns = Layout?.ColumnCount ?? 1;
            if (columns < 1) columns = 1;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"display:grid;grid-template-columns:repeat({columns},minmax(0,1fr));gap:8px");
        }

        private static void SetTimeField(FormDataObject dataObject, string fieldName, string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                // An emptied box is an explicit "unset". Midnight is a legal value, so the empty
                // string is how an unfilled time of day is stored.
                dataObject.SetField(fieldName, string.Empty);
                return;
            }

            // Input that does not parse keeps the stored value, matching the desktop `TimeEdit`, so a
            // stray keystroke does not erase data. It is not passed on to `SetField` either, because the
            // time column coercion throws `FormatException` for it inside the event handler.
            string normalized = ValueUtilities.CTimeString(input);
            if (normalized.Length > 0)
                dataObject.SetField(fieldName, normalized);
        }

        // A date input accepts only yyyy-MM-dd and shows nothing for any other value, so a value that
        // carries a time of day is cut to its date part rather than rendered blank.
        private static string ToDateInputValue(string rawValue)
            => TryParseBindingValue(rawValue, out var value)
                ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : rawValue;

        // A datetime-local input needs the time part even at midnight, which the binding string omits.
        private static string ToDateTimeInputValue(string rawValue)
            => TryParseBindingValue(rawValue, out var value)
                ? value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                : string.Empty;

        // The read-only form: the circuit culture's short date with its long time, as the grid shows it.
        private static string ToDateTimeDisplay(string rawValue)
            => TryParseBindingValue(rawValue, out var value)
                ? value.ToString("G", CultureInfo.CurrentCulture)
                : string.Empty;

        private static void SetDateTimeField(FormDataObject dataObject, string fieldName, string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                dataObject.SetField(fieldName, string.Empty);
                return;
            }

            // The browser only reports complete values, but a value that still does not parse keeps the
            // stored one rather than throwing inside the event handler.
            if (TryParseBindingValue(input, out _))
                dataObject.SetField(fieldName, input);
        }

        private static bool TryParseBindingValue(string? value, out DateTime result)
        {
            if (!string.IsNullOrEmpty(value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }
            result = default;
            return false;
        }

        private static string BuildFieldStyle(LayoutField field)
        {
            var rowSpan = field.RowSpan < 1 ? 1 : field.RowSpan;
            var colSpan = field.ColumnSpan < 1 ? 1 : field.ColumnSpan;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"grid-row:span {rowSpan};grid-column:span {colSpan}");
        }
    }
}
