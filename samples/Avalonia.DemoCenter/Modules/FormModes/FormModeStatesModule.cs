using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Avalonia.DemoCenter.Modules.FormModes
{
    /// <summary>
    /// Controls × FormMode: the same fields shown in three columns, each pinned to View /
    /// Add / Edit via <see cref="FormScope.SetFormMode"/>. Fields carry different
    /// <c>LayoutField.AllowEditModes</c> so you can see both how each control renders per
    /// mode and how per-field editability is gated — side by side, independent of the toolbar.
    /// </summary>
    public sealed class FormModeStatesModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "FormMode States";

        /// <inheritdoc/>
        public override string Title => "Controls × the three FormModes (with AllowEditModes)";

        /// <inheritdoc/>
        public override string Description =>
            "Three columns pinned to View / Add / Edit, each with the same fields but different AllowEditModes: name=All, code only Add, "
            + "dept only Edit, audit=None. Compare at a glance how each control renders per mode and how editing is gated per field. Not affected by the toolbar FormMode.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var legend = new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                CornerRadius = new CornerRadius(4),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "AllowEditModes settings", FontWeight = FontWeight.Bold },
                        Hint("name — All (editable in Add and Edit)"),
                        Hint("code — Add (editable only in Add, like a key field)"),
                        Hint("dept — Edit (editable only in Edit)"),
                        Hint("audit — None (never editable, like an audit field)"),
                    },
                },
            };

            var columns = new Grid { ColumnSpacing = 12 };
            for (var i = 0; i < 3; i++)
                columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AddColumn(columns, 0, SingleFormMode.View, "View");
            AddColumn(columns, 1, SingleFormMode.Add, "Add");
            AddColumn(columns, 2, SingleFormMode.Edit, "Edit");

            var stack = new StackPanel { Spacing = 16, Margin = new Thickness(4) };
            stack.Children.Add(legend);
            stack.Children.Add(columns);
            return new ScrollViewer { Content = stack };
        }

        private static void AddColumn(Grid host, int column, SingleFormMode mode, string title)
        {
            var data = BuildData();
            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.Bold });
            stack.Children.Add(Cell("name", Bind(new TextEdit(), data, "name", FormEditModes.All)));
            stack.Children.Add(Cell("code", Bind(new TextEdit(), data, "code", FormEditModes.Add)));
            stack.Children.Add(Cell("dept", Bind(new DropDownEdit(), data, "dept", FormEditModes.Edit)));
            stack.Children.Add(Cell("audit", Bind(new TextEdit(), data, "audit", FormEditModes.None)));

            var card = new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                CornerRadius = new CornerRadius(4),
                Child = stack,
            };
            // Pin this column to its mode; descendant editors inherit it and ignore the toolbar.
            FormScope.SetFormMode(card, mode);

            Grid.SetColumn(card, column);
            host.Children.Add(card);
        }

        private static T Bind<T>(T editor, FormDataObject data, string field, FormEditModes modes)
            where T : Control, IFieldEditor
        {
            editor.Bind(data, new LayoutField { FieldName = field, AllowEditModes = modes });
            return editor;
        }

        private static Control Cell(string label, Control editor)
        {
            editor.MinWidth = 0;
            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            return new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 },
                    editor,
                },
            };
        }

        private static TextBlock Hint(string text) =>
            new() { Text = text, FontSize = 12, Opacity = 0.75 };

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Demo", "Demo");
            var master = schema.Tables!.Add("Demo", "Demo");
            master.Fields!.Add("name", "Name", FieldDbType.String);
            master.Fields.Add("code", "Code", FieldDbType.String);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("audit", "Audit", FieldDbType.String);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("name", "Alice Chen");
            data.SetField("code", "EMP-001");
            data.SetField("dept", "IT");
            data.SetField("audit", "system");
            return data;
        }
    }
}
