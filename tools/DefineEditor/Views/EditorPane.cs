using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polhem.DefineEditor.Behaviors;
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

    private readonly ContentControl _templated;
    private readonly Border _templatedHost;
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
        _grid = new PropertyGridControl { IsVisible = false };
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

    /// <summary>Gets the grid that shows objects no template covers.</summary>
    internal PropertyGridControl Grid => _grid;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContextProperty)
            Show(Context);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // The templates in scope are only known once the pane is in the view's logical tree.
        Show(Context);
    }

    private void Show(object? context)
    {
        var useGrid = context is not null && this.FindDataTemplate(context) is null;
        _grid.SelectedObject = useGrid ? context : null;
        _templated.Content = useGrid ? null : context;
        _grid.IsVisible = useGrid;
        _templatedHost.IsVisible = !useGrid;
    }
}
