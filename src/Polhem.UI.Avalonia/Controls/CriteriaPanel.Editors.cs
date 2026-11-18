using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Polhem.Definition;
using Polhem.Definition.Language;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The value editors of <see cref="CriteriaPanel"/>, one shape per field kind and operator.
    /// </summary>
    /// <remarks>
    /// Every editor writes to its row through <see cref="AvaloniaObject.PropertyChanged"/> rather than the controls'
    /// semantic events, which do not fire for a value set from code (the Avalonia controls pitfall log, item 6).
    /// </remarks>
    public sealed partial class CriteriaPanel
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

        private Control BuildValueEditor(CriteriaRow row)
        {
            return row.Operator switch
            {
                CriteriaOperator.In when row.Field.Kind == CriteriaFieldKind.Relation => BuildRelationEditor(row),
                CriteriaOperator.In => BuildChoiceEditor(row),
                CriteriaOperator.Range when row.Field.Kind is CriteriaFieldKind.Date or CriteriaFieldKind.DateTime => BuildDateRangeEditor(row),
                CriteriaOperator.Range => BuildPair(
                    BuildSingle(row, row.From, value => row.SetRange(value, row.To), UIText.Get(PolhemUIText.FilterFrom)),
                    BuildSingle(row, row.To, value => row.SetRange(row.From, value), UIText.Get(PolhemUIText.FilterTo))),
                _ when row.Field.Kind == CriteriaFieldKind.Boolean => BuildBooleanEditor(row),
                _ => BuildSingle(row, row.Value, row.SetValue, string.Empty),
            };
        }

        // One value of the row's kind: a text box, a number box or a date picker.
        private Control BuildSingle(CriteriaRow row, object? current, Action<object?> write, string watermark)
        {
            switch (row.Field.Kind)
            {
                case CriteriaFieldKind.Number:
                    var number = new NumericUpDown
                    {
                        Value = current as decimal?,
                        ShowButtonSpinner = false,
                        Width = 140,
                        PlaceholderText = watermark,
                    };
                    number.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != NumericUpDown.ValueProperty) return;
                        write(number.Value);
                        OnChanged();
                    };
                    return number;
                case CriteriaFieldKind.Date or CriteriaFieldKind.DateTime:
                    var date = NewDatePicker(current as DateOnly?, watermark);
                    date.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != CalendarDatePicker.SelectedDateProperty) return;
                        write(date.SelectedDate);
                        OnChanged();
                    };
                    return date;
                default:
                    var text = new TextBox { Text = current as string, Width = 200, PlaceholderText = watermark };
                    text.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != TextBox.TextProperty) return;
                        write(text.Text);
                        OnChanged();
                    };
                    return text;
            }
        }

        private static CalendarDatePicker NewDatePicker(DateOnly? day, string watermark) => new()
        {
            SelectedDate = day?.ToDateTime(TimeOnly.MinValue),
            Width = 150,
            PlaceholderText = watermark,
        };

        private static WrapPanel BuildPair(Control first, Control second)
        {
            var pair = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
            pair.Children.Add(first);
            pair.Children.Add(new TextBlock { Text = "~", VerticalAlignment = VerticalAlignment.Center });
            pair.Children.Add(second);
            return pair;
        }

        // A date range: two pickers and a menu of periods named relative to today. Choosing a period keeps it
        // relative in the row and shows its days; editing a day afterwards turns the row back into fixed days.
        private Control BuildDateRangeEditor(CriteriaRow row)
        {
            var today = Today();
            var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            DateOnly? start = row.From as DateOnly?;
            DateOnly? end = row.To as DateOnly?;
            if (row.RelativeDate is { } relative)
                (start, end) = relative.Resolve(today, firstDay);
            var from = NewDatePicker(start, UIText.Get(PolhemUIText.FilterFrom));
            var to = NewDatePicker(end, UIText.Get(PolhemUIText.FilterTo));
            var periodButton = new Button { Content = PeriodText(row.RelativeDate), VerticalAlignment = VerticalAlignment.Center };
            bool showingPeriod = false;

            void WriteDays()
            {
                if (showingPeriod) return;
                row.SetRange(from.SelectedDate, to.SelectedDate);
                periodButton.Content = PeriodText(null);
                OnChanged();
            }
            from.PropertyChanged += (_, e) => { if (e.Property == CalendarDatePicker.SelectedDateProperty) WriteDays(); };
            to.PropertyChanged += (_, e) => { if (e.Property == CalendarDatePicker.SelectedDateProperty) WriteDays(); };

            var menu = new MenuFlyout();
            foreach (var period in s_periods)
            {
                var item = new MenuItem { Header = PeriodText(period) };
                item.Click += (_, _) =>
                {
                    row.SetRelativeDate(period);
                    var (first, last) = period.Resolve(Today(), firstDay);
                    showingPeriod = true;
                    from.SelectedDate = first.ToDateTime(TimeOnly.MinValue);
                    to.SelectedDate = last.ToDateTime(TimeOnly.MinValue);
                    showingPeriod = false;
                    periodButton.Content = PeriodText(period);
                    OnChanged();
                };
                menu.Items.Add(item);
            }
            periodButton.Flyout = menu;

            var editor = BuildPair(from, to);
            editor.Children.Add(periodButton);
            return editor;
        }

        private static string PeriodText(RelativeDate? period)
            => period is { } value ? CriteriaRowSummary.PeriodText(value, UIText.Get) : UIText.Get(PolhemUIText.DatePeriod);

        private Control BuildBooleanEditor(CriteriaRow row)
        {
            var box = new ComboBox
            {
                ItemsSource = new[] { UIText.Get(PolhemUIText.CriteriaAny), UIText.Get(PolhemUIText.True), UIText.Get(PolhemUIText.False) },
                SelectedIndex = row.Value switch { true => 1, false => 2, _ => 0 },
                MinWidth = 100,
            };
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property != SelectingItemsControl.SelectedIndexProperty) return;
                row.SetValue(box.SelectedIndex switch { 1 => true, 2 => false, _ => null });
                OnChanged();
            };
            return box;
        }

        // A field with listed values: one check box per value.
        private Control BuildChoiceEditor(CriteriaRow row)
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 520 };
            foreach (var item in row.Field.ListItems)
            {
                var check = new CheckBox
                {
                    Content = item.Text,
                    Margin = new Thickness(0, 0, 12, 0),
                    IsChecked = row.Values.Any(v => SameValue(v.Value, item.Value)),
                };
                check.PropertyChanged += (_, e) =>
                {
                    if (e.Property != ToggleButton.IsCheckedProperty) return;
                    if (check.IsChecked == true)
                        row.TryAddValue(item.Value, item.Text);
                    else
                        foreach (var value in row.Values.Where(v => SameValue(v.Value, item.Value)).ToArray())
                            row.RemoveValue(value.Value);
                    OnChanged();
                };
                panel.Children.Add(check);
            }
            return panel;
        }

        private static bool SameValue(object value, string listItemValue)
            => string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), listItemValue, StringComparison.Ordinal);

        // A relation field: the picked records as removable chips, and a button that opens the lookup.
        private Control BuildRelationEditor(CriteriaRow row)
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 520 };

            void Refresh()
            {
                panel.Children.Clear();
                foreach (var value in row.Values)
                {
                    var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 8, 4) };
                    chip.Children.Add(new TextBlock { Text = value.Text, VerticalAlignment = VerticalAlignment.Center });
                    var remove = new Button { Content = "✕", Padding = new Thickness(4, 0) };
                    AutomationNameOf(remove, string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.RemoveItem), value.Text));
                    remove.Click += (_, _) =>
                    {
                        row.RemoveValue(value.Value);
                        Refresh();
                        OnChanged();
                    };
                    chip.Children.Add(remove);
                    panel.Children.Add(chip);
                }
                var pick = new Button { Content = UIText.Get(PolhemUIText.PickValue), Margin = new Thickness(0, 0, 0, 4) };
                pick.Click += async (_, _) => await PickRelationAsync(row, Refresh).ConfigureAwait(true);
                panel.Children.Add(pick);
            }

            Refresh();
            return panel;
        }

        private async Task PickRelationAsync(CriteriaRow row, Action refresh)
        {
            try
            {
                var picked = await LookupDialog.ShowAsync(this, row.Field.RelationProgId).ConfigureAwait(true);
                if (picked is null) return;
                if (row.TryAddValue(picked[SysFields.RowId], PickedText(picked)))
                {
                    refresh();
                    OnChanged();
                }
            }
            catch (Exception ex)
            {
                // Boundary: a click handler; a lookup that cannot open is shown on the panel, which stays usable.
                _message.Show(ex.Message);
            }
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
