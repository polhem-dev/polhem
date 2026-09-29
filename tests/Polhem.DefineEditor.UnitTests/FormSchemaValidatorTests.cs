using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.Definition;
using Polhem.Definition.Forms;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Unit binding checks of <see cref="FormSchemaValidator"/>: a quantity or weight field must bind a UnitField,
    /// and the UnitField must be a field of the same table with the same casing (at run time it is looked up in the
    /// same row by its declared field name).
    /// </summary>
    public class FormSchemaValidatorTests
    {
        private static FormSchema SchemaWith(params FormField[] fields)
        {
            var schema = new FormSchema("Order", "訂單");
            var table = schema.Tables!.Add("OrderLine", "明細");
            foreach (var field in fields)
                table.Fields!.Add(field);
            return schema;
        }

        private static List<ValidationIssue> UnitFieldErrors(FormSchema schema)
            => FormSchemaValidator.Validate(schema, SolutionContext.Empty)
                .Where(issue => issue.Severity == ValidationSeverity.Error
                    && issue.Path.EndsWith(".UnitField", StringComparison.Ordinal))
                .ToList();

        [Theory]
        [InlineData(NumberKind.Quantity)]
        [InlineData(NumberKind.Weight)]
        [DisplayName("A quantity or weight field without a UnitField reports an Error")]
        public void Validate_UnitKindWithoutUnitField_ReportsError(NumberKind kind)
        {
            var schema = SchemaWith(new FormField("qty", "數量", FieldDbType.Decimal) { NumberKind = kind });

            var issue = Assert.Single(UnitFieldErrors(schema));
            Assert.Equal("OrderLine.qty.UnitField", issue.Path);
        }

        [Fact]
        [DisplayName("A UnitField naming a field that does not exist on the same table reports an Error")]
        public void Validate_UnitFieldNotOnTable_ReportsError()
        {
            var schema = SchemaWith(
                new FormField("qty", "數量", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity, UnitField = "qty_uom" });

            var issue = Assert.Single(UnitFieldErrors(schema));
            Assert.Contains("qty_uom", issue.Message);
        }

        [Fact]
        [DisplayName("A UnitField on another table (such as the header) reports an Error because run time only reads the same row")]
        public void Validate_UnitFieldOnAnotherTable_ReportsError()
        {
            var schema = new FormSchema("Order", "訂單");
            schema.Tables!.Add("Order", "訂單").Fields!.Add("uom", "單位", FieldDbType.String);
            schema.Tables.Add("OrderLine", "明細").Fields!.Add(
                new FormField("qty", "數量", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity, UnitField = "uom" });

            var issue = Assert.Single(UnitFieldErrors(schema));
            Assert.Equal("OrderLine.qty.UnitField", issue.Path);
        }

        [Fact]
        [DisplayName("A UnitField differing only in case reports an Error because run time looks it up case-sensitively by the declared name")]
        public void Validate_UnitFieldCaseMismatch_ReportsError()
        {
            var schema = SchemaWith(
                new FormField("qty", "數量", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity, UnitField = "QTY_UOM" },
                new FormField("qty_uom", "單位", FieldDbType.String));

            Assert.Single(UnitFieldErrors(schema));
        }

        [Fact]
        [DisplayName("A quantity field bound to a unit field on the same table with the same casing reports no unit Error")]
        public void Validate_UnitFieldOnSameTable_NoError()
        {
            var schema = SchemaWith(
                new FormField("qty", "數量", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity, UnitField = "qty_uom" },
                new FormField("qty_uom", "單位", FieldDbType.String));

            Assert.Empty(UnitFieldErrors(schema));
        }

        [Theory]
        [InlineData(NumberKind.None)]
        [InlineData(NumberKind.Amount)]
        [InlineData(NumberKind.UnitPrice)]
        [InlineData(NumberKind.Percent)]
        [DisplayName("A field that is neither quantity nor weight does not require a UnitField")]
        public void Validate_NonUnitKind_NoUnitFieldRequired(NumberKind kind)
        {
            var schema = SchemaWith(new FormField("value", "數值", FieldDbType.Decimal) { NumberKind = kind });

            Assert.Empty(UnitFieldErrors(schema));
        }
    }
}
