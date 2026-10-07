using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    public partial class PropertyGridControl
    {
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
                    AddCategoryHeader(component.GetType(), group.Key, members);
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

        private void AddCategoryHeader(Type componentType, string category, List<Control> members)
        {
            var text = PropertyGridMetadata.Translate(LabelTranslator, PropertyGridTextKind.Category, componentType, null, category);
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
            var suggestions = PropertyGridMetadata.GetSuggestions(property, component, ValueSuggestionProvider);
            var kind = suggestions is null ? PropertyGridMetadata.GetEditorKind(property) : PropertyGridEditorKind.Suggestion;
            var isReadOnly = IsReadOnly || property.IsReadOnly;
            var label = new TextBlock
            {
                Text = PropertyGridMetadata.Translate(LabelTranslator, PropertyGridTextKind.DisplayName, component.GetType(),
                    property.Name, property.DisplayName),
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

            var editor = CreateEditor(kind, property, isReadOnly, suggestions);
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

        private sealed record CategoryTag(string Text);
    }
}
