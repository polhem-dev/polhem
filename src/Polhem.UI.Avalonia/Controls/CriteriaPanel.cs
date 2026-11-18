using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Query conditions a user adds one row at a time, each a field, an operator and a value editor chosen by the
    /// field's kind. It edits a <see cref="QueryCriteria"/> and raises <see cref="SearchRequested"/>; the view that
    /// hosts it converts the criteria for the backend method it calls and runs the query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is laid out as a pane: a heading with a close button, the rows stacked in a scrolled area (each row's caption
    /// above its operator and value), and the buttons docked below the rows, so they stay on screen however many rows
    /// there are and above an on-screen keyboard.
    /// </para>
    /// <para>
    /// <see cref="Polhem.UI.Avalonia.Views.ListView"/> hosts one behind its Query button, with the fields from
    /// <see cref="CriteriaField.FromQueryFields"/> and <see cref="GetListFilterConverter"/>. Another view hosts it the
    /// same way: put the starting rows (a report's required ones, say) into a <see cref="QueryCriteria"/>, call
    /// <see cref="Bind"/>, and run the query on <see cref="SearchRequested"/>, which is raised only when every
    /// required row has a value.
    /// </para>
    /// </remarks>
    public sealed partial class CriteriaPanel : UserControl
    {
        /// <summary>The style class of the framed block around each condition row.</summary>
        internal const string RowClass = "criteria-row";

        private static readonly CriteriaOperator[] s_noOperators = [];

        private readonly StackPanel _rows = new() { Spacing = 12 };
        private readonly ScrollViewer _scroller;
        private readonly Dictionary<CriteriaRow, Control> _rowControls = [];
        private readonly Button _addButton;
        private readonly MenuFlyout _addMenu = new();
        private readonly MessageBar _message = new() { Margin = new Thickness(0, 0, 0, 6) };
        private QueryCriteria _criteria = new();
        private IReadOnlyList<CriteriaField> _fields = [];

        /// <summary>Initializes a new instance of <see cref="CriteriaPanel"/> with no fields.</summary>
        public CriteriaPanel()
        {
            _addButton = new Button { Content = UIText.Get(PolhemUIText.AddCondition), Flyout = _addMenu, IsEnabled = false };
            var searchButton = new Button { Content = UIText.Get(PolhemUIText.Search) };
            searchButton.Click += (_, _) => RequestSearch();
            var clearButton = new Button { Content = UIText.Get(PolhemUIText.FilterClear) };
            clearButton.Click += (_, _) => ClearAndSearch();

            var footer = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var button in new[] { _addButton, searchButton, clearButton })
            {
                button.Margin = new Thickness(0, 0, 8, 4);
                footer.Children.Add(button);
            }

            var closeButton = new Button { Content = "✕", HorizontalAlignment = HorizontalAlignment.Right };
            AutomationNameOf(closeButton, UIText.Get(PolhemUIText.Close));
            closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(closeButton, Dock.Right);
            header.Children.Add(closeButton);
            header.Children.Add(new TextBlock
            {
                Text = UIText.Get(PolhemUIText.QueryConditions),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var body = new StackPanel();
            body.Children.Add(_message);
            body.Children.Add(_rows);
            _scroller = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

            var host = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            host.Children.Add(header);
            host.Children.Add(footer);
            host.Children.Add(_scroller);
            Content = host;

            // Handled events too: a date picker handles Enter to read the date typed into it, and the query should
            // still run, after the picker has read the date.
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        /// <summary>Raised when the user asks to run the query and every required row has a value.</summary>
        public event EventHandler? SearchRequested;

        /// <summary>Raised when a row is added or removed, or what a row holds changes.</summary>
        public event EventHandler? CriteriaChanged;

        /// <summary>Raised when the user closes the pane with its close button.</summary>
        public event EventHandler? CloseRequested;

        /// <summary>Gets the criteria the panel edits.</summary>
        public QueryCriteria Criteria => _criteria;

        /// <summary>Gets the fields "add condition" offers.</summary>
        public IReadOnlyList<CriteriaField> Fields => _fields;

        /// <summary>
        /// Gets or sets how today is read for the date periods the date rows show; defaults to the machine's date.
        /// </summary>
        public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

        /// <summary>Gets the message shown above the rows, such as the required rows left empty.</summary>
        internal string MessageText => _message.Text;

        /// <summary>
        /// Shows the fields "add condition" offers and the rows of <paramref name="criteria"/>.
        /// </summary>
        /// <param name="fields">The fields offered, in order.</param>
        /// <param name="criteria">The criteria to edit, usually with no rows for a list.</param>
        public void Bind(IReadOnlyList<CriteriaField> fields, QueryCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(fields);
            ArgumentNullException.ThrowIfNull(criteria);
            _fields = fields;
            _criteria = criteria;
            _addMenu.Items.Clear();
            foreach (var field in fields)
            {
                var item = new MenuItem { Header = field.Caption };
                item.Click += (_, _) => AddRow(field);
                _addMenu.Items.Add(item);
            }
            _addButton.IsEnabled = fields.Count > 0;
            _message.Clear();
            RebuildRows();
        }

        /// <summary>
        /// Shows the rows of <see cref="Criteria"/> again, after the host added, removed or changed rows outside the
        /// panel.
        /// </summary>
        public void Refresh() => RebuildRows();

        /// <summary>Scrolls the pane so <paramref name="row"/> is in view.</summary>
        /// <param name="row">A row of <see cref="Criteria"/>; a row the panel does not show is ignored.</param>
        public void ScrollToRow(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (_rowControls.TryGetValue(row, out var control))
                control.BringIntoView();
        }

        /// <summary>Adds an optional row for <paramref name="field"/> and shows it.</summary>
        /// <param name="field">The field.</param>
        /// <returns>The new row.</returns>
        internal CriteriaRow AddRow(CriteriaField field)
        {
            var row = _criteria.Add(field);
            _rows.Children.Add(BuildRow(row));
            OnChanged();
            ScrollToRow(row);
            return row;
        }

        /// <summary>
        /// Runs the query when every required row has a value; otherwise names the empty ones.
        /// </summary>
        internal void RequestSearch()
        {
            var missing = _criteria.MissingRequired.Select(r => r.Field.Caption).ToArray();
            if (missing.Length > 0)
            {
                _message.ShowWarning(string.Format(CultureInfo.CurrentCulture,
                    UIText.Get(PolhemUIText.QueryRequiredMissing), string.Join(", ", missing)));
                return;
            }
            _message.Clear();
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Removes every row that is not required, empties the required ones and runs the query.</summary>
        internal void ClearAndSearch()
        {
            _criteria.Clear();
            RebuildRows();
            OnChanged();
            RequestSearch();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            // A button or a drop-down keeps Enter for itself: it presses the button or picks the item.
            if (e.Source is Button or ComboBox or ComboBoxItem) return;
            e.Handled = true;
            RequestSearch();
        }

        private void OnChanged() => CriteriaChanged?.Invoke(this, EventArgs.Empty);

        private void RebuildRows()
        {
            _rows.Children.Clear();
            _rowControls.Clear();
            foreach (var row in _criteria.Rows)
                _rows.Children.Add(BuildRow(row));
        }

        private Control BuildRow(CriteriaRow row)
        {
            // The caption on its own line, with the remove button at its end; the operator and the value below. Each
            // row is a framed block, so where one condition ends and the next begins is plain at a glance.
            var panel = new StackPanel { Spacing = 4 };
            var block = new Border
            {
                Child = panel,
                Padding = new Thickness(10, 8),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Classes = { RowClass },
            };
            block.Bind(Border.BorderBrushProperty, block.GetResourceObservable("SemiColorBorder", value => value as IBrush ?? Brushes.LightGray));
            block.Bind(Border.BackgroundProperty, block.GetResourceObservable("SemiColorFill0"));
            var captionLine = new DockPanel();
            panel.Children.Add(captionLine);
            var line = new WrapPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(line);

            var valueHost = new ContentControl { Content = BuildValueEditor(row) };
            var operators = row.Field.Operators.Count > 1 ? row.Field.Operators : s_noOperators;
            if (operators.Count > 0)
            {
                var operatorBox = new ComboBox
                {
                    ItemsSource = operators.Select(OperatorText).ToArray(),
                    SelectedIndex = IndexOf(operators, row.Operator),
                    IsEnabled = !row.IsOperatorLocked,
                    MinWidth = 110,
                    Margin = new Thickness(0, 0, 6, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                operatorBox.PropertyChanged += (_, e) =>
                {
                    if (e.Property != SelectingItemsControl.SelectedIndexProperty) return;
                    if (operatorBox.SelectedIndex < 0 || operators[operatorBox.SelectedIndex] == row.Operator) return;
                    row.ChangeOperator(operators[operatorBox.SelectedIndex]);
                    valueHost.Content = BuildValueEditor(row);
                    OnChanged();
                };
                line.Children.Add(operatorBox);
            }
            line.Children.Add(valueHost);

            if (row.Kind != CriteriaRowKind.Required)
            {
                var remove = new Button { Content = "✕", Padding = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
                AutomationNameOf(remove, UIText.Get(PolhemUIText.RemoveCondition));
                remove.Click += (_, _) =>
                {
                    if (!_criteria.Remove(row)) return;
                    _rows.Children.Remove(block);
                    _rowControls.Remove(row);
                    OnChanged();
                };
                DockPanel.SetDock(remove, Dock.Right);
                captionLine.Children.Add(remove);
            }
            captionLine.Children.Add(new TextBlock
            {
                Text = row.Kind == CriteriaRowKind.Required ? row.Field.Caption + " *" : row.Field.Caption,
                VerticalAlignment = VerticalAlignment.Center,
            });
            _rowControls[row] = block;
            return block;
        }

        private static int IndexOf(IReadOnlyList<CriteriaOperator> operators, CriteriaOperator @operator)
        {
            for (int i = 0; i < operators.Count; i++)
                if (operators[i] == @operator) return i;
            return -1;
        }

        private static string OperatorText(CriteriaOperator @operator) => UIText.Get(@operator switch
        {
            CriteriaOperator.Contains => PolhemUIText.CriteriaContains,
            CriteriaOperator.StartsWith => PolhemUIText.CriteriaStartsWith,
            CriteriaOperator.Equal => PolhemUIText.CriteriaEqual,
            CriteriaOperator.Range => PolhemUIText.CriteriaRange,
            _ => PolhemUIText.CriteriaIn,
        });

        private static void AutomationNameOf(Control control, string name)
        {
            global::Avalonia.Automation.AutomationProperties.SetName(control, name);
            ToolTip.SetTip(control, name);
        }
    }
}
