using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.DefineEditor.Behaviors;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The right-hand pane of a tree editor. An object that a data template in scope can show (a
/// wrapper view-model, or a root node's hint) is shown through that template; any other object is
/// shown by a <see cref="PropertyGridControl"/>, which builds its rows from the type's annotations.
/// </summary>
/// <remarks>
/// The template path keeps <see cref="DirtyTracking"/>, since its controls bind straight into the
/// define objects. The grid path reports each write itself through
/// <see cref="ObjectTreeDocumentViewModelBase.OnPropertyEdited"/>.
/// </remarks>
public sealed class EditorPane : UserControl
{
    /// <summary>
    /// Defines the <see cref="Context"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> ContextProperty =
        AvaloniaProperty.Register<EditorPane, object?>(nameof(Context));

    /// <summary>
    /// Defines the <see cref="PropertyFilter"/> property.
    /// </summary>
    public static readonly StyledProperty<Func<PropertyDescriptor, bool>?> PropertyFilterProperty =
        AvaloniaProperty.Register<EditorPane, Func<PropertyDescriptor, bool>?>(nameof(PropertyFilter));

    /// <summary>
    /// Defines the <see cref="ValueSuggestionProvider"/> property.
    /// </summary>
    public static readonly StyledProperty<Func<PropertyDescriptor, object, IReadOnlyList<string>?>?> ValueSuggestionProviderProperty =
        AvaloniaProperty.Register<EditorPane, Func<PropertyDescriptor, object, IReadOnlyList<string>?>?>(nameof(ValueSuggestionProvider));

    /// <summary>
    /// Defines the <see cref="CollectionEditorProvider"/> property.
    /// </summary>
    public static readonly StyledProperty<Func<CollectionEditContext, Task<bool>?>?> CollectionEditorProviderProperty =
        AvaloniaProperty.Register<EditorPane, Func<CollectionEditContext, Task<bool>?>?>(nameof(CollectionEditorProvider));

    private readonly ContentControl _templated;
    private readonly Border _templatedHost;
    private ObjectTreeDocumentViewModelBase? _document;
    private readonly PropertyGridControl _grid;

    public EditorPane()
    {
        _templated = new ContentControl();
        DirtyTracking.SetIsEnabled(_templated, true);
        _templatedHost = new Border
        {
            Padding = new Thickness(18, 14),
            Child = new ScrollViewer
            {
                Classes = { "props-pane" },
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _templated,
            },
        };
        _grid = new PropertyGridControl { IsVisible = false, LabelTranslator = PropertyLabels.Translate };
        _grid.PropertyValueChanged += (_, _) => (DataContext as ObjectTreeDocumentViewModelBase)?.OnPropertyEdited();
        Content = new Panel { Children = { _templatedHost, _grid } };
    }

    /// <summary>
    /// Gets or sets the object the pane shows, usually the document's
    /// <see cref="ObjectTreeDocumentViewModelBase.SelectedEditorContext"/>.
    /// </summary>
    public object? Context
    {
        get { return GetValue(ContextProperty); }
        set { SetValue(ContextProperty, value); }
    }

    /// <summary>Gets or sets the grid's <see cref="PropertyGridControl.PropertyFilter"/>.</summary>
    public Func<PropertyDescriptor, bool>? PropertyFilter
    {
        get { return GetValue(PropertyFilterProperty); }
        set { SetValue(PropertyFilterProperty, value); }
    }

    /// <summary>Gets or sets the grid's <see cref="PropertyGridControl.ValueSuggestionProvider"/>.</summary>
    public Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider
    {
        get { return GetValue(ValueSuggestionProviderProperty); }
        set { SetValue(ValueSuggestionProviderProperty, value); }
    }

    /// <summary>Gets or sets the grid's <see cref="PropertyGridControl.CollectionEditorProvider"/>.</summary>
    public Func<CollectionEditContext, Task<bool>?>? CollectionEditorProvider
    {
        get { return GetValue(CollectionEditorProviderProperty); }
        set { SetValue(CollectionEditorProviderProperty, value); }
    }

    /// <summary>Gets the grid that shows objects no template covers.</summary>
    internal PropertyGridControl Grid => _grid;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContextProperty)
            Show(Context);
        else if (change.Property == PropertyFilterProperty)
            _grid.PropertyFilter = PropertyFilter;
        else if (change.Property == ValueSuggestionProviderProperty)
            _grid.ValueSuggestionProvider = ValueSuggestionProvider;
        else if (change.Property == CollectionEditorProviderProperty)
            _grid.CollectionEditorProvider = CollectionEditorProvider;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // The templates in scope are only known once the pane is in the view's logical tree.
        Show(Context);
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null)
            _document.EditorRefreshRequested -= OnEditorRefreshRequested;
        _document = DataContext as ObjectTreeDocumentViewModelBase;
        if (_document is not null)
            _document.EditorRefreshRequested += OnEditorRefreshRequested;
    }

    private void OnEditorRefreshRequested(object? sender, EventArgs e) => _grid.Refresh();

    // The grid reads its texts when it builds its rows, and the translator stays the same across a language switch.
    // The subscription follows the visual tree so a closed document's pane does not stay referenced by the service.
    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Current.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        LocalizationService.Current.CultureChanged -= OnCultureChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo culture) => _grid.Refresh();

    private void Show(object? context)
    {
        var useGrid = context is not null && this.FindDataTemplate(context) is null;
        _grid.SelectedObject = useGrid ? context : null;
        _templated.Content = useGrid ? null : context;
        _grid.IsVisible = useGrid;
        _templatedHost.IsVisible = !useGrid;
    }
}
