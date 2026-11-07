using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The values the editors offer for properties that name something the solution already has, and the field
    /// picker behind ListFields and LookupFields.
    /// </summary>
    public sealed class ValuePickerTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-value-pickers-" + Guid.NewGuid().ToString("N"));
        private readonly string _schemaPath;
        private readonly SolutionContext _solution;

        public ValuePickerTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            master.Fields!.Add("currency_id", "Currency", FieldDbType.String);
            master.Fields!.Add("total", "Total", FieldDbType.Decimal);
            var detail = schema.Tables!.Add("OrderDetail", "Order Details");
            detail.Fields!.Add("product_rowid", "Product", FieldDbType.Guid);
            detail.Fields!.Add("quantity", "Quantity", FieldDbType.Decimal);
            _schemaPath = Save(schema, "Order.FormSchema.xml");

            var models = new PermissionModels();
            models.Models!.Add(new PermissionModel("Order", "Orders"));
            models.Models!.Add(new PermissionModel("Simple", "Simple"));
            Save(models, "PermissionModels.xml");

            var programs = new ProgramSettings();
            programs.Items!.Add(new ProgramItem("Order", "Orders"));
            programs.Items!.Add(new ProgramItem("Report", "Reports"));
            Save(programs, "ProgramSettings.xml");

            _solution = new SolutionContext(["Order"])
            {
                FormSchemaPaths = new Dictionary<string, string> { ["Order"] = _schemaPath },
                DefinePath = _directory,
            };
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private string Save<T>(T value, string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            XmlCodec.SerializeToFile(value!, path);
            return path;
        }

        private static PropertyDescriptor Property(object component, string name) =>
            TypeDescriptor.GetProperties(component)[name]!;

        private static IReadOnlyList<string>? Suggest(ObjectTreeDocumentViewModelBase document, object component, string name) =>
            document.ValueSuggestionProvider!(Property(component, name), component);

        [Fact]
        [DisplayName("A FormSchema offers the database categories, company first, for its CategoryId")]
        public void FormSchema_CategoryId_OffersCategories()
        {
            var document = FormSchemaDocumentViewModel.Load(_schemaPath, _solution);

            Assert.Equal(["company", "common", "log"], Suggest(document, document.Schema, nameof(FormSchema.CategoryId)));
        }

        [Fact]
        [DisplayName("A FormSchema offers the solution's permission models for its PermissionModelId")]
        public void FormSchema_PermissionModelId_OffersModels()
        {
            var document = FormSchemaDocumentViewModel.Load(_schemaPath, _solution);

            Assert.Equal(["Order", "Simple"], Suggest(document, document.Schema, nameof(FormSchema.PermissionModelId)));
        }

        [Fact]
        [DisplayName("Without PermissionModels.xml a PermissionModelId stays a plain text value")]
        public void FormSchema_PermissionModelId_NoFile_OffersNothing()
        {
            var document = FormSchemaDocumentViewModel.Load(_schemaPath, _solution with { DefinePath = string.Empty });

            Assert.Null(Suggest(document, document.Schema, nameof(FormSchema.PermissionModelId)));
        }

        [Fact]
        [DisplayName("A form's CurrencyField offers the master fields and a field's UnitField its own table's fields")]
        public void CurrencyAndUnitFields_OfferTableFields()
        {
            var document = FormSchemaDocumentViewModel.Load(_schemaPath, _solution);
            var quantity = document.Schema.Tables!["OrderDetail"].Fields!["quantity"];

            Assert.Equal(["sys_id", "currency_id", "total"], Suggest(document, document.Schema, nameof(FormSchema.CurrencyField)));
            Assert.Equal(["product_rowid", "quantity"], Suggest(document, quantity, nameof(FormField.UnitField)));
        }

        [Fact]
        [DisplayName("ListFields and LookupFields get the field picker, and other text properties of the form do not")]
        public void FormSchema_FieldLists_GetFieldPicker()
        {
            var document = FormSchemaDocumentViewModel.Load(_schemaPath, _solution);
            var schema = document.Schema;

            Assert.NotNull(document.ValueEditorProvider!(Property(schema, nameof(FormSchema.ListFields)), schema));
            Assert.NotNull(document.ValueEditorProvider!(Property(schema, nameof(FormSchema.LookupFields)), schema));
            Assert.Null(document.ValueEditorProvider!(Property(schema, nameof(FormSchema.DisplayName)), schema));
        }

        [Fact]
        [DisplayName("A FormLayout offers master fields for a section field, detail tables for a grid and the grid table's fields for a column")]
        public void FormLayout_OffersFieldsAndTablesFromItsForm()
        {
            var layout = new FormLayout { LayoutId = "Order", ProgId = "Order", Caption = "Order" };
            var section = new LayoutSection { Name = "Main", Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Order No" });
            layout.Sections!.Add(section);
            var grid = new LayoutGrid("OrderDetail", "Order Details");
            grid.Columns!.Add(new LayoutColumn("quantity", "Quantity", ControlType.TextEdit));
            layout.Details!.Add(grid);
            var document = FormLayoutDocumentViewModel.Load(Save(layout, "Order.FormLayout.xml"), _solution);
            var gridNode = document.RootNode.Children.First(c => c.IsFolder && c.Value is LayoutGridCollection).Children[0];
            document.SelectedTreeNode = gridNode.Children[0];
            var loaded = document.Root;

            Assert.Equal(["Order"], Suggest(document, loaded, nameof(FormLayout.ProgId)));
            Assert.Equal(["sys_id", "currency_id", "total"], Suggest(document, loaded.Sections![0].Fields![0], nameof(LayoutField.FieldName)));
            Assert.Equal(["OrderDetail"], Suggest(document, loaded.Details![0], nameof(LayoutGrid.TableName)));
            Assert.Equal(["product_rowid", "quantity"], Suggest(document, loaded.Details![0].Columns![0], nameof(LayoutColumn.FieldName)));
        }

        [Fact]
        [DisplayName("An index field offers the fields of its own table schema")]
        public void TableSchema_IndexField_OffersTableFields()
        {
            var schema = new TableSchema { TableName = "ft_order", DisplayName = "Order" };
            schema.Fields!.Add(new DbField("sys_id", "Order No", FieldDbType.String));
            schema.Fields!.Add(new DbField("order_date", "Order Date", FieldDbType.Date));
            var index = new DbTableIndex { Name = "PK_ft_order", PrimaryKey = true };
            index.IndexFields!.Add(new IndexField("sys_id", SortDirection.Asc));
            schema.Indexes!.Add(index);
            var document = TableSchemaDocumentViewModel.Load(Save(schema, "ft_order.TableSchema.xml"));

            Assert.Equal(["sys_id", "order_date"], Suggest(document, document.Root.Indexes![0].IndexFields![0], nameof(IndexField.FieldName)));
        }

        [Fact]
        [DisplayName("A menu entry offers the programs registered in ProgramSettings for its ProgId")]
        public void MenuSettings_Entry_OffersRegisteredPrograms()
        {
            var menu = new MenuSettings();
            menu.Items!.Add(new MenuEntry("orders", "Order", "Orders"));
            var document = MenuSettingsDocumentViewModel.Load(Save(menu, "MenuSettings.xml"), _solution);

            Assert.Equal(["Order", "Report"], Suggest(document, document.Root.Items![0], nameof(MenuEntry.ProgId)));
        }

        [Fact]
        [DisplayName("The field picker lists the named fields first in their order, then the rest, and keeps an unknown name")]
        public void FieldPicker_OrdersNamedFieldsFirstAndKeepsUnknown()
        {
            var picker = new FieldPickerViewModel("ListFields",
                [("sys_id", "Order No"), ("order_date", "Order Date"), ("total", "Total")], "TOTAL, missing,sys_id");

            Assert.Equal(["total", "missing", "sys_id", "order_date"], picker.Items.Select(i => i.Name));
            Assert.Equal([true, true, true, false], picker.Items.Select(i => i.IsChecked));
            Assert.Equal("total,missing,sys_id", picker.Result);
        }

        [Fact]
        [DisplayName("Ticking and moving fields in the picker changes the result to the ticked names in list order")]
        public void FieldPicker_TickAndMove_ChangesResult()
        {
            var picker = new FieldPickerViewModel("ListFields",
                [("sys_id", "Order No"), ("order_date", "Order Date"), ("total", "Total")], "sys_id");
            picker.Items[2].IsChecked = true;
            picker.SelectedItem = picker.Items[2];

            Assert.False(picker.MoveDownCommand.CanExecute(null));
            picker.MoveUpCommand.Execute(null);
            picker.MoveUpCommand.Execute(null);

            Assert.Equal("total,sys_id", picker.Result);
            Assert.False(picker.MoveUpCommand.CanExecute(null));
            Assert.Same(picker.Items[0], picker.SelectedItem);
        }
    }
}
