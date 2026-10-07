using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Language;

namespace Polhem.UI.Avalonia.Controls
{
    public partial class PropertyGridControl
    {
        // The mask a password row shows instead of its text.
        private const char PasswordMask = '●';

        /// <summary>
        /// Writes the text in the text box or the editable drop-down of <paramref name="row"/> to its property, unless
        /// it is the text last loaded.
        /// </summary>
        internal void CommitText(PropertyGridRow row)
        {
            var text = row.Kind switch
            {
                PropertyGridEditorKind.Text => ((TextBox)row.Editor).Text ?? string.Empty,
                PropertyGridEditorKind.Suggestion => ((ComboBox)row.Editor).Text ?? string.Empty,
                _ => null,
            };
            if (text is null || row.IsReadOnly) { return; }
            if (string.Equals(text, row.LoadedText, StringComparison.Ordinal)) { return; }
            if (!PropertyGridMetadata.TryParseText(row.Property, text, out var value, out var error))
            {
                ShowError(row, error);
                return;
            }
            WriteValue(row, value, reloadOnError: false);
        }

        // Semi's ComboBox, NumericUpDown and DatePicker do not stretch by default, unlike TextBox (gotcha #3).
        private static Control CreateEditor(PropertyGridEditorKind kind, PropertyDescriptor property, bool isReadOnly,
            IReadOnlyList<string>? suggestions)
        {
            Control editor = kind switch
            {
                PropertyGridEditorKind.Boolean => new CheckBox { IsEnabled = !isReadOnly, VerticalAlignment = VerticalAlignment.Center },
                PropertyGridEditorKind.Choice => new ComboBox
                {
                    ItemsSource = PropertyGridMetadata.GetChoices(property),
                    IsEnabled = !isReadOnly,
                },
                PropertyGridEditorKind.Suggestion => new ComboBox
                {
                    ItemsSource = suggestions,
                    IsEditable = true,
                    IsEnabled = !isReadOnly,
                },
                PropertyGridEditorKind.Numeric => CreateNumericEditor(property.PropertyType, isReadOnly),
                PropertyGridEditorKind.Date => new DatePicker { IsEnabled = !isReadOnly },
                PropertyGridEditorKind.Collection => CreateCollectionEditor(),
                PropertyGridEditorKind.Summary => new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                _ => new TextBox
                {
                    IsReadOnly = isReadOnly,
                    PasswordChar = PropertyGridMetadata.IsPassword(property) ? PasswordMask : default,
                },
            };
            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            return editor;
        }

        private static DockPanel CreateCollectionEditor()
        {
            var button = new Button
            {
                // Three periods rather than the ellipsis character, which the theme's font draws as a short dash.
                Content = "...",
                MinWidth = 32,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
            };
            DockPanel.SetDock(button, Dock.Right);
            var summary = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            return new DockPanel { Children = { button, summary } };
        }

        private static NumericUpDown CreateNumericEditor(Type propertyType, bool isReadOnly)
        {
            var (minimum, maximum, format) = PropertyGridMetadata.GetNumericRange(propertyType);
            var editor = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Increment = 1,
                IsReadOnly = isReadOnly,
                IsEnabled = !isReadOnly,
            };
            if (format is not null)
                editor.FormatString = format;
            return editor;
        }

        // Writes go through PropertyChanged on the value property rather than the controls' own change events,
        // which do not fire for values set from code (gotcha #6). _isLoading keeps a load from writing back.
        private void AttachEditor(PropertyGridRow row)
        {
            var property = row.Property;
            var component = row.Component;
            if (row.Kind == PropertyGridEditorKind.Suggestion)
            {
                AttachSuggestionEditor(row, (ComboBox)row.Editor);
                return;
            }
            switch (row.Editor)
            {
                case CheckBox checkBox:
                    row.Load = () => checkBox.IsChecked = property.GetValue(component) is true;
                    checkBox.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == CheckBox.IsCheckedProperty && !_isLoading)
                            WriteValue(row, checkBox.IsChecked == true, reloadOnError: true);
                    };
                    break;
                case ComboBox comboBox:
                    row.Load = () => comboBox.SelectedItem = property.GetValue(component);
                    comboBox.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == ComboBox.SelectedItemProperty && !_isLoading && comboBox.SelectedItem is { } item)
                            WriteValue(row, item, reloadOnError: true);
                    };
                    break;
                case NumericUpDown numeric:
                    row.Load = () => numeric.Value = property.GetValue(component) is { } number
                        ? Convert.ToDecimal(number, CultureInfo.InvariantCulture)
                        : null;
                    numeric.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != NumericUpDown.ValueProperty || _isLoading) { return; }
                        if (numeric.Value is { } number && PropertyGridMetadata.FromNumber(property.PropertyType, number) is { } value)
                            WriteValue(row, value, reloadOnError: true);
                        else
                            LoadAll();
                    };
                    break;
                case DatePicker datePicker:
                    row.Load = () => datePicker.SelectedDate = PropertyGridMetadata.ToPickerDate(property.GetValue(component));
                    datePicker.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != DatePicker.SelectedDateProperty || _isLoading) { return; }
                        if (datePicker.SelectedDate is { } picked)
                            WriteValue(row, PropertyGridMetadata.FromPickerDate(property.PropertyType, picked, property.GetValue(component)), reloadOnError: true);
                        else
                            LoadAll();
                    };
                    break;
                case TextBox textBox:
                    row.Load = () =>
                    {
                        row.LoadedText = PropertyGridMetadata.FormatText(property, property.GetValue(component));
                        textBox.Text = row.LoadedText;
                    };
                    textBox.LostFocus += (_, _) => CommitText(row);
                    textBox.KeyDown += (_, e) =>
                    {
                        if (e.Key != Key.Enter) { return; }
                        CommitText(row);
                        e.Handled = true;
                    };
                    break;
                case DockPanel { Children: [Button editButton, TextBlock summary] }:
                    row.Load = () =>
                    {
                        var value = property.GetValue(component);
                        summary.Text = string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.CollectionSummary),
                            value is ICollection collection ? collection.Count : 0);
                        editButton.IsEnabled = CanEditCollection(value);
                    };
                    editButton.Click += async (_, _) => await EditCollectionAsync(row);
                    break;
                case TextBlock summary:
                    row.Load = () => summary.Text = PropertyGridMetadata.FormatText(property, property.GetValue(component));
                    break;
            }
        }

        // An editable drop-down writes its text the way a text box does: on Enter, when it loses focus, and when the
        // drop-down closes after a pick. Writing on each SelectedItem change would also write while the user is still
        // typing, since typed text that matches a value selects it.
        private void AttachSuggestionEditor(PropertyGridRow row, ComboBox comboBox)
        {
            row.Load = () =>
            {
                row.LoadedText = PropertyGridMetadata.FormatText(row.Property, row.Property.GetValue(row.Component));
                comboBox.Text = row.LoadedText;
            };
            comboBox.LostFocus += (_, _) => CommitText(row);
            comboBox.DropDownClosed += (_, _) => CommitText(row);
            comboBox.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) { return; }
                CommitText(row);
                e.Handled = true;
            };
        }

        private void WriteValue(PropertyGridRow row, object? value, bool reloadOnError)
        {
            var oldValue = row.Property.GetValue(row.Component);
            if (Equals(oldValue, value)) { return; }
            if (PropertyGridMetadata.TrySetValue(row.Property, row.Component, value, out var error))
            {
                OnValueWritten(row, oldValue);
                return;
            }
            // An editor that cannot hold an invalid entry goes back to the value the property kept; a text box keeps
            // the typed text so it can be corrected.
            if (reloadOnError)
                LoadAll();
            ShowError(row, error);
        }
    }
}
