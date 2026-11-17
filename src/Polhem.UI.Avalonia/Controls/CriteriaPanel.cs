using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Polhem.Definition.Language;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// The query conditions of a list: rows the user adds one at a time, each a field, an operator and a value
    /// editor chosen by the field's kind. It edits a <see cref="QueryCriteria"/> and raises
    /// <see cref="SearchRequested"/>; the owner converts the criteria and runs the query.
    /// </summary>
    internal sealed partial class CriteriaPanel : UserControl
    {
        private static readonly CriteriaOperator[] s_noOperators = [];

        private readonly StackPanel _rows = new() { Spacing = 6 };
        private readonly Button _addButton;
        private readonly MenuFlyout _addMenu = new();
        private readonly MessageBar _message = new() { Margin = new Thickness(0, 0, 0, 6) };
        private QueryCriteria _criteria = new();
        private IReadOnlyList<CriteriaField> _fields = [];
        private bool _isCompact;

        /// <summary>Initializes a new instance of <see cref="CriteriaPanel"/> with no fields.</summary>
        public CriteriaPanel()
        {
            _addButton = new Button { Content = UIText.Get(PolhemUIText.AddCondition), Flyout = _addMenu, IsEnabled = false };
            var searchButton = new Button { Content = UIText.Get(PolhemUIText.Search) };
            searchButton.Click += (_, _) => RequestSearch();
            var clearButton = new Button { Content = UIText.Get(PolhemUIText.FilterClear) };
            clearButton.Click += (_, _) => ClearAndSearch();

            var footer = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var button in new[] { _addButton, searchButton, clearButton })
            {
                button.Margin = new Thickness(0, 0, 8, 0);
                footer.Children.Add(button);
            }

            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            host.Children.Add(_message);
            host.Children.Add(_rows);
            host.Children.Add(footer);
            Content = host;

            // Handled events too: a date picker handles Enter to read the date typed into it, and the query should
            // still run, after the picker has read the date.
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        /// <summary>Raised when the user asks to run the query and every required row has a value.</summary>
        public event EventHandler? SearchRequested;

        /// <summary>Raised when a row is added or removed, or what a row holds changes.</summary>
        public event EventHandler? CriteriaChanged;

        /// <summary>Gets the criteria the panel edits.</summary>
        public QueryCriteria Criteria => _criteria;

        /// <summary>Gets the fields "add condition" offers.</summary>
        public IReadOnlyList<CriteriaField> Fields => _fields;

        /// <summary>
        /// Gets or sets how today is read for the date periods the date rows show; defaults to the machine's date.
        /// </summary>
        public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

        /// <summary>Gets or sets a value indicating whether each row stacks its parts for a narrow viewport.</summary>
        public bool IsCompact
        {
            get => _isCompact;
            set
            {
                if (_isCompact == value) return;
                _isCompact = value;
                RebuildRows();
            }
        }

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

        /// <summary>Adds an optional row for <paramref name="field"/> and shows it.</summary>
        /// <param name="field">The field.</param>
        /// <returns>The new row.</returns>
        internal CriteriaRow AddRow(CriteriaField field)
        {
            var row = _criteria.Add(field);
            _rows.Children.Add(BuildRow(row));
            OnChanged();
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
            foreach (var row in _criteria.Rows)
                _rows.Children.Add(BuildRow(row));
        }

        private Control BuildRow(CriteriaRow row)
        {
            var panel = new StackPanel
            {
                Orientation = _isCompact ? Orientation.Vertical : Orientation.Horizontal,
                Spacing = 8,
            };
            panel.Children.Add(new TextBlock
            {
                Text = row.Kind == CriteriaRowKind.Required ? row.Field.Caption + " *" : row.Field.Caption,
                MinWidth = 120,
                VerticalAlignment = VerticalAlignment.Center,
            });

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
                panel.Children.Add(operatorBox);
            }
            panel.Children.Add(valueHost);

            if (row.Kind != CriteriaRowKind.Required)
            {
                var remove = new Button { Content = "✕", VerticalAlignment = VerticalAlignment.Center };
                AutomationNameOf(remove, UIText.Get(PolhemUIText.RemoveCondition));
                remove.Click += (_, _) =>
                {
                    if (!_criteria.Remove(row)) return;
                    _rows.Children.Remove(panel);
                    OnChanged();
                };
                panel.Children.Add(remove);
            }
            return panel;
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
