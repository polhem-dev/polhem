using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Renders the body of a single-record form from a <see cref="FormLayout"/>: the master sections with their
    /// field editors, then the detail grids. It needs nothing but the layout, so a design tool can show what a
    /// layout looks like without data, a schema or a backend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without data, each field editor still takes the layout's <see cref="LayoutFieldBase.ReadOnly"/> and
    /// <see cref="LayoutField.AllowEditModes"/>, and each detail grid shows the toolbar its
    /// <see cref="LayoutGrid.AllowActions"/> allows. The form mode comes from the ambient
    /// <see cref="FormScope.FormModeProperty"/>; schema metadata such as drop-down items needs data and is not shown.
    /// </para>
    /// <para>
    /// The presenter never writes into <see cref="Layout"/>. Changing a property re-renders the form; a layout
    /// changed in place keeps the same reference, so call <see cref="Refresh"/> after editing it.
    /// <see cref="Polhem.UI.Avalonia.Views.FormView"/> renders its record through this control.
    /// </para>
    /// </remarks>
    public class FormLayoutPresenter : UserControl
    {
        /// <summary>Identifies the <see cref="Layout"/> styled property.</summary>
        public static readonly StyledProperty<FormLayout?> LayoutProperty =
            AvaloniaProperty.Register<FormLayoutPresenter, FormLayout?>(nameof(Layout));

        /// <summary>Identifies the <see cref="IsCompact"/> styled property.</summary>
        public static readonly StyledProperty<bool> IsCompactProperty =
            AvaloniaProperty.Register<FormLayoutPresenter, bool>(nameof(IsCompact));

        /// <summary>Identifies the <see cref="DetailEditMode"/> styled property.</summary>
        public static readonly StyledProperty<GridEditMode> DetailEditModeProperty =
            AvaloniaProperty.Register<FormLayoutPresenter, GridEditMode>(nameof(DetailEditMode), GridEditMode.InCell);

        /// <summary>Identifies the <see cref="RoundingContext"/> styled property.</summary>
        public static readonly StyledProperty<RoundingContext?> RoundingContextProperty =
            AvaloniaProperty.Register<FormLayoutPresenter, RoundingContext?>(nameof(RoundingContext));

        private readonly StackPanel _host;
        // Detail grids by table name, repopulated on every render so a host can refresh one grid
        // (realized cells do not track later DataRow writes).
        private readonly Dictionary<string, GridControl> _detailGrids = new(StringComparer.OrdinalIgnoreCase);
        private int _updateDepth;

        /// <summary>
        /// Initializes a new instance of <see cref="FormLayoutPresenter"/>.
        /// </summary>
        public FormLayoutPresenter()
        {
            _host = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
            Content = _host;
        }

        /// <summary>Gets or sets the layout to render; <c>null</c> renders nothing.</summary>
        public FormLayout? Layout
        {
            get => GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to render for a compact (phone-sized) viewport: master fields
        /// in a single column and detail grids in <see cref="GridEditMode.EditForm"/>, whatever the layout says.
        /// </summary>
        public bool IsCompact
        {
            get => GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        /// <summary>
        /// Gets or sets the detail grids' editing model when <see cref="IsCompact"/> is <c>false</c>. Defaults to
        /// <see cref="GridEditMode.InCell"/>.
        /// </summary>
        public GridEditMode DetailEditMode
        {
            get => GetValue(DetailEditModeProperty);
            set => SetValue(DetailEditModeProperty, value);
        }

        /// <summary>
        /// Gets or sets the currency and unit masters and the company that decide the decimals of numeric fields
        /// and grid cells; <c>null</c> uses the framework defaults.
        /// </summary>
        public RoundingContext? RoundingContext
        {
            get => GetValue(RoundingContextProperty);
            set => SetValue(RoundingContextProperty, value);
        }

        /// <summary>
        /// Gets or sets the data object the editors and detail grids bind to, or <c>null</c> to render the layout
        /// alone. Set by <see cref="Polhem.UI.Avalonia.Views.FormView"/> inside <see cref="Update"/>.
        /// </summary>
        internal FormDataObject? DataObject { get; set; }

        /// <summary>Gets the panel that holds the rendered sections and detail grids.</summary>
        internal StackPanel Host => _host;

        /// <summary>
        /// Renders the form again from the current properties. Call it after changing <see cref="Layout"/> in
        /// place.
        /// </summary>
        public void Refresh()
        {
            if (_updateDepth > 0) return;

            _host.Children.Clear();
            _detailGrids.Clear();
            if (Layout is not { } layout) return;

            foreach (var section in layout.Sections ?? Enumerable.Empty<LayoutSection>())
                _host.Children.Add(BuildSection(section, layout));
            foreach (var detail in layout.Details ?? Enumerable.Empty<LayoutGrid>())
                _host.Children.Add(BuildDetailSection(detail));
        }

        /// <summary>
        /// Applies several property changes and renders once, after <paramref name="apply"/> returns.
        /// </summary>
        /// <param name="apply">Sets the properties to change.</param>
        internal void Update(Action<FormLayoutPresenter> apply)
        {
            _updateDepth++;
            try
            {
                apply(this);
            }
            finally
            {
                _updateDepth--;
            }
            Refresh();
        }

        /// <summary>
        /// Gets the detail grid rendered for <paramref name="tableName"/>, or <c>null</c> when the layout has none.
        /// </summary>
        /// <param name="tableName">The detail table name, compared case-insensitively.</param>
        internal GridControl? GetDetailGrid(string tableName)
            => _detailGrids.TryGetValue(tableName, out var grid) ? grid : null;

        /// <inheritdoc />
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == LayoutProperty
                || change.Property == IsCompactProperty
                || change.Property == DetailEditModeProperty
                || change.Property == RoundingContextProperty)
            {
                Refresh();
            }
        }

        private Border BuildSection(LayoutSection section, FormLayout layout)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            if (section.ShowCaption && !string.IsNullOrEmpty(section.Caption))
            {
                stack.Children.Add(new TextBlock { Text = section.Caption, FontWeight = FontWeight.Bold });
            }

            stack.Children.Add(BuildFieldGrid(section, layout));

            return new Border
            {
                Padding = new Thickness(8),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Child = stack,
            };
        }

        private Grid BuildFieldGrid(LayoutSection section, FormLayout layout)
        {
            var columnCount = IsCompact ? 1 : NormalizeColumnCount(layout.ColumnCount);
            var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            for (int i = 0; i < columnCount; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            // Placed the way CSS grid auto-places items (sparse, row by row), which the Blazor presenter gets from the
            // browser: the cursor only moves forward, and a field goes to the first position at or after it whose
            // cells are all free. A field spanning several rows keeps its cells from the fields after it.
            var occupied = new HashSet<(int Row, int Column)>();
            int row = 0, col = 0;
            foreach (var field in EnumerateFields(section))
            {
                var (rowSpan, colSpan) = NormalizeSpans(field);
                colSpan = Math.Min(colSpan, columnCount);
                (row, col) = FindFreePosition(occupied, row, col, rowSpan, colSpan, columnCount);
                Occupy(occupied, row, col, rowSpan, colSpan);

                while (grid.RowDefinitions.Count < row + rowSpan)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var cell = BuildFieldCell(field);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                Grid.SetRowSpan(cell, rowSpan);
                Grid.SetColumnSpan(cell, colSpan);
                grid.Children.Add(cell);

                col += colSpan;
            }

            return grid;
        }

        private StackPanel BuildFieldCell(LayoutField field)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            var caption = new TextBlock { Text = field.Caption };
            // A required field's caption is blue; a read-only field's caption stays the theme
            // default — its cue is the editor's read-only underline (the detail grid, which has no
            // such per-cell visual, parenthesises the header instead). See FieldCaptionStyle.
            // The schema's Required counts as well as the layout's: the server enforces the schema's.
            var required = field.Required || DataObject?.GetFormField(field.FieldName)?.Required == true;
            FieldCaptionStyle.ApplyCaptionForeground(caption, field.ReadOnly, required);
            stack.Children.Add(caption);
            stack.Children.Add(BuildInputControl(field));
            return stack;
        }

        // Dispatches LayoutField.ControlType to the corresponding field editor. The editor takes the layout's
        // metadata first, so it renders correctly with no data; with a data object it then binds, pulling its
        // value and the FormField metadata through FormDataObject.
        internal Control BuildInputControl(LayoutField field)
        {
            var editor = FieldEditorFactory.Create(field.ControlType);
            // Currency/unit-aware cell decimals (Tier 2), set before Bind so the first render uses them.
            if (editor is NumericEdit numeric && RoundingContext is { } rounding)
            {
                numeric.CurrencySettings = rounding.CurrencySettings;
                numeric.UnitSettings = rounding.UnitSettings;
                numeric.DefaultCurrencyCode = rounding.Company?.DefaultCurrency ?? string.Empty;
            }
            ((IFieldEditorBinderHost)editor).Binder.AttachLayout(field);
            if (DataObject is { } dataObject)
                ((IFieldEditor)editor).Bind(dataObject, field);
            else
                ((IFieldEditor)editor).FieldName = field.FieldName;
            return editor;
        }

        private Border BuildDetailSection(LayoutGrid layout)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            if (!string.IsNullOrEmpty(layout.Caption))
            {
                stack.Children.Add(new TextBlock { Text = layout.Caption, FontWeight = FontWeight.Bold });
            }

            // The grid carries its own icon toolbar and edit-form flow; the presenter only supplies the
            // layout, the data object and the editing model (compact forces the edit form).
            var grid = new GridControl
            {
                MinHeight = 120,
                EditMode = IsCompact ? GridEditMode.EditForm : DetailEditMode,
            };
            // Currency/unit-aware cell decimals (Tier 2), set before Bind so the first render uses them.
            if (RoundingContext is { } rounding)
            {
                grid.CurrencySettings = rounding.CurrencySettings;
                grid.UnitSettings = rounding.UnitSettings;
                grid.DefaultCurrencyCode = rounding.Company?.DefaultCurrency ?? string.Empty;
            }
            if (DataObject is { } dataObject)
                grid.Bind(dataObject, layout);
            else
                grid.BindLayout(layout);
            _detailGrids[layout.TableName] = grid;
            stack.Children.Add(grid);

            return new Border
            {
                Padding = new Thickness(8),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Child = stack,
            };
        }

        private static IEnumerable<LayoutField> EnumerateFields(LayoutSection section)
            => section.Fields?.Where(f => f.Visible) ?? Enumerable.Empty<LayoutField>();

        private static int NormalizeColumnCount(int? columnCount)
        {
            var n = columnCount ?? 1;
            return n < 1 ? 1 : n;
        }

        // The first position at or after (row, col), in row order, where a rowSpan × colSpan block fits within the
        // columns and covers no occupied cell. colSpan must not exceed columnCount, so a free row always ends the search.
        private static (int Row, int Column) FindFreePosition(HashSet<(int Row, int Column)> occupied,
            int row, int col, int rowSpan, int colSpan, int columnCount)
        {
            while (col + colSpan > columnCount || !IsFree(occupied, row, col, rowSpan, colSpan))
            {
                col++;
                if (col + colSpan > columnCount)
                {
                    row++;
                    col = 0;
                }
            }
            return (row, col);
        }

        private static void Occupy(HashSet<(int Row, int Column)> occupied, int row, int col, int rowSpan, int colSpan)
        {
            for (int r = row; r < row + rowSpan; r++)
                for (int c = col; c < col + colSpan; c++)
                    occupied.Add((r, c));
        }

        private static bool IsFree(HashSet<(int Row, int Column)> occupied, int row, int col, int rowSpan, int colSpan)
        {
            for (int r = row; r < row + rowSpan; r++)
                for (int c = col; c < col + colSpan; c++)
                    if (occupied.Contains((r, c))) return false;
            return true;
        }

        private static (int rowSpan, int columnSpan) NormalizeSpans(LayoutField field)
        {
            var rowSpan = field.RowSpan < 1 ? 1 : field.RowSpan;
            var colSpan = field.ColumnSpan < 1 ? 1 : field.ColumnSpan;
            return (rowSpan, colSpan);
        }
    }
}
