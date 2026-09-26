using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Tests for the <see cref="FormLayout.Clone"/> family: every field copied, nested structures independent, and changing the copy leaves the source untouched.
    /// They exist because definition data is immutable after init: the layout obtained at runtime is the process-wide cached instance,
    /// so it must be cloned before any processing.
    /// </summary>
    public class FormLayoutCloneTests
    {
        [Fact]
        [DisplayName("Clone copies every scalar property of FormLayout")]
        public void Clone_CopiesScalarProperties()
        {
            var source = BuildLayout();

            var copy = source.Clone();

            Assert.Equal(source.LayoutId, copy.LayoutId);
            Assert.Equal(source.ProgId, copy.ProgId);
            Assert.Equal(source.Caption, copy.Caption);
            Assert.Equal(source.ColumnCount, copy.ColumnCount);
        }

        [Fact]
        [DisplayName("Clone deep-copies Sections / Fields without sharing instances")]
        public void Clone_DeepCopiesSectionsAndFields()
        {
            var source = BuildLayout();

            var copy = source.Clone();

            Assert.Equal(source.Sections!.Count, copy.Sections!.Count);
            Assert.NotSame(source.Sections[0], copy.Sections[0]);
            Assert.NotSame(source.Sections[0].Fields![0], copy.Sections[0].Fields![0]);
            Assert.Equal("員工編號", copy.Sections[0].Fields![0].Caption);
        }

        [Fact]
        [DisplayName("Clone deep-copies Details / Columns without sharing instances")]
        public void Clone_DeepCopiesDetailsAndColumns()
        {
            var source = BuildLayout();

            var copy = source.Clone();

            Assert.Equal(source.Details!.Count, copy.Details!.Count);
            Assert.NotSame(source.Details[0], copy.Details[0]);
            Assert.NotSame(source.Details[0].Columns![0], copy.Details[0].Columns![0]);
            Assert.Equal("項次", copy.Details[0].Columns![0].Caption);
        }

        [Fact]
        [DisplayName("Changing the copy does not affect the source (protects the cached instance)")]
        public void Clone_MutatingCopy_LeavesSourceUntouched()
        {
            var source = BuildLayout();

            var copy = source.Clone();
            copy.Caption = "改過的表單";
            copy.Sections![0].Caption = "改過的區塊";
            copy.Sections[0].Fields![0].Caption = "改過的欄位";
            copy.Details![0].Caption = "改過的明細";
            copy.Details[0].Columns![0].Caption = "改過的欄";
            copy.Sections[0].Fields!.Add(new LayoutField { FieldName = "extra" });

            Assert.Equal("員工資料", source.Caption);
            Assert.Equal("主要資料", source.Sections![0].Caption);
            Assert.Equal("員工編號", source.Sections[0].Fields![0].Caption);
            Assert.Equal("工作紀錄", source.Details![0].Caption);
            Assert.Equal("項次", source.Details[0].Columns![0].Caption);
            Assert.Single(source.Sections[0].Fields!);
        }

        [Fact]
        [DisplayName("LayoutField.Clone copies base and own members (including ExtendedProperties)")]
        public void LayoutFieldClone_CopiesAllMembers()
        {
            var source = new LayoutField
            {
                FieldName = "sys_id",
                Caption = "編號",
                ControlType = ControlType.ButtonEdit,
                DisplayFields = "a,b",
                DisplayFormat = "yyyy/MM/dd",
                NumberFormat = "#,##0.00",
                NumberKind = NumberKind.Amount,
                CurrencyField = "currency",
                UnitField = "unit",
                Visible = false,
                ReadOnly = true,
                Required = true,
                AllowEditModes = FormEditModes.Add,
            };
            source.ExtendedProperties!.Add("placeholder", "請輸入");

            var copy = source.Clone();

            Assert.Equal("sys_id", copy.FieldName);
            Assert.Equal("編號", copy.Caption);
            Assert.Equal(ControlType.ButtonEdit, copy.ControlType);
            Assert.Equal("a,b", copy.DisplayFields);
            Assert.Equal("yyyy/MM/dd", copy.DisplayFormat);
            Assert.Equal("#,##0.00", copy.NumberFormat);
            Assert.Equal(NumberKind.Amount, copy.NumberKind);
            Assert.Equal("currency", copy.CurrencyField);
            Assert.Equal("unit", copy.UnitField);
            Assert.False(copy.Visible);
            Assert.True(copy.ReadOnly);
            Assert.True(copy.Required);
            Assert.Equal(FormEditModes.Add, copy.AllowEditModes);
            Assert.Equal("請輸入", copy.ExtendedProperties!.First(x => x.Name == "placeholder").Value);
            Assert.NotSame(source.ExtendedProperties, copy.ExtendedProperties);
        }

        [Fact]
        [DisplayName("LayoutColumn.Clone copies Width and the base members")]
        public void LayoutColumnClone_CopiesWidthAndBase()
        {
            var source = new LayoutColumn("qty", "數量", ControlType.NumericEdit) { Width = 120, ReadOnly = true };

            var copy = source.Clone();

            Assert.Equal("qty", copy.FieldName);
            Assert.Equal("數量", copy.Caption);
            Assert.Equal(ControlType.NumericEdit, copy.ControlType);
            Assert.Equal(120, copy.Width);
            Assert.True(copy.ReadOnly);
        }

        [Fact]
        [DisplayName("LayoutGrid.Clone copies AllowActions / AllowEditModes")]
        public void LayoutGridClone_CopiesGridMembers()
        {
            var source = new LayoutGrid("Detail", "明細")
            {
                AllowActions = GridControlAllowActions.Add,
                AllowEditModes = FormEditModes.Edit,
            };

            var copy = source.Clone();

            Assert.Equal("Detail", copy.TableName);
            Assert.Equal("明細", copy.Caption);
            Assert.Equal(GridControlAllowActions.Add, copy.AllowActions);
            Assert.Equal(FormEditModes.Edit, copy.AllowEditModes);
        }

        [Fact]
        [DisplayName("LayoutSection.Clone copies ShowCaption")]
        public void LayoutSectionClone_CopiesShowCaption()
        {
            var source = new LayoutSection { Name = "Main", Caption = "主要", ShowCaption = false };

            var copy = source.Clone();

            Assert.Equal("Main", copy.Name);
            Assert.Equal("主要", copy.Caption);
            Assert.False(copy.ShowCaption);
        }

        [Fact]
        [DisplayName("Clone with empty Sections / Details does not throw")]
        public void Clone_EmptyCollections_DoesNotThrow()
        {
            var source = new FormLayout { LayoutId = "L", ProgId = "P" };

            var exception = Record.Exception(() => source.Clone());

            Assert.Null(exception);
        }

        private static FormLayout BuildLayout()
        {
            var layout = new FormLayout
            {
                LayoutId = "Employee",
                ProgId = "Employee",
                Caption = "員工資料",
                ColumnCount = 3,
            };
            var section = new LayoutSection { Name = "Main", Caption = "主要資料" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "員工編號" });
            layout.Sections!.Add(section);

            var grid = new LayoutGrid("WorkLog", "工作紀錄");
            grid.Columns!.Add(new LayoutColumn("seq", "項次", ControlType.TextEdit));
            layout.Details!.Add(grid);
            return layout;
        }
    }
}
