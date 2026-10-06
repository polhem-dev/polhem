using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Covers what runs without a UI thread. The editors are set from code here, which raises the same
    /// <c>PropertyChanged</c> the control listens to; focus, pointer and context-menu input are left to the app.
    /// </summary>
    public class PropertyGridControlTests
    {
        private static PropertyGridRow Row(PropertyGridControl grid, string name) =>
            grid.Rows.Single(r => r.Property.Name == name);

        [Fact]
        [DisplayName("Setting SelectedObject shows one row per browsable property under its category")]
        public void SelectedObject_Set_BuildsRowsAndHeaders()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample() };

            Assert.Equal(["General", "Layout", "Misc"], grid.CategoryHeaders);
            Assert.DoesNotContain(grid.Rows, r => r.Property.Name == nameof(PropertyGridSample.Hidden));
            Assert.Contains(grid.Rows, r => r.Property.Name == nameof(PropertyGridSample.Count));
        }

        [Fact]
        [DisplayName("With ShowCategories off the rows are listed without group headers")]
        public void ShowCategories_Off_HasNoHeaders()
        {
            var grid = new PropertyGridControl { ShowCategories = false, SelectedObject = new PropertyGridSample() };

            Assert.Empty(grid.CategoryHeaders);
            Assert.NotEmpty(grid.Rows);
        }

        [Fact]
        [DisplayName("Clearing SelectedObject removes every row")]
        public void SelectedObject_Cleared_RemovesRows()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample() };

            grid.SelectedObject = null;

            Assert.Empty(grid.Rows);
        }

        [Fact]
        [DisplayName("Each editor is loaded with the property's current value")]
        public void Rows_LoadCurrentValues()
        {
            var sample = new PropertyGridSample { Name = "n", Enabled = true, Count = 7 };
            sample.Items.Add("a");

            var grid = new PropertyGridControl { SelectedObject = sample };

            Assert.Equal("n", Assert.IsType<TextBox>(Row(grid, nameof(PropertyGridSample.Name)).Editor).Text);
            Assert.True(Assert.IsType<CheckBox>(Row(grid, nameof(PropertyGridSample.Enabled)).Editor).IsChecked);
            Assert.Equal(7m, Assert.IsType<NumericUpDown>(Row(grid, nameof(PropertyGridSample.Count)).Editor).Value);
            Assert.Contains("1", Assert.IsType<TextBlock>(Row(grid, nameof(PropertyGridSample.Items)).Editor).Text);
            Assert.Equal("child", Assert.IsType<TextBlock>(Row(grid, nameof(PropertyGridSample.Child)).Editor).Text);
        }

        [Fact]
        [DisplayName("Ticking a check box writes the property and raises PropertyValueChanged")]
        public void CheckBox_Changed_WritesAndRaises()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };
            PropertyValueChangedEventArgs? raised = null;
            grid.PropertyValueChanged += (_, e) => raised = e;

            Assert.IsType<CheckBox>(Row(grid, nameof(PropertyGridSample.Enabled)).Editor).IsChecked = true;

            Assert.True(sample.Enabled);
            Assert.NotNull(raised);
            Assert.Same(sample, raised.Component);
            Assert.Equal(nameof(PropertyGridSample.Enabled), raised.Property.Name);
            Assert.Equal(false, raised.OldValue);
            Assert.Equal(true, raised.NewValue);
        }

        [Fact]
        [DisplayName("Picking an enum member in the drop-down writes the property")]
        public void ComboBox_Changed_WritesEnum()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };

            Assert.IsType<ComboBox>(Row(grid, nameof(PropertyGridSample.Mode)).Editor).SelectedItem =
                PropertyGridSample.SampleMode.Second;

            Assert.Equal(PropertyGridSample.SampleMode.Second, sample.Mode);
        }

        [Fact]
        [DisplayName("Committing a text box writes the converted text and reloads the rows that depend on it")]
        public void CommitText_ValidText_WritesAndReloads()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };
            var row = Row(grid, nameof(PropertyGridSample.Name));

            ((TextBox)row.Editor).Text = "abc";
            grid.CommitText(row);

            Assert.Equal("abc", sample.Name);
            Assert.Equal("abc!", Assert.IsType<TextBox>(Row(grid, nameof(PropertyGridSample.Computed)).Editor).Text);
        }

        [Fact]
        [DisplayName("Pressing Enter in a text box writes its text")]
        public void TextBox_Enter_Writes()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };
            var textBox = (TextBox)Row(grid, nameof(PropertyGridSample.Name)).Editor;

            textBox.Text = "typed";
            textBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("typed", sample.Name);
        }

        [Fact]
        [DisplayName("Text the setter refuses leaves the property unchanged and marks the editor with an error")]
        public void CommitText_SetterRefuses_MarksError()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };
            var row = Row(grid, nameof(PropertyGridSample.Guarded));

            ((TextBox)row.Editor).Text = "bad";
            grid.CommitText(row);

            Assert.Equal(string.Empty, sample.Guarded);
            Assert.True(DataValidationErrors.GetHasErrors(row.Editor));
            Assert.Equal("bad", ((TextBox)row.Editor).Text);
        }

        [Fact]
        [DisplayName("Text the converter refuses leaves the property unchanged and marks the editor with an error")]
        public void CommitText_ConverterRefuses_MarksError()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };
            var row = Row(grid, nameof(PropertyGridSample.Flags));

            ((TextBox)row.Editor).Text = "Nope";
            grid.CommitText(row);

            Assert.Equal(PropertyGridSample.SampleOptions.None, sample.Flags);
            Assert.True(DataValidationErrors.GetHasErrors(row.Editor));
        }

        [Fact]
        [DisplayName("A property without a setter gets a read-only editor")]
        public void Row_NoSetter_IsReadOnly()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample() };

            Assert.True(Assert.IsType<TextBox>(Row(grid, nameof(PropertyGridSample.Computed)).Editor).IsReadOnly);
        }

        [Fact]
        [DisplayName("IsReadOnly makes every editor refuse edits")]
        public void IsReadOnly_True_LocksEveryEditor()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample(), IsReadOnly = true };

            Assert.All(grid.Rows, r => Assert.True(r.IsReadOnly));
            Assert.True(((TextBox)Row(grid, nameof(PropertyGridSample.Name)).Editor).IsReadOnly);
            Assert.False(Row(grid, nameof(PropertyGridSample.Enabled)).Editor.IsEnabled);
            Assert.False(Row(grid, nameof(PropertyGridSample.Mode)).Editor.IsEnabled);
        }

        [Fact]
        [DisplayName("A value that differs from its DefaultValue shows its label in bold")]
        public void Label_ModifiedValue_IsBold()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample { Name = "changed" } };

            Assert.Equal(FontWeight.Bold, Row(grid, nameof(PropertyGridSample.Name)).Label.FontWeight);
            Assert.Equal(FontWeight.Normal, Row(grid, nameof(PropertyGridSample.Enabled)).Label.FontWeight);
            Assert.Equal(FontWeight.Normal, Row(grid, nameof(PropertyGridSample.NoDefault)).Label.FontWeight);
        }

        [Fact]
        [DisplayName("ResetRow puts the property back to its DefaultValue and clears the bold label")]
        public void ResetRow_RestoresDefault()
        {
            var sample = new PropertyGridSample { Name = "changed" };
            var grid = new PropertyGridControl { SelectedObject = sample };
            var row = Row(grid, nameof(PropertyGridSample.Name));

            grid.ResetRow(row);

            Assert.Equal(string.Empty, sample.Name);
            Assert.Equal(FontWeight.Normal, row.Label.FontWeight);
        }

        [Fact]
        [DisplayName("Selecting a row shows its name and Description in the description bar")]
        public void SelectRow_ShowsDescription()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample() };

            grid.SelectRow(Row(grid, nameof(PropertyGridSample.Name)));

            Assert.Equal("Name", grid.DescriptionTitle);
            Assert.Equal("The sample's name.", grid.DescriptionText);
        }

        [Fact]
        [DisplayName("LabelTranslator translates headers, labels and the description, and Refresh keeps the selected row")]
        public void LabelTranslator_TranslatesAndRefreshKeepsSelection()
        {
            var grid = new PropertyGridControl { SelectedObject = new PropertyGridSample() };
            grid.SelectRow(Row(grid, nameof(PropertyGridSample.Name)));

            grid.LabelTranslator = text => text.ToUpperInvariant();

            Assert.Equal(["GENERAL", "LAYOUT", "MISC"], grid.CategoryHeaders);
            Assert.Equal("NAME", Row(grid, nameof(PropertyGridSample.Name)).Label.Text);
            Assert.Equal(nameof(PropertyGridSample.Name), grid.SelectedRow?.Property.Name);
            Assert.Equal("THE SAMPLE'S NAME.", grid.DescriptionText);
        }

        [Fact]
        [DisplayName("Refresh reads values that changed outside the control")]
        public void Refresh_ReadsOutsideChanges()
        {
            var sample = new PropertyGridSample();
            var grid = new PropertyGridControl { SelectedObject = sample };

            sample.Name = "outside";
            grid.Refresh();

            Assert.Equal("outside", ((TextBox)Row(grid, nameof(PropertyGridSample.Name)).Editor).Text);
        }
    }
}
