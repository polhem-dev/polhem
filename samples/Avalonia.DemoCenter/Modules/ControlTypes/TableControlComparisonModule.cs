using System.Data;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Avalonia.DemoCenter.Modules.ControlTypes
{
    /// <summary>
    /// Native vs inherited comparison for table-level controls (<see cref="IBindTableControl"/>):
    /// <see cref="GridControl"/> beside the native <see cref="DataGrid"/>, exercising layout
    /// binding, ambient (<c>TableName</c>) binding, and the EditForm popup mode.
    /// </summary>
    public sealed class TableControlComparisonModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Control Types";

        /// <inheritdoc/>
        public override string Title => "Native vs derived (BindTableControl)";

        /// <inheritdoc/>
        public override string Description =>
            "Table-level control (IBindTableControl): GridControl side by side with the native DataGrid, showing Layout / Ambient "
            + "binding and the in-cell / EditForm edit modes (the editing strategy is in ADR-021).";

        /// <inheritdoc/>
        public override Control BuildView() => new ViewBuilder().Build();

        private sealed class ViewBuilder
        {
            private readonly FormDataObject _data;
            private readonly StackPanel _host = new() { Spacing = 16, Margin = new Thickness(4) };

            public ViewBuilder()
            {
                _data = BuildDataObject();
            }

            public Control Build()
            {
                AddGridSection();
                AddEditFormSection();

                var root = new ScrollViewer { Content = _host };
                FormScope.SetDataObject(root, _data);
                return root;
            }

            private static FormDataObject BuildDataObject()
            {
                var schema = new FormSchema("Gallery", "Gallery");
                // Master table is unused here but FormDataObject expects a master shape.
                var master = schema.Tables!.Add("Gallery", "Gallery");
                master.Fields!.Add("emp_name", "Name", FieldDbType.String);

                var phones = schema.Tables.Add("Phones", "Phones");
                phones.Fields!.Add("phone", "Phone", FieldDbType.String);
                var phoneType = phones.Fields.Add("type", "Type", FieldDbType.String);
                phoneType.ListItems!.Add("Office", "Office");
                phoneType.ListItems.Add("Mobile", "Mobile");
                phoneType.ListItems.Add("Home", "Home");
                phones.Fields.Add("is_primary", "Primary", FieldDbType.Boolean);
                phones.Fields.Add("valid_from", "Valid From", FieldDbType.Date);
                phones.Fields.Add("bill_month", "Bill Month", FieldDbType.String);

                var data = new FormDataObject(schema);
                data.InitializeNewMaster();
                var phoneTable = data.DataSet.Tables["Phones"]!;
                phoneTable.Rows.Add("02-1234-5678", "Office", true,
                    new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), "2026-06");
                phoneTable.Rows.Add("0912-345-678", "Mobile", false,
                    new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), "2026-07");
                return data;
            }

            private void AddGridSection()
            {
                var phoneTable = _data.DataSet.Tables["Phones"]!;

                var layout = new LayoutGrid("Phones", "Phones");
                layout.Columns!.Add(new LayoutColumn("phone", "Phone (read-only)", ControlType.TextEdit) { ReadOnly = true });
                layout.Columns.Add(new LayoutColumn("type", "Type (required)", ControlType.DropDownEdit) { Required = true });
                layout.Columns.Add(new LayoutColumn("is_primary", "Primary (Check)", ControlType.CheckEdit));
                layout.Columns.Add(new LayoutColumn("valid_from", "Valid From (Date)", ControlType.DateEdit));
                layout.Columns.Add(new LayoutColumn("bill_month", "Bill Month (YearMonth)", ControlType.YearMonthEdit));

                var bound = new GridControl { MinHeight = 120 };
                bound.Bind(_data, layout);

                // Stacked top/bottom (not side-by-side) so each grid spans the full width and
                // shows every column without truncation.
                var section = new StackPanel { Spacing = 8 };
                section.Children.Add(new TextBlock { Text = "GridControl ← DataGrid", FontSize = 15, FontWeight = FontWeight.Bold });
                section.Children.Add(new TextBlock { Text = "Native control (DataGrid)", FontWeight = FontWeight.SemiBold });
                section.Children.Add(BuildNativeGrid(phoneTable));
                section.Children.Add(new TextBlock { Text = "Derived control (GridControl, Layout binding)", FontWeight = FontWeight.SemiBold });
                section.Children.Add(bound);

                _host.Children.Add(Card(section));
            }

            private void AddEditFormSection()
            {
                var detail = new LayoutGrid("Phones", "Phones");
                detail.Columns!.Add(new LayoutColumn("phone", "Phone", ControlType.TextEdit));
                detail.Columns.Add(new LayoutColumn("type", "Type", ControlType.DropDownEdit));
                detail.Columns.Add(new LayoutColumn("is_primary", "Primary", ControlType.CheckEdit));
                detail.Columns.Add(new LayoutColumn("valid_from", "Valid From", ControlType.DateEdit));
                detail.Columns.Add(new LayoutColumn("bill_month", "Bill Month", ControlType.YearMonthEdit));

                var grid = new GridControl { MinHeight = 120, EditMode = GridEditMode.EditForm };
                grid.Bind(_data, detail);

                var section = new StackPanel { Spacing = 8 };
                section.Children.Add(new TextBlock
                {
                    Text = "GridControl EditForm mode (the grid is read-only; double-click a row or press the Edit button to edit it in a dialog)",
                    FontSize = 15,
                    FontWeight = FontWeight.Bold,
                });
                section.Children.Add(grid);

                _host.Children.Add(Card(section));
            }

            private static DataGrid BuildNativeGrid(DataTable table)
            {
                var grid = new DataGrid
                {
                    IsReadOnly = true,
                    AutoGenerateColumns = false,
                    CanUserResizeColumns = true,
                    SelectionMode = DataGridSelectionMode.Single,
                    MinHeight = 120,
                };
                foreach (DataColumn column in table.Columns)
                {
                    var name = column.ColumnName;
                    grid.Columns.Add(new DataGridTemplateColumn
                    {
                        Header = name,
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                        CellTemplate = new FuncDataTemplate<DataRowView>(
                            (row, _) => new TextBlock
                            {
                                Text = row?.Row[name]?.ToString() ?? string.Empty,
                                Margin = new Thickness(8, 4),
                            },
                            supportsRecycling: true),
                    });
                }
                grid.ItemsSource = table.DefaultView;
                return grid;
            }

            private static Border Card(Control child) => new()
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                CornerRadius = new CornerRadius(4),
                Child = child,
            };
        }
    }
}
