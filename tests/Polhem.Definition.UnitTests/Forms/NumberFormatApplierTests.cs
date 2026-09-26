using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// NumberFormatApplier.Bake bakes display formats into the numeric fields of a FormSchema from the company's decimal places.
    /// An explicit NumberFormat wins, None is skipped, and the company's decimal places show in the format.
    /// </summary>
    public class NumberFormatApplierTests
    {
        private static FormSchema SchemaWith(params FormField[] fields)
        {
            var schema = new FormSchema("Order", "訂單");
            var table = schema.Tables!.Add("Order", "訂單");
            foreach (var f in fields)
                table.Fields!.Add(f);
            return schema;
        }

        private static CompanyInfo CompanyWith(params NumberFormatItem[] overrides)
        {
            var company = new CompanyInfo { CompanyId = "C001" };
            foreach (var item in overrides)
                company.NumberFormats.Add(item);
            return company;
        }

        [Fact]
        [DisplayName("Bake applies the framework default format to Company/SystemFixed fields and does not bake Currency (amount) fields")]
        public void Bake_NullCompany_FrameworkFormat()
        {
            var schema = SchemaWith(
                new FormField("disc", "折扣", FieldDbType.Decimal) { NumberKind = NumberKind.Percent },
                new FormField("rate", "匯率", FieldDbType.Decimal) { NumberKind = NumberKind.ExchangeRate },
                new FormField("amount", "金額", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal("P2", schema.Tables!["Order"].Fields!["disc"].NumberFormat);
            Assert.Equal("N5", schema.Tables!["Order"].Fields!["rate"].NumberFormat);
            // Currency amounts resolve at runtime by their currency, so they are not baked.
            Assert.Equal(string.Empty, schema.Tables!["Order"].Fields!["amount"].NumberFormat);
        }

        [Fact]
        [DisplayName("Bake does not bake an amount field, which inherits the master currency field when it has no CurrencyField")]
        public void Bake_AmountField_NotBaked_InheritsMasterCurrencyField()
        {
            var schema = SchemaWith(new FormField("amount", "金額", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });
            schema.CurrencyField = "sys_currency";

            NumberFormatApplier.Bake(schema, null);

            var field = schema.Tables!["Order"].Fields!["amount"];
            Assert.Equal(string.Empty, field.NumberFormat);
            Assert.Equal("sys_currency", field.CurrencyField);
        }

        [Fact]
        [DisplayName("Bake does not overwrite an amount field's explicit CurrencyField with the master currency field")]
        public void Bake_AmountField_ExplicitCurrencyField_NotOverwritten()
        {
            var schema = SchemaWith(new FormField("home_amount", "本幣金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "local_currency",
            });
            schema.CurrencyField = "sys_currency";

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal("local_currency", schema.Tables!["Order"].Fields!["home_amount"].CurrencyField);
        }

        [Fact]
        [DisplayName("Bake leaves an amount field's CurrencyField empty when the master has no currency field")]
        public void Bake_AmountField_NoMasterCurrencyField_LeavesEmpty()
        {
            var schema = SchemaWith(new FormField("amount", "金額", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal(string.Empty, schema.Tables!["Order"].Fields!["amount"].CurrencyField);
        }

        [Fact]
        [DisplayName("Bake produces different format strings for companies A and B with different decimal places")]
        public void Bake_DifferentCompanies_DifferentFormats()
        {
            var companyA = CompanyWith(new NumberFormatItem(NumberKind.Percent, 2));
            var companyB = CompanyWith(new NumberFormatItem(NumberKind.Percent, 4));

            var schemaA = SchemaWith(new FormField("disc", "折扣", FieldDbType.Decimal) { NumberKind = NumberKind.Percent });
            var schemaB = SchemaWith(new FormField("disc", "折扣", FieldDbType.Decimal) { NumberKind = NumberKind.Percent });

            NumberFormatApplier.Bake(schemaA, companyA);
            NumberFormatApplier.Bake(schemaB, companyB);

            Assert.Equal("P2", schemaA.Tables!["Order"].Fields!["disc"].NumberFormat);
            Assert.Equal("P4", schemaB.Tables!["Order"].Fields!["disc"].NumberFormat);
        }

        [Fact]
        [DisplayName("Bake keeps an explicit NumberFormat instead of overwriting it")]
        public void Bake_ExplicitFormat_Preserved()
        {
            var field = new FormField("amount", "金額", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Amount,
                NumberFormat = "C2",
            };
            var schema = SchemaWith(field);

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal("C2", schema.Tables!["Order"].Fields!["amount"].NumberFormat);
        }

        [Fact]
        [DisplayName("Bake does not bake a quantity field bound to a UnitField (resolved at runtime by unit)")]
        public void Bake_QuantityWithUnitField_NotBaked()
        {
            var field = new FormField("order_qty", "數量", FieldDbType.Decimal)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            };
            var schema = SchemaWith(field);

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal(string.Empty, schema.Tables!["Order"].Fields!["order_qty"].NumberFormat);
        }

        [Theory]
        [InlineData(NumberKind.Quantity)]
        [InlineData(NumberKind.Weight)]
        [DisplayName("Bake does not bake quantity/weight fields without a UnitField either, and company decimal places do not apply")]
        public void Bake_UnitKindWithoutUnitField_NotBaked(NumberKind kind)
        {
            var field = new FormField("line_value", "數值", FieldDbType.Decimal) { NumberKind = kind };
            var schema = SchemaWith(field);

            NumberFormatApplier.Bake(schema, CompanyWith(new NumberFormatItem(kind, 2)));

            Assert.Equal(string.Empty, schema.Tables!["Order"].Fields!["line_value"].NumberFormat);
        }

        [Fact]
        [DisplayName("Bake skips NumberKind=None fields and applies no format")]
        public void Bake_NoneKind_Skipped()
        {
            var field = new FormField("memo", "備註", FieldDbType.String);
            var schema = SchemaWith(field);

            NumberFormatApplier.Bake(schema, null);

            Assert.Equal(string.Empty, schema.Tables!["Order"].Fields!["memo"].NumberFormat);
        }

        [Fact]
        [DisplayName("HasNumericField returns true when a field has a NumberKind and false when all are None")]
        public void HasNumericField_DetectsNumericFields()
        {
            var numeric = SchemaWith(new FormField("amount", "金額", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });
            var plain = SchemaWith(new FormField("memo", "備註", FieldDbType.String));

            Assert.True(NumberFormatApplier.HasNumericField(numeric));
            Assert.False(NumberFormatApplier.HasNumericField(plain));
        }

        [Fact]
        [DisplayName("Bake changes only the instance passed in, and the source schema (simulating the cache) is not polluted")]
        public void Bake_DoesNotAffectSourceSchema()
        {
            // Percent (company-sourced, so it is baked) checks pollution isolation. Amount is not baked, so it does not fit this test.
            var source = SchemaWith(new FormField("disc", "折扣", FieldDbType.Decimal) { NumberKind = NumberKind.Percent });

            // Mimic the delivery flow: bake only after cloning, so the source keeps an empty `NumberFormat`.
            var clone = source.Clone();
            NumberFormatApplier.Bake(clone, null);

            Assert.Equal(string.Empty, source.Tables!["Order"].Fields!["disc"].NumberFormat);
            Assert.Equal("P2", clone.Tables!["Order"].Fields!["disc"].NumberFormat);
        }
    }
}
