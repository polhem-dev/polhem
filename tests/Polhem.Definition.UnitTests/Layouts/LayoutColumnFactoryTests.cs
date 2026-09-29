using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutColumnFactory (a shared helper).
    /// </summary>
    public class LayoutColumnFactoryTests
    {
        [Theory]
        [InlineData(ControlType.TextEdit, FieldDbType.String, ControlType.TextEdit)]
        [InlineData(ControlType.CheckEdit, FieldDbType.String, ControlType.CheckEdit)]
        [InlineData(ControlType.MemoEdit, FieldDbType.String, ControlType.MemoEdit)]
        [InlineData(ControlType.DropDownEdit, FieldDbType.Integer, ControlType.DropDownEdit)]
        [DisplayName("ResolveControlType returns the specified value as is when it is not Auto")]
        public void ResolveControlType_NonAuto_ReturnsAsIs(ControlType type, FieldDbType dbType, ControlType expected)
        {
            var actual = LayoutColumnFactory.ResolveControlType(type, dbType);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(FieldDbType.Boolean, ControlType.CheckEdit)]
        [InlineData(FieldDbType.Date, ControlType.DateEdit)]
        [InlineData(FieldDbType.DateTime, ControlType.DateTimeEdit)]
        [InlineData(FieldDbType.Text, ControlType.MemoEdit)]
        [InlineData(FieldDbType.String, ControlType.TextEdit)]
        [InlineData(FieldDbType.Short, ControlType.NumericEdit)]
        [InlineData(FieldDbType.Integer, ControlType.NumericEdit)]
        [InlineData(FieldDbType.Long, ControlType.NumericEdit)]
        [InlineData(FieldDbType.Decimal, ControlType.NumericEdit)]
        [InlineData(FieldDbType.Currency, ControlType.NumericEdit)]
        [InlineData(FieldDbType.Guid, ControlType.TextEdit)]
        [DisplayName("ResolveControlType infers the default control type from the DbType when it is Auto")]
        public void ResolveControlType_Auto_MapsDbType(FieldDbType dbType, ControlType expected)
        {
            var actual = LayoutColumnFactory.ResolveControlType(ControlType.Auto, dbType);

            Assert.Equal(expected, actual);
        }

        [Fact]
        [DisplayName("ToField copies the FormField properties to a LayoutField")]
        public void ToField_CopiesProperties()
        {
            var formField = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                ControlType = ControlType.TextEdit,
                DisplayFormat = "{0:C}",
                NumberFormat = "Amount"
            };

            var field = LayoutColumnFactory.ToField(formField);

            Assert.Equal("amount", field.FieldName);
            Assert.Equal("金額", field.Caption);
            Assert.Equal(ControlType.TextEdit, field.ControlType);
            Assert.Equal("{0:C}", field.DisplayFormat);
            Assert.Equal("Amount", field.NumberFormat);
        }

        [Fact]
        [DisplayName("ToColumn copies the FormField properties (including Width) to a LayoutColumn")]
        public void ToColumn_CopiesProperties()
        {
            var formField = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                ControlType = ControlType.TextEdit,
                Width = 150,
                DisplayFormat = "{0:C}",
                NumberFormat = "Amount"
            };

            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal("amount", column.FieldName);
            Assert.Equal("金額", column.Caption);
            Assert.Equal(ControlType.TextEdit, column.ControlType);
            Assert.Equal(150, column.Width);
            Assert.Equal("{0:C}", column.DisplayFormat);
            Assert.Equal("Amount", column.NumberFormat);
        }

        [Fact]
        [DisplayName("ToField infers CheckEdit for ControlType=Auto + DbType=Boolean")]
        public void ToField_AutoControlType_BooleanDbType_ProducesCheckEdit()
        {
            var formField = new FormField("active", "啟用", FieldDbType.Boolean)
            {
                ControlType = ControlType.Auto
            };

            var field = LayoutColumnFactory.ToField(formField);

            Assert.Equal(ControlType.CheckEdit, field.ControlType);
        }

        [Fact]
        [DisplayName("ToField/ToColumn pass the ReadOnly and Required flags through")]
        public void ToFieldAndColumn_PropagateReadOnlyAndRequired()
        {
            var formField = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                ReadOnly = true,
                Required = true,
            };

            var field = LayoutColumnFactory.ToField(formField);
            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.True(field.ReadOnly);
            Assert.True(field.Required);
            Assert.True(column.ReadOnly);
            Assert.True(column.Required);
        }

        [Fact]
        [DisplayName("ToField/ToColumn pass the NumberKind semantic kind through")]
        public void ToFieldAndColumn_PropagateNumberKind()
        {
            var formField = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
            };

            var field = LayoutColumnFactory.ToField(formField);
            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal(NumberKind.Amount, field.NumberKind);
            Assert.Equal(NumberKind.Amount, column.NumberKind);
        }

        [Fact]
        [DisplayName("ToField/ToColumn pass CurrencyField (the CUKY reference field name) through")]
        public void ToFieldAndColumn_PropagateCurrencyField()
        {
            var formField = new FormField("home_amount", "本幣金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "local_currency",
            };

            var field = LayoutColumnFactory.ToField(formField);
            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal("local_currency", field.CurrencyField);
            Assert.Equal("local_currency", column.CurrencyField);
        }

        [Fact]
        [DisplayName("ToField/ToColumn pass UnitField (the UNIT reference field name) through")]
        public void ToFieldAndColumn_PropagateUnitField()
        {
            var formField = new FormField("order_qty", "數量", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            };

            var field = LayoutColumnFactory.ToField(formField);
            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal("qty_uom", field.UnitField);
            Assert.Equal("qty_uom", column.UnitField);
        }

        [Fact]
        [DisplayName("ToColumn keeps Width=0 as 0, meaning auto/unset")]
        public void ToColumn_WidthZero_StaysZero()
        {
            var formField = new FormField("col", "欄", FieldDbType.String);

            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal(0, column.Width);
        }

        [Fact]
        [DisplayName("ResolveControlType resolves Auto + RelationProgId to ButtonEdit")]
        public void ResolveControlType_AutoWithRelation_ProducesButtonEdit()
        {
            var formField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
            };

            var actual = LayoutColumnFactory.ResolveControlType(formField);

            Assert.Equal(ControlType.ButtonEdit, actual);
        }

        [Fact]
        [DisplayName("ResolveControlType gives an explicit ControlType precedence over the RelationProgId inference")]
        public void ResolveControlType_ExplicitTypeWithRelation_ExplicitWins()
        {
            var formField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                ControlType = ControlType.DropDownEdit,
                RelationProgId = "Customer",
            };

            var actual = LayoutColumnFactory.ResolveControlType(formField);

            Assert.Equal(ControlType.DropDownEdit, actual);
        }

        [Fact]
        [DisplayName("GetDisplayFields gives an explicit declaration precedence over the convention")]
        public void GetDisplayFields_Explicit_WinsOverConvention()
        {
            var formField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
                DisplayFields = "ref_customer_name",
            };
            formField.RelationFieldMappings!.Add("sys_id", "ref_customer_id");
            formField.RelationFieldMappings!.Add("sys_name", "ref_customer_name");

            var actual = formField.GetDisplayFields();

            Assert.Equal(["ref_customer_name"], actual);
        }

        [Fact]
        [DisplayName("GetDisplayFields without a setting takes the destination fields of sys_id and sys_name by convention (ID + name)")]
        public void GetDisplayFields_Convention_UsesIdAndNameMappings()
        {
            var formField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
            };
            formField.RelationFieldMappings!.Add("sys_id", "ref_customer_id");
            formField.RelationFieldMappings!.Add("sys_name", "ref_customer_name");

            var actual = formField.GetDisplayFields();

            Assert.Equal(["ref_customer_id", "ref_customer_name"], actual);
        }

        [Fact]
        [DisplayName("GetDisplayFields returns only the ID field when a transactional target maps only sys_id (document number display)")]
        public void GetDisplayFields_IdMappingOnly_ReturnsIdField()
        {
            var formField = new FormField("po_rowid", "採購單", FieldDbType.Guid)
            {
                RelationProgId = "PurchaseOrder",
            };
            formField.RelationFieldMappings!.Add("sys_id", "ref_po_no");

            var actual = formField.GetDisplayFields();

            Assert.Equal(["ref_po_no"], actual);
        }

        [Fact]
        [DisplayName("GetDisplayFields returns an empty collection for a non-relation field")]
        public void GetDisplayFields_NonRelationField_ReturnsEmpty()
        {
            var formField = new FormField("amount", "金額", FieldDbType.Decimal);

            var actual = formField.GetDisplayFields();

            Assert.Empty(actual);
        }

        [Fact]
        [DisplayName("ToField gives a relation field a ButtonEdit and the convention DisplayFields")]
        public void ToField_RelationField_CarriesButtonEditAndDisplayField()
        {
            var formField = new FormField("customer_rowid", "客戶", FieldDbType.Guid)
            {
                RelationProgId = "Customer",
            };
            formField.RelationFieldMappings!.Add("sys_name", "ref_customer_name");

            var field = LayoutColumnFactory.ToField(formField);

            Assert.Equal(ControlType.ButtonEdit, field.ControlType);
            Assert.Equal("ref_customer_name", field.DisplayFields);
        }

        [Fact]
        [DisplayName("ToColumn gives a relation field a ButtonEdit and the convention DisplayFields")]
        public void ToColumn_RelationField_CarriesButtonEditAndDisplayField()
        {
            var formField = new FormField("product_rowid", "商品", FieldDbType.Guid)
            {
                RelationProgId = "Product",
            };
            formField.RelationFieldMappings!.Add("sys_name", "ref_product_name");

            var column = LayoutColumnFactory.ToColumn(formField);

            Assert.Equal(ControlType.ButtonEdit, column.ControlType);
            Assert.Equal("ref_product_name", column.DisplayFields);
        }
    }
}
