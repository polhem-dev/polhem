using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Shows the properties of an object as a two-column list of labels and editors, driven by the
    /// <see cref="System.ComponentModel"/> annotations on its type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The properties come from <see cref="TypeDescriptor.GetProperties(object, Attribute[])"/>, so
    /// <c>[Browsable(false)]</c> hides a property, <c>[Category]</c> groups it, <c>[Description]</c> fills the
    /// description bar, <c>[DisplayName]</c> names it and <c>[TypeConverter]</c> converts its text. Each property gets
    /// an editor by its type: a check box for <see cref="bool"/>, a drop-down for an enum or a converter's exclusive
    /// standard values, a numeric up-down for an integral or <see cref="decimal"/> type, a date picker for
    /// <see cref="DateTime"/> and <see cref="DateOnly"/>, and a text box for anything its converter reads from text.
    /// A collection shows its item count and a button that opens it in <see cref="CollectionEditDialog"/>, or in
    /// whatever <see cref="CollectionEditorProvider"/> supplies. Any other value, such as a nested object, shows its
    /// text and cannot be edited here.
    /// </para>
    /// <para>
    /// A value that differs from its <c>[DefaultValue]</c> is shown in bold, and the label's context menu resets it.
    /// A text box writes its value when it loses focus or on Enter; the other editors write on each change. Text the
    /// converter or the setter refuses leaves the property unchanged and marks the editor with the reason. After each
    /// write every row is reloaded, since one setter may change other properties.
    /// </para>
    /// <para>
    /// This control reads types through <see cref="TypeDescriptor"/>, which trimming does not preserve. It is meant
    /// for desktop tools and is not supported in a trimmed mobile head.
    /// </para>
    /// </remarks>
    public partial class PropertyGridControl : ContentControl
    {
        /// <summary>
        /// Defines the <see cref="SelectedObject"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> SelectedObjectProperty =
            AvaloniaProperty.Register<PropertyGridControl, object?>(nameof(SelectedObject));

        /// <summary>
        /// Defines the <see cref="ShowDescription"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowDescriptionProperty =
            AvaloniaProperty.Register<PropertyGridControl, bool>(nameof(ShowDescription), defaultValue: true);

        /// <summary>
        /// Defines the <see cref="ShowCategories"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCategoriesProperty =
            AvaloniaProperty.Register<PropertyGridControl, bool>(nameof(ShowCategories), defaultValue: true);

        /// <summary>
        /// Defines the <see cref="IsReadOnly"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<PropertyGridControl, bool>(nameof(IsReadOnly));

        /// <summary>
        /// Defines the <see cref="LabelTranslator"/> property.
        /// </summary>
        public static readonly StyledProperty<Func<string, string>?> LabelTranslatorProperty =
            AvaloniaProperty.Register<PropertyGridControl, Func<string, string>?>(nameof(LabelTranslator));

        /// <summary>
        /// Defines the <see cref="CollectionEditorProvider"/> property.
        /// </summary>
        public static readonly StyledProperty<Func<CollectionEditContext, Task<bool>?>?> CollectionEditorProviderProperty =
            AvaloniaProperty.Register<PropertyGridControl, Func<CollectionEditContext, Task<bool>?>?>(nameof(CollectionEditorProvider));

        private const double EditorRowHeight = 36;

        private readonly Grid _rowsHost;
        private readonly Border _descriptionBar;
        private readonly TextBlock _descriptionTitle;
        private readonly TextBlock _descriptionText;
        private readonly List<PropertyGridRow> _rows = [];
        private PropertyGridRow? _selectedRow;
        private bool _isLoading;

        /// <summary>
        /// Initializes a new instance of <see cref="PropertyGridControl"/>.
        /// </summary>
        public PropertyGridControl()
        {
            _rowsHost = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            };
            _descriptionTitle = new TextBlock { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            _descriptionText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            BindResource(_descriptionText, TextBlock.ForegroundProperty, "SemiColorText2", null);
            _descriptionBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 6),
                Height = 72,
                Child = new StackPanel { Children = { _descriptionTitle, _descriptionText } },
            };
            BindResource(_descriptionBar, Border.BorderBrushProperty, "SemiColorBorder", Brushes.LightGray);
            BindResource(_descriptionBar, Border.BackgroundProperty, "SemiColorBackground1", Brushes.Transparent);

            var host = new DockPanel();
            DockPanel.SetDock(_descriptionBar, Dock.Bottom);
            host.Children.Add(_descriptionBar);
            host.Children.Add(new ScrollViewer
            {
                Content = _rowsHost,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });
            Content = host;
        }

        /// <summary>
        /// Gets or sets the object whose properties are shown, or <c>null</c> to show nothing.
        /// </summary>
        public object? SelectedObject
        {
            get { return GetValue(SelectedObjectProperty); }
            set { SetValue(SelectedObjectProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether the bar under the list shows the selected property's name and description. The
        /// default is <c>true</c>.
        /// </summary>
        public bool ShowDescription
        {
            get { return GetValue(ShowDescriptionProperty); }
            set { SetValue(ShowDescriptionProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether the properties are grouped under their categories. The default is <c>true</c>; when
        /// <c>false</c> they are listed without group headers.
        /// </summary>
        public bool ShowCategories
        {
            get { return GetValue(ShowCategoriesProperty); }
            set { SetValue(ShowCategoriesProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether every editor refuses edits. A property without a setter is read-only either way.
        /// </summary>
        public bool IsReadOnly
        {
            get { return GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        /// <summary>
        /// Gets or sets the translator applied to category names, property display names and descriptions;
        /// <c>null</c> shows them as written.
        /// </summary>
        /// <remarks>
        /// The translator receives the text as written in the annotations, and an empty result keeps that text. After
        /// the language changes, call <see cref="Refresh"/>: the same translator usually stays set, so nothing else
        /// tells the control to translate again.
        /// </remarks>
        public Func<string, string>? LabelTranslator
        {
            get { return GetValue(LabelTranslatorProperty); }
            set { SetValue(LabelTranslatorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the function the control asks first when the user opens a collection property; <c>null</c>
        /// always uses <see cref="CollectionEditDialog"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The function returns <c>null</c> to leave the collection to <see cref="CollectionEditDialog"/>, or a task
        /// that edits the collection in its own way and completes with whether it changed it. When the collection
        /// changed, the control reloads its rows and raises <see cref="PropertyValueChanged"/> with the collection as
        /// both the old and the new value.
        /// </para>
        /// <para>
        /// The grid inside <see cref="CollectionEditDialog"/> uses the same function for collections of the items.
        /// While a function is set, the button that opens a collection is enabled for any collection, since the
        /// function may edit one the dialog cannot.
        /// </para>
        /// </remarks>
        public Func<CollectionEditContext, Task<bool>?>? CollectionEditorProvider
        {
            get { return GetValue(CollectionEditorProviderProperty); }
            set { SetValue(CollectionEditorProviderProperty, value); }
        }

        /// <summary>
        /// Occurs after the control has written a new value to a property of <see cref="SelectedObject"/>.
        /// </summary>
        public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

        /// <summary>
        /// Rebuilds the list from <see cref="SelectedObject"/>: reads every value again and translates every label
        /// again through <see cref="LabelTranslator"/>. The selected property stays selected.
        /// </summary>
        /// <remarks>
        /// Call it after the object changed outside the control, or after the language changed.
        /// </remarks>
        public void Refresh()
        {
            var selectedName = _selectedRow?.Property.Name;
            Rebuild();
            if (selectedName is not null && _rows.Find(r => r.Property.Name == selectedName) is { } row)
                SelectRow(row);
        }

        // NOTE: Without this override the control looks up a theme for its own type, which no theme ships, and it
        // would render nothing (maintainers/gotchas/avalonia-controls.md #4).
        /// <inheritdoc/>
        protected override Type StyleKeyOverride => typeof(ContentControl);

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedObjectProperty)
            {
                Rebuild();
            }
            else if (change.Property == ShowCategoriesProperty || change.Property == IsReadOnlyProperty
                || change.Property == LabelTranslatorProperty || change.Property == CollectionEditorProviderProperty)
            {
                Refresh();
            }
            else if (change.Property == ShowDescriptionProperty)
            {
                _descriptionBar.IsVisible = ShowDescription;
            }
        }

        /// <summary>Gets the rows currently shown, in display order.</summary>
        internal IReadOnlyList<PropertyGridRow> Rows => _rows;

        /// <summary>Gets the row whose property the description bar shows.</summary>
        internal PropertyGridRow? SelectedRow => _selectedRow;

        /// <summary>Gets the text of the description bar's title.</summary>
        internal string? DescriptionTitle => _descriptionTitle.Text;

        /// <summary>Gets the text of the description bar.</summary>
        internal string? DescriptionText => _descriptionText.Text;

        /// <summary>Gets the category headers currently shown, in display order.</summary>
        internal IReadOnlyList<string> CategoryHeaders =>
            _rowsHost.Children.OfType<Border>().Where(b => b.Tag is CategoryTag).Select(b => ((CategoryTag)b.Tag!).Text).ToList();

        /// <summary>
        /// Makes <paramref name="row"/> the selected row and shows its description.
        /// </summary>
        internal void SelectRow(PropertyGridRow row)
        {
            if (_selectedRow is { } previous)
            {
                previous.LabelHost.ClearValue(Border.BackgroundProperty);
                previous.LabelHost.Background = Brushes.Transparent;
            }
            _selectedRow = row;
            BindResource(row.LabelHost, Border.BackgroundProperty, "SemiColorFill1", Brushes.LightSteelBlue);
            var translator = LabelTranslator;
            _descriptionTitle.Text = PropertyGridMetadata.Translate(translator, row.Property.DisplayName);
            _descriptionText.Text = PropertyGridMetadata.Translate(translator, row.Property.Description);
        }

        /// <summary>
        /// Puts the property of <paramref name="row"/> back to its default value.
        /// </summary>
        internal void ResetRow(PropertyGridRow row)
        {
            if (!PropertyGridMetadata.CanReset(row.Property, row.Component, IsReadOnly)) { return; }
            var oldValue = row.Property.GetValue(row.Component);
            if (PropertyGridMetadata.TryResetValue(row.Property, row.Component, out var error))
            {
                OnValueWritten(row, oldValue);
                return;
            }
            ShowError(row, error);
        }

        private void Rebuild()
        {
            _rows.Clear();
            _selectedRow = null;
            _rowsHost.Children.Clear();
            _rowsHost.RowDefinitions.Clear();
            _descriptionTitle.Text = null;
            _descriptionText.Text = null;
            if (SelectedObject is not { } component) { return; }

            var properties = PropertyGridMetadata.GetProperties(component);
            if (ShowCategories)
            {
                foreach (var group in PropertyGridMetadata.GroupByCategory(properties))
                {
                    var members = new List<Control>();
                    AddCategoryHeader(group.Key, members);
                    foreach (var property in group)
                        members.AddRange(AddRow(component, property));
                }
            }
            else
            {
                foreach (var property in properties)
                    AddRow(component, property);
            }
            LoadAll();
        }

        private void AddCategoryHeader(string category, List<Control> members)
        {
            var text = PropertyGridMetadata.Translate(LabelTranslator, category);
            var glyph = new Run("▾ ");
            var header = new Border
            {
                Padding = new Thickness(6, 4),
                Tag = new CategoryTag(text),
                Child = new TextBlock { FontWeight = FontWeight.SemiBold, Inlines = [glyph, new Run(text)] },
            };
            // The header needs a background to take clicks on its blank area (gotcha #2).
            BindResource(header, Border.BackgroundProperty, "SemiColorBackground2", Brushes.Gainsboro);
            header.PointerPressed += (_, e) =>
            {
                var expand = members.Count > 0 && !members[0].IsVisible;
                foreach (var member in members)
                    member.IsVisible = expand;
                glyph.Text = expand ? "▾ " : "▸ ";
                e.Handled = true;
            };
            Grid.SetColumnSpan(header, 2);
            PlaceInNewRow(header);
        }

        private Control[] AddRow(object component, PropertyDescriptor property)
        {
            var kind = PropertyGridMetadata.GetEditorKind(property);
            var isReadOnly = IsReadOnly || property.IsReadOnly;
            var label = new TextBlock
            {
                Text = PropertyGridMetadata.Translate(LabelTranslator, property.DisplayName),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var labelHost = new Border
            {
                Padding = new Thickness(16, 2, 6, 2),
                BorderThickness = new Thickness(0, 0, 1, 1),
                // A transparent background lets the whole cell take the click that selects the row (gotcha #2).
                Background = Brushes.Transparent,
                Child = label,
            };
            BindResource(labelHost, Border.BorderBrushProperty, "SemiColorBorder", Brushes.LightGray);

            var editor = CreateEditor(kind, property, isReadOnly);
            var editorHost = new Border
            {
                Padding = new Thickness(2),
                // Keeps the rows of check boxes and summaries as tall as the rows of text boxes.
                MinHeight = EditorRowHeight,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = editor,
            };
            BindResource(editorHost, Border.BorderBrushProperty, "SemiColorBorder", Brushes.LightGray);
            Grid.SetColumn(editorHost, 1);

            var row = new PropertyGridRow(component, property, kind, isReadOnly, labelHost, label, editor);
            _rows.Add(row);
            AttachEditor(row);
            labelHost.ContextMenu = CreateRowMenu(row);
            labelHost.PointerPressed += (_, _) => SelectRow(row);
            editor.GotFocus += (_, _) => SelectRow(row);

            PlaceInNewRow(labelHost);
            _rowsHost.Children.Add(editorHost);
            Grid.SetRow(editorHost, Grid.GetRow(labelHost));
            return [labelHost, editorHost];
        }

        private void PlaceInNewRow(Control control)
        {
            _rowsHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(control, _rowsHost.RowDefinitions.Count - 1);
            _rowsHost.Children.Add(control);
        }

        private ContextMenu CreateRowMenu(PropertyGridRow row)
        {
            var reset = new MenuItem { Header = UIText.Get(PolhemUIText.ResetValue) };
            reset.Click += (_, _) =>
            {
                ResetRow(row);
                // Focus goes back to the editor that had it before the menu opened, which would select that row.
                row.Editor.Focus();
                SelectRow(row);
            };
            var menu = new ContextMenu { ItemsSource = new[] { reset } };
            menu.Opening += (_, _) => reset.IsEnabled = PropertyGridMetadata.CanReset(row.Property, row.Component, IsReadOnly);
            return menu;
        }

        private void LoadAll()
        {
            _isLoading = true;
            try
            {
                foreach (var row in _rows)
                {
                    row.Load();
                    DataValidationErrors.ClearErrors(row.Editor);
                    row.Label.FontWeight = PropertyGridMetadata.IsModified(row.Property, row.Component)
                        ? FontWeight.Bold
                        : FontWeight.Normal;
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Opens the collection of <paramref name="row"/> in <see cref="CollectionEditorProvider"/> or in
        /// <see cref="CollectionEditDialog"/>.
        /// </summary>
        /// <returns><c>true</c> when the collection changed.</returns>
        internal async Task<bool> EditCollectionAsync(PropertyGridRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.Kind != PropertyGridEditorKind.Collection || !CanEditCollection(row.Property.GetValue(row.Component)))
                return false;
            SelectRow(row);
            var context = new CollectionEditContext(row.Component, row.Property);
            var provider = CollectionEditorProvider;
            var edit = provider?.Invoke(context)
                ?? CollectionEditDialog.ShowAsync(this, context, LabelTranslator, provider, CancellationToken.None);
            if (!await edit) { return false; }
            OnValueWritten(row, context.Collection);
            return true;
        }

        /// <summary>
        /// Returns whether the button that opens a collection is enabled for <paramref name="value"/>.
        /// </summary>
        internal bool CanEditCollection(object? value)
        {
            if (IsReadOnly || value is not IList collection) { return false; }
            return CollectionEditorProvider is not null
                || (PropertyGridMetadata.IsResizable(collection)
                    && PropertyGridMetadata.IsEditableItemType(PropertyGridMetadata.GetItemType(collection.GetType())));
        }

        private void OnValueWritten(PropertyGridRow row, object? oldValue)
        {
            var newValue = row.Property.GetValue(row.Component);
            LoadAll();
            PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(row.Component, row.Property, oldValue, newValue));
        }

        private static void ShowError(PropertyGridRow row, string? error)
        {
            DataValidationErrors.SetErrors(row.Editor, [error ?? string.Empty]);
        }

        private static void BindResource(Control control, AvaloniaProperty property, string key, IBrush? fallback)
        {
            control.Bind(property, control.GetResourceObservable(key, value => value as IBrush ?? fallback));
        }

        private sealed record CategoryTag(string Text);
    }
}
