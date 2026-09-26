using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for FormField paths not covered elsewhere: the Collection==null branch of the Table property,
    /// getting the owner after joining a FormTable, and the ToString format.
    /// </summary>
    public class FormFieldTests
    {
        [Fact]
        [DisplayName("Table returns null when Collection is null")]
        public void Table_NoCollection_ReturnsNull()
        {
            var field = new FormField("sys_no", "流水號", FieldDbType.AutoIncrement);

            Assert.Null(field.Table);
        }

        [Fact]
        [DisplayName("Table returns the FormTable after the field is added to FormTable.Fields")]
        public void Table_AddedToFormTable_ReturnsOwner()
        {
            var ft = new FormTable("Customer", "客戶");
            var field = new FormField("sys_no", "流水號", FieldDbType.AutoIncrement);
            ft.Fields!.Add(field);

            Assert.Same(ft, field.Table);
        }

        [Fact]
        [DisplayName("ToString returns \"FieldName - Caption\"")]
        public void ToString_ReturnsFieldNameDashCaption()
        {
            var field = new FormField("sys_no", "流水號", FieldDbType.AutoIncrement);

            Assert.Equal("sys_no - 流水號", field.ToString());
        }

        [Fact]
        [DisplayName("NumberKind defaults to None")]
        public void NumberKind_DefaultsToNone()
        {
            var field = new FormField("amount", "金額", FieldDbType.Decimal);

            Assert.Equal(NumberKind.None, field.NumberKind);
        }

        [Fact]
        [DisplayName("NumberKind XML round-trip keeps the semantic kind")]
        public void NumberKind_XmlRoundtrip_Preserved()
        {
            var original = new FormField("unit_price", "單價", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.UnitPrice,
            };

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<FormField>(xml);

            Assert.NotNull(restored);
            Assert.Equal(NumberKind.UnitPrice, restored!.NumberKind);
        }

        [Fact]
        [DisplayName("NumberKind=None at its default is omitted from serialization")]
        public void NumberKind_None_OmitsXmlAttribute()
        {
            var field = new FormField("col", "欄", FieldDbType.String);

            var xml = XmlCodec.Serialize(field);

            Assert.DoesNotContain("NumberKind=", xml);
        }

        [Fact]
        [DisplayName("Clone copies NumberKind, ReadOnly and Required")]
        public void Clone_CopiesNumberKindReadOnlyRequired()
        {
            var original = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                ReadOnly = true,
                Required = true,
            };

            var clone = original.Clone();

            Assert.Equal(NumberKind.Amount, clone.NumberKind);
            Assert.True(clone.ReadOnly);
            Assert.True(clone.Required);
        }

        [Fact]
        [DisplayName("CurrencyField XML round-trip keeps the CUKY reference field name")]
        public void CurrencyField_XmlRoundtrip_Preserved()
        {
            var original = new FormField("home_amount", "本幣金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "local_currency",
            };

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<FormField>(xml);

            Assert.NotNull(restored);
            Assert.Equal("local_currency", restored!.CurrencyField);
        }

        [Fact]
        [DisplayName("CurrencyField at its empty default is omitted from serialization")]
        public void CurrencyField_Empty_OmitsXmlAttribute()
        {
            var field = new FormField("col", "欄", FieldDbType.String);

            var xml = XmlCodec.Serialize(field);

            Assert.DoesNotContain("CurrencyField=", xml);
        }

        [Fact]
        [DisplayName("Clone copies CurrencyField")]
        public void Clone_CopiesCurrencyField()
        {
            var original = new FormField("home_amount", "本幣金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "local_currency",
            };

            var clone = original.Clone();

            Assert.Equal("local_currency", clone.CurrencyField);
        }

        [Fact]
        [DisplayName("UnitField XML round-trip keeps the UNIT reference field name")]
        public void UnitField_XmlRoundtrip_Preserved()
        {
            var original = new FormField("order_qty", "數量", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            };

            var xml = XmlCodec.Serialize(original);
            var restored = XmlCodec.Deserialize<FormField>(xml);

            Assert.NotNull(restored);
            Assert.Equal("qty_uom", restored!.UnitField);
        }

        [Fact]
        [DisplayName("UnitField at its empty default is omitted from serialization")]
        public void UnitField_Empty_OmitsXmlAttribute()
        {
            var field = new FormField("col", "欄", FieldDbType.String);

            var xml = XmlCodec.Serialize(field);

            Assert.DoesNotContain("UnitField=", xml);
        }

        [Fact]
        [DisplayName("Clone copies UnitField")]
        public void Clone_CopiesUnitField()
        {
            var original = new FormField("order_qty", "數量", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            };

            var clone = original.Clone();

            Assert.Equal("qty_uom", clone.UnitField);
        }
    }
}
