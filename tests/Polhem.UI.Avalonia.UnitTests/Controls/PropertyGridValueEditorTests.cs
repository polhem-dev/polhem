using System.ComponentModel;
using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// The button <see cref="PropertyGridControl.ValueEditorProvider"/> adds to a text row, and what running its
    /// editor does to the grid.
    /// </summary>
    public class PropertyGridValueEditorTests
    {
        private static readonly string[] s_suggestions = ["Alpha", "Beta"];

        private static PropertyGridRow Row(PropertyGridControl grid, string name) =>
            grid.Rows.Single(r => r.Property.Name == name);

        private static Func<PropertyDescriptor, object, Func<Task<bool>>?> EditorFor(string name, Func<Task<bool>> edit) =>
            (property, _) => property.Name == name ? edit : null;

        [Fact]
        [DisplayName("A text row the provider supplies an editor for keeps its text box and gains an enabled button")]
        public void ValueEditorProvider_TextRow_AddsButtonBesideEditor()
        {
            var grid = new PropertyGridControl
            {
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () => Task.FromResult(false)),
                SelectedObject = new PropertyGridSample(),
            };
            var row = Row(grid, nameof(PropertyGridSample.Name));

            Assert.IsType<TextBox>(row.Editor);
            Assert.NotNull(row.ValueEditorButton);
            Assert.True(row.ValueEditorButton.IsEnabled);
            Assert.Null(Row(grid, nameof(PropertyGridSample.NoDefault)).ValueEditorButton);
        }

        [Fact]
        [DisplayName("A suggestion row keeps its editable drop-down when it also gets a button")]
        public void ValueEditorProvider_SuggestionRow_KeepsDropDown()
        {
            var grid = new PropertyGridControl
            {
                ValueSuggestionProvider = (property, _) => property.Name == nameof(PropertyGridSample.Name) ? s_suggestions : null,
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () => Task.FromResult(false)),
                SelectedObject = new PropertyGridSample(),
            };
            var row = Row(grid, nameof(PropertyGridSample.Name));

            Assert.IsType<ComboBox>(row.Editor);
            Assert.NotNull(row.ValueEditorButton);
        }

        [Fact]
        [DisplayName("The provider is not asked for a password, a check box or a collection row")]
        public void ValueEditorProvider_NonTextRows_AreNotAsked()
        {
            var asked = new List<string>();
            var grid = new PropertyGridControl
            {
                ValueEditorProvider = (property, _) =>
                {
                    asked.Add(property.Name);
                    return () => Task.FromResult(false);
                },
                SelectedObject = new PropertyGridSample(),
            };

            Assert.Contains(nameof(PropertyGridSample.Name), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Secret), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Enabled), asked);
            Assert.DoesNotContain(nameof(PropertyGridSample.Items), asked);
            Assert.Null(Row(grid, nameof(PropertyGridSample.Secret)).ValueEditorButton);
        }

        [Fact]
        [DisplayName("The button is disabled while the grid is read-only, and running it then does nothing")]
        public async Task EditValueAsync_ReadOnly_DoesNotRun()
        {
            var ran = false;
            var grid = new PropertyGridControl
            {
                IsReadOnly = true,
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () =>
                {
                    ran = true;
                    return Task.FromResult(true);
                }),
                SelectedObject = new PropertyGridSample(),
            };
            var row = Row(grid, nameof(PropertyGridSample.Name));

            Assert.False(row.ValueEditorButton!.IsEnabled);
            Assert.False(await grid.EditValueAsync(row));
            Assert.False(ran);
        }

        [Fact]
        [DisplayName("When the editor reports a change, the grid reloads the row and raises PropertyValueChanged with the old and new values")]
        public async Task EditValueAsync_Changed_RaisesAndReloads()
        {
            var sample = new PropertyGridSample { Name = "Alpha" };
            var grid = new PropertyGridControl
            {
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () =>
                {
                    sample.Name = "Gamma";
                    return Task.FromResult(true);
                }),
                SelectedObject = sample,
            };
            PropertyValueChangedEventArgs? raised = null;
            grid.PropertyValueChanged += (_, e) => raised = e;

            var changed = await grid.EditValueAsync(Row(grid, nameof(PropertyGridSample.Name)));

            Assert.True(changed);
            Assert.NotNull(raised);
            Assert.Equal("Alpha", raised.OldValue);
            Assert.Equal("Gamma", raised.NewValue);
            Assert.Equal("Gamma", Assert.IsType<TextBox>(Row(grid, nameof(PropertyGridSample.Name)).Editor).Text);
        }

        [Fact]
        [DisplayName("When the editor reports no change, the grid raises nothing")]
        public async Task EditValueAsync_Unchanged_RaisesNothing()
        {
            var grid = new PropertyGridControl
            {
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () => Task.FromResult(false)),
                SelectedObject = new PropertyGridSample(),
            };
            var raised = false;
            grid.PropertyValueChanged += (_, _) => raised = true;

            Assert.False(await grid.EditValueAsync(Row(grid, nameof(PropertyGridSample.Name))));
            Assert.False(raised);
        }

        [Fact]
        [DisplayName("Text typed into the row is written before the editor runs, so the editor sees it")]
        public async Task EditValueAsync_TypedText_IsCommittedFirst()
        {
            var sample = new PropertyGridSample { Name = "Alpha" };
            string? seen = null;
            var grid = new PropertyGridControl
            {
                ValueEditorProvider = EditorFor(nameof(PropertyGridSample.Name), () =>
                {
                    seen = sample.Name;
                    return Task.FromResult(false);
                }),
                SelectedObject = sample,
            };
            var row = Row(grid, nameof(PropertyGridSample.Name));
            ((TextBox)row.Editor).Text = "Typed";

            await grid.EditValueAsync(row);

            Assert.Equal("Typed", seen);
        }
    }
}
