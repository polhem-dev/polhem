using System.Globalization;
using Avalonia.Controls;
using Avalonia.DemoCenter.Modules.DataEditors;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Filters;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core.Criteria;

namespace Avalonia.DemoCenter.Modules.QueryConditions
{
    /// <summary>
    /// <see cref="CriteriaPanel"/> on its own: the fields come from the schema's query fields, the host adds a required
    /// row and a default one, and on <see cref="CriteriaPanel.SearchRequested"/> turns the rows into the filter a list
    /// request carries with <see cref="GetListFilterConverter"/>.
    /// </summary>
    public sealed class CriteriaPanelModule : DemoModuleBase
    {
        private const string EmptyLog = "(No events yet — add a condition, change a value or press Search)";

        /// <inheritdoc/>
        public override string Category => "Query Conditions";

        /// <inheritdoc/>
        public override string Title => "Condition pane on its own";

        /// <inheritdoc/>
        public override string Description =>
            "CriteriaPanel bound to the fields a schema offers (text, code, choice, date, number, yes/no). Order Date is a "
            + "required row and Customer a default one; Search is refused while a required row is empty. The filter "
            + "GetListFilterConverter builds and the panel's events are shown on the right.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = OrderQueryData.BuildSchema();
            var fields = CriteriaField.FromQueryFields(schema);

            // A host adds the rows a query starts with; a report would add its required ones the same way.
            var criteria = new QueryCriteria();
            criteria.Add(fields.Single(f => f.FieldName == "order_date"), CriteriaOperator.Range, CriteriaRowKind.Required);
            criteria.Add(fields.Single(f => f.FieldName == "customer_name"), kind: CriteriaRowKind.Default);

            var panel = new CriteriaPanel();
            panel.Bind(fields, criteria);

            var filterText = new SelectableTextBlock
            {
                Text = "(Press Search to build the filter)",
                FontFamily = new FontFamily("Menlo, Consolas, monospace"),
                TextWrapping = TextWrapping.Wrap,
            };
            var log = new TextBlock { Text = EmptyLog, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
            var lines = new List<string>();
            var seq = 0;
            void Append(string entry)
            {
                seq++;
                lines.Insert(0, $"{seq:D2}  {entry}");
                if (lines.Count > 12)
                    lines.RemoveAt(lines.Count - 1);
                log.Text = string.Join(Environment.NewLine, lines);
            }

            panel.CriteriaChanged += (_, _) => Append($"CriteriaChanged   {panel.Criteria.Rows.Count} row(s)");
            panel.CloseRequested += (_, _) => Append("CloseRequested    (a host would hide the pane)");
            panel.SearchRequested += (_, _) =>
            {
                Append("SearchRequested");
                var filter = GetListFilterConverter.ToFilter(panel.Criteria, panel.Today(),
                    CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
                filterText.Text = filter is null ? "(No condition has a value: the list request carries no filter)" : Describe(filter, 0);
            };

            var pane = new Border
            {
                Child = panel,
                Width = 380,
                MinHeight = 480,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                CornerRadius = new CornerRadius(6),
                VerticalAlignment = VerticalAlignment.Top,
            };

            var right = new StackPanel { Spacing = 12, Margin = new Thickness(16, 0, 0, 0) };
            right.Children.Add(DataEditorParts.Section("Filter for GetList",
                "GetListFilterConverter.ToFilter(criteria, today, firstDayOfWeek), the FilterNode a list request sends.", filterText));
            right.Children.Add(DataEditorParts.Section("Event log (newest first)",
                "CriteriaChanged, SearchRequested and CloseRequested.", log));

            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(right, 1);
            layout.Children.Add(pane);
            layout.Children.Add(right);
            return new ScrollViewer { Content = layout };
        }

        private static string Describe(FilterNode node, int depth)
        {
            string indent = new(' ', depth * 2);
            return node switch
            {
                FilterGroup group => indent + (group.Operator == LogicalOperator.Or ? "Any of" : "All of") + Environment.NewLine
                    + string.Join(Environment.NewLine, group.Nodes.Select(n => Describe(n, depth + 1))),
                FilterCondition condition => indent + $"{condition.FieldName} {condition.Operator} {Format(condition.Value)}"
                    + (condition.SecondValue is null ? string.Empty : $" and {Format(condition.SecondValue)}"),
                _ => indent + node.GetType().Name,
            };
        }

        private static string Format(object? value) => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            DateTime instant => instant.ToString(instant.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            object[] values => "[" + string.Join(", ", values.Select(Format)) + "]",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
