using System.ComponentModel;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Tests for <see cref="FormLayoutPresenter"/> rendering a <see cref="FormLayout"/> with no data object, which is
    /// how a design tool previews a layout. The bound path is covered through <see cref="Views.FormView"/> in
    /// <see cref="FormViewTests"/>.
    /// </summary>
    public class FormLayoutPresenterTests
    {
        private static FormLayout BuildLayout()
        {
            var layout = new FormLayout { LayoutId = "Order", ProgId = "Order", ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main", Caption = "Order" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Order No" });
            section.Fields.Add(new LayoutField { FieldName = "order_date", Caption = "Order Date", ControlType = ControlType.DateEdit });
            section.Fields.Add(new LayoutField { FieldName = "customer_rowid", Caption = "Customer", ControlType = ControlType.ButtonEdit });
            section.Fields.Add(new LayoutField { FieldName = "status", Caption = "Status", ControlType = ControlType.DropDownEdit });
            section.Fields.Add(new LayoutField { FieldName = "is_closed", Caption = "Closed", ControlType = ControlType.CheckEdit });
            section.Fields.Add(new LayoutField
            {
                FieldName = "total_amount",
                Caption = "Total Amount",
                ControlType = ControlType.NumericEdit,
                ReadOnly = true,
            });
            layout.Sections!.Add(section);

            var grid = new LayoutGrid("OrderDetail", "Order Details");
            grid.Columns!.Add(new LayoutColumn { FieldName = "product_rowid", Caption = "Product" });
            grid.Columns.Add(new LayoutColumn { FieldName = "quantity", Caption = "Quantity", ControlType = ControlType.NumericEdit });
            layout.Details!.Add(grid);
            return layout;
        }

        private static FormLayoutPresenter Render(FormLayout layout, SingleFormMode formMode = SingleFormMode.Edit)
        {
            var presenter = new FormLayoutPresenter();
            FormScope.SetFormMode(presenter, formMode);
            presenter.Layout = layout;
            return presenter;
        }

        private static List<T> FindAll<T>(Control root) where T : Control
        {
            var found = new List<T>();
            Collect(root, found);
            return found;
        }

        private static void Collect<T>(Control root, List<T> found) where T : Control
        {
            if (root is T match)
            {
                found.Add(match);
                return;
            }
            switch (root)
            {
                case Panel panel:
                    foreach (var child in panel.Children)
                        Collect(child, found);
                    break;
                case Border { Child: { } borderChild }:
                    Collect(borderChild, found);
                    break;
                case ContentControl { Content: Control content }:
                    Collect(content, found);
                    break;
            }
        }

        private static Grid FieldGrid(FormLayoutPresenter presenter)
        {
            var section = Assert.IsType<Border>(presenter.Host.Children[0]);
            var stack = Assert.IsType<StackPanel>(section.Child);
            return Assert.IsType<Grid>(stack.Children[^1]);
        }

        private static StackPanel GridToolbar(GridControl grid)
            => Assert.IsType<StackPanel>(Assert.IsType<DockPanel>(grid.Content).Children[0]);

        private static T EditorFor<T>(FormLayoutPresenter presenter, string fieldName) where T : Control
            => Assert.Single(FindAll<T>(presenter), e => ((IFieldEditor)e).FieldName == fieldName);

        [Fact]
        [DisplayName("Without a data object, the presenter renders every section and every detail grid of the layout")]
        public void Render_NoDataObject_RendersSectionsAndDetails()
        {
            var presenter = Render(BuildLayout());

            Assert.Equal(2, presenter.Host.Children.Count);
            Assert.All(presenter.Host.Children, child => Assert.IsType<Border>(child));
            Assert.Single(FindAll<GridControl>(presenter));
        }

        [Fact]
        [DisplayName("Without a data object, each field gets the editor its ControlType names and carries its FieldName")]
        public void Render_NoDataObject_CreatesEditorPerControlType()
        {
            var presenter = Render(BuildLayout());

            EditorFor<DateEdit>(presenter, "order_date");
            EditorFor<ButtonEdit>(presenter, "customer_rowid");
            EditorFor<DropDownEdit>(presenter, "status");
            EditorFor<CheckEdit>(presenter, "is_closed");
            EditorFor<NumericEdit>(presenter, "total_amount");
            Assert.Contains(FindAll<TextEdit>(presenter), e => e.FieldName == "sys_id");
        }

        [Fact]
        [DisplayName("Without a data object, a field the layout marks ReadOnly is read-only in Edit mode")]
        public void Render_NoDataObject_ReadOnlyFieldIsReadOnlyInEditMode()
        {
            var presenter = Render(BuildLayout(), SingleFormMode.Edit);

            Assert.True(EditorFor<NumericEdit>(presenter, "total_amount").IsReadOnly);
            Assert.False(Assert.Single(FindAll<TextEdit>(presenter), e => e.FieldName == "sys_id").IsReadOnly);
        }

        [Fact]
        [DisplayName("Without a data object, AllowEditModes decides whether a field is editable in the current form mode")]
        public void Render_NoDataObject_AllowEditModesFollowsFormMode()
        {
            var layout = new FormLayout { ColumnCount = 1 };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", AllowEditModes = FormEditModes.Add });
            layout.Sections!.Add(section);

            var editing = Render(layout, SingleFormMode.Edit);
            var adding = Render(layout, SingleFormMode.Add);

            Assert.True(Assert.Single(FindAll<TextEdit>(editing)).IsReadOnly);
            Assert.False(Assert.Single(FindAll<TextEdit>(adding)).IsReadOnly);
        }

        [Fact]
        [DisplayName("Without a data object, a detail grid shows the toolbar AllowActions allows, with its buttons disabled")]
        public void Render_NoDataObject_DetailGridShowsDisabledToolbar()
        {
            var presenter = Render(BuildLayout(), SingleFormMode.Edit);

            var toolbar = GridToolbar(Assert.Single(FindAll<GridControl>(presenter)));
            Assert.True(toolbar.IsVisible);
            var visibleButtons = toolbar.Children.OfType<Button>().Where(b => b.IsVisible).ToList();
            Assert.NotEmpty(visibleButtons);
            Assert.All(visibleButtons, b => Assert.False(b.IsEnabled));
        }

        [Fact]
        [DisplayName("Without a data object, a detail grid hides its toolbar in View mode")]
        public void Render_NoDataObject_DetailGridHidesToolbarInViewMode()
        {
            var presenter = Render(BuildLayout(), SingleFormMode.View);

            Assert.False(GridToolbar(Assert.Single(FindAll<GridControl>(presenter))).IsVisible);
        }

        [Fact]
        [DisplayName("A detail grid whose layout allows no actions shows no toolbar")]
        public void Render_NoDataObject_NoActionsHidesToolbar()
        {
            var layout = BuildLayout();
            layout.Details![0].AllowActions = GridControlAllowActions.None;

            var presenter = Render(layout, SingleFormMode.Edit);

            Assert.False(GridToolbar(Assert.Single(FindAll<GridControl>(presenter))).IsVisible);
        }

        [Fact]
        [DisplayName("Fields wrap to a new row once ColumnCount columns are used, honouring ColumnSpan")]
        public void Render_FieldsWrapByColumnCountAndSpan()
        {
            var layout = new FormLayout { ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "a" });
            section.Fields.Add(new LayoutField { FieldName = "b", ColumnSpan = 2 });
            section.Fields.Add(new LayoutField { FieldName = "c" });
            layout.Sections!.Add(section);

            var grid = FieldGrid(Render(layout));

            Assert.Equal(2, grid.ColumnDefinitions.Count);
            var cells = grid.Children.ToList();
            Assert.Equal((0, 0), (Grid.GetRow(cells[0]), Grid.GetColumn(cells[0])));
            Assert.Equal((1, 0, 2), (Grid.GetRow(cells[1]), Grid.GetColumn(cells[1]), Grid.GetColumnSpan(cells[1])));
            Assert.Equal((2, 0), (Grid.GetRow(cells[2]), Grid.GetColumn(cells[2])));
        }

        [Fact]
        [DisplayName("Fields after a field spanning rows and columns start below it instead of overlapping it")]
        public void Render_FieldsAfterRowSpanStartBelowIt()
        {
            var layout = new FormLayout { ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "a" });
            section.Fields.Add(new LayoutField { FieldName = "b" });
            section.Fields.Add(new LayoutField { FieldName = "notes", RowSpan = 3, ColumnSpan = 2 });
            section.Fields.Add(new LayoutField { FieldName = "c" });
            section.Fields.Add(new LayoutField { FieldName = "d" });
            layout.Sections!.Add(section);

            var cells = FieldGrid(Render(layout)).Children.ToList();

            Assert.Equal((1, 0, 3, 2), (Grid.GetRow(cells[2]), Grid.GetColumn(cells[2]),
                Grid.GetRowSpan(cells[2]), Grid.GetColumnSpan(cells[2])));
            Assert.Equal((4, 0), (Grid.GetRow(cells[3]), Grid.GetColumn(cells[3])));
            Assert.Equal((4, 1), (Grid.GetRow(cells[4]), Grid.GetColumn(cells[4])));
        }

        [Fact]
        [DisplayName("A field spanning rows in one column leaves the cells beside it to the next fields")]
        public void Render_CellsBesideRowSpanAreFilled()
        {
            var layout = new FormLayout { ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "tall", RowSpan = 2 });
            section.Fields.Add(new LayoutField { FieldName = "b" });
            section.Fields.Add(new LayoutField { FieldName = "c" });
            section.Fields.Add(new LayoutField { FieldName = "d" });
            layout.Sections!.Add(section);

            var cells = FieldGrid(Render(layout)).Children.ToList();

            Assert.Equal((0, 0, 2), (Grid.GetRow(cells[0]), Grid.GetColumn(cells[0]), Grid.GetRowSpan(cells[0])));
            Assert.Equal((0, 1), (Grid.GetRow(cells[1]), Grid.GetColumn(cells[1])));
            Assert.Equal((1, 1), (Grid.GetRow(cells[2]), Grid.GetColumn(cells[2])));
            Assert.Equal((2, 0), (Grid.GetRow(cells[3]), Grid.GetColumn(cells[3])));
        }

        [Fact]
        [DisplayName("A ColumnSpan wider than the layout is clamped to its column count")]
        public void Render_ColumnSpanWiderThanLayoutIsClamped()
        {
            var layout = new FormLayout { ColumnCount = 2 };
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "wide", ColumnSpan = 5 });
            section.Fields.Add(new LayoutField { FieldName = "b" });
            layout.Sections!.Add(section);

            var cells = FieldGrid(Render(layout)).Children.ToList();

            Assert.Equal((0, 0, 2), (Grid.GetRow(cells[0]), Grid.GetColumn(cells[0]), Grid.GetColumnSpan(cells[0])));
            Assert.Equal((1, 0), (Grid.GetRow(cells[1]), Grid.GetColumn(cells[1])));
        }

        [Fact]
        [DisplayName("A field with Visible false is not rendered")]
        public void Render_HiddenFieldIsSkipped()
        {
            var layout = BuildLayout();
            layout.Sections![0].Fields![0].Visible = false;

            var presenter = Render(layout);

            Assert.DoesNotContain(FindAll<TextEdit>(presenter), e => e.FieldName == "sys_id");
        }

        [Fact]
        [DisplayName("IsCompact renders a single column and puts detail grids in the edit form")]
        public void Render_Compact_SingleColumnAndEditForm()
        {
            var presenter = Render(BuildLayout());

            presenter.IsCompact = true;

            Assert.Single(FieldGrid(presenter).ColumnDefinitions);
            Assert.Equal(GridEditMode.EditForm, Assert.Single(FindAll<GridControl>(presenter)).EditMode);
        }

        [Fact]
        [DisplayName("Refresh renders a change made to the layout in place")]
        public void Refresh_RendersInPlaceChange()
        {
            var layout = BuildLayout();
            var presenter = Render(layout);
            layout.Sections![0].Fields!.Add(new LayoutField { FieldName = "remark", ControlType = ControlType.MemoEdit });

            Assert.Empty(FindAll<MemoEdit>(presenter));
            presenter.Refresh();

            EditorFor<MemoEdit>(presenter, "remark");
        }

        [Fact]
        [DisplayName("Setting Layout to null clears the rendered form")]
        public void Layout_SetToNull_ClearsForm()
        {
            var presenter = Render(BuildLayout());

            presenter.Layout = null;

            Assert.Empty(presenter.Host.Children);
        }

        private static TextBlock CaptionOf(FormLayoutPresenter presenter, string caption)
            => Assert.Single(FindAll<TextBlock>(presenter), t => t.Text == caption);

        private static FormLayoutPresenter RenderWithSchemaRequired(bool schemaRequired)
        {
            var schema = new FormSchema("Customer", "Customer");
            var master = schema.Tables!.Add("Customer", "Customer");
            master.Fields!.Add("contact_name", "Contact Name", FieldDbType.String).Required = schemaRequired;
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();

            var layout = new FormLayout();
            var section = new LayoutSection { Name = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "contact_name", Caption = "Contact Name" });
            layout.Sections!.Add(section);

            var presenter = new FormLayoutPresenter();
            presenter.Update(p =>
            {
                p.Layout = layout;
                p.DataObject = dataObject;
            });
            return presenter;
        }

        [Fact]
        [DisplayName("A field the schema marks Required gets the required caption colour even when the layout does not")]
        public void Render_SchemaRequired_ColoursCaption()
        {
            var presenter = RenderWithSchemaRequired(schemaRequired: true);

            Assert.True(CaptionOf(presenter, "Contact Name").IsSet(TextBlock.ForegroundProperty));
        }

        [Fact]
        [DisplayName("A field neither the schema nor the layout marks Required keeps the default caption colour")]
        public void Render_NotRequired_LeavesCaptionDefault()
        {
            var presenter = RenderWithSchemaRequired(schemaRequired: false);

            Assert.False(CaptionOf(presenter, "Contact Name").IsSet(TextBlock.ForegroundProperty));
        }

        [Fact]
        [DisplayName("Rendering does not write into the layout")]
        public void Render_LeavesLayoutUnchanged()
        {
            var layout = BuildLayout();
            var before = XmlCodec.Serialize(layout);

            var presenter = Render(layout, SingleFormMode.View);
            presenter.IsCompact = true;

            Assert.Equal(before, XmlCodec.Serialize(layout));
        }
    }
}
