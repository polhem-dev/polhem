using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// The event arguments <see cref="FieldValueChangeCoordinator"/> raises after a server-side field change.
    /// </summary>
    public class FieldValueChangeEventArgsTests
    {
        private static readonly IReadOnlyDictionary<string, string> s_none = new Dictionary<string, string>();

        private static DataRow NewRow()
        {
            var table = new DataTable("Order");
            table.Columns.Add("qty", typeof(decimal));
            return table.Rows.Add(1m);
        }

        private static FormTable OrderTable()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("qty", "Quantity", FieldDbType.Decimal);
            table.Fields!.Add("note", string.Empty, FieldDbType.String);
            return table;
        }

        [Fact]
        [DisplayName("The applied arguments carry the table, field, row and messages they were given")]
        public void Applied_Constructor_ExposesValues()
        {
            var row = NewRow();
            var errors = new Dictionary<string, string> { ["qty"] = "Too many" };

            var args = new FieldValueChangeAppliedEventArgs("Order", "qty", row, errors, s_none);

            Assert.Equal("Order", args.TableName);
            Assert.Equal("qty", args.FieldName);
            Assert.Same(row, args.Row);
            Assert.Same(errors, args.Errors);
            Assert.Same(s_none, args.Warnings);
        }

        [Fact]
        [DisplayName("HasMessages is false when there are neither errors nor warnings")]
        public void HasMessages_None_ReturnsFalse()
        {
            var args = new FieldValueChangeAppliedEventArgs("Order", "qty", NewRow(), s_none, s_none);

            Assert.False(args.HasMessages);
        }

        [Fact]
        [DisplayName("HasMessages is true when there is only a warning")]
        public void HasMessages_WarningOnly_ReturnsTrue()
        {
            var warnings = new Dictionary<string, string> { ["qty"] = "Low stock" };

            var args = new FieldValueChangeAppliedEventArgs("Order", "qty", NewRow(), s_none, warnings);

            Assert.True(args.HasMessages);
        }

        [Fact]
        [DisplayName("FormatMessages names each field by its caption, errors before warnings")]
        public void FormatMessages_WithTable_UsesCaptions()
        {
            var errors = new Dictionary<string, string> { ["qty"] = "Too many" };
            var warnings = new Dictionary<string, string> { ["unknown"] = "Check it" };
            var args = new FieldValueChangeAppliedEventArgs("Order", "qty", NewRow(), errors, warnings);

            var text = args.FormatMessages(OrderTable());

            Assert.Equal("Quantity: Too many" + Environment.NewLine + "unknown: Check it", text);
        }

        [Fact]
        [DisplayName("FormatMessages falls back to the field name when the field has no caption")]
        public void FormatMessages_EmptyCaption_UsesFieldName()
        {
            var errors = new Dictionary<string, string> { ["note"] = "Required" };
            var args = new FieldValueChangeAppliedEventArgs("Order", "note", NewRow(), errors, s_none);

            Assert.Equal("note: Required", args.FormatMessages(OrderTable()));
        }

        [Fact]
        [DisplayName("FormatMessages without a table names each field by its field name")]
        public void FormatMessages_NoTable_UsesFieldNames()
        {
            var errors = new Dictionary<string, string> { ["qty"] = "Too many" };
            var args = new FieldValueChangeAppliedEventArgs("Order", "qty", NewRow(), errors, s_none);

            Assert.Equal("qty: Too many", args.FormatMessages(null));
        }

        [Fact]
        [DisplayName("The failed arguments carry the table, field, row and exception they were given")]
        public void Failed_Constructor_ExposesValues()
        {
            var row = NewRow();
            var exception = new InvalidOperationException("server down");

            var args = new FieldValueChangeFailedEventArgs("Order", "qty", row, exception);

            Assert.Equal("Order", args.TableName);
            Assert.Equal("qty", args.FieldName);
            Assert.Same(row, args.Row);
            Assert.Same(exception, args.Exception);
        }
    }
}
