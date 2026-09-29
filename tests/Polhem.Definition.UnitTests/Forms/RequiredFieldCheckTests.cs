using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Which empty required fields <see cref="RequiredFieldCheck"/> reports, what counts as empty, and the prompt
    /// it builds.
    /// </summary>
    /// <remarks>
    /// The rule is shared by the server's save and the UI heads. How the server turns the first miss into its
    /// refusal is covered by <c>FormBusinessObjectRequiredFieldTests</c> in the Business tests.
    /// </remarks>
    public class RequiredFieldCheckTests
    {
        private const string Master = "Order";
        private const string Detail = "OrderLine";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(Master, "Orders");
            var master = schema.Tables!.Add(Master, "Orders");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("order_no", "Order No", FieldDbType.String).Required = true;
            master.Fields.Add("cust_rowid", "Customer", FieldDbType.Guid).Required = true;
            master.Fields.Add("qty_total", "Total Quantity", FieldDbType.Decimal).Required = true;
            master.Fields.Add("note", "Note", FieldDbType.String);
            var detail = schema.Tables.Add(Detail, "Order Lines");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields.Add("product", "Product", FieldDbType.String).Required = true;
            return schema;
        }

        private static DataSet BuildDataSet(FormSchema schema, string orderNo, object customer, params string[] products)
        {
            var dataSet = new DataSet(schema.ProgId);
            foreach (var formTable in schema.Tables!)
            {
                var dataTable = dataSet.Tables.Add(formTable.TableName);
                foreach (var formField in formTable.Fields!)
                    dataTable.Columns.Add(formField.FieldName, formField.DbType == FieldDbType.Guid ? typeof(Guid)
                        : formField.DbType == FieldDbType.Decimal ? typeof(decimal) : typeof(string));
            }
            var master = dataSet.Tables[Master]!;
            var row = master.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["order_no"] = orderNo;
            row["cust_rowid"] = customer;
            row["qty_total"] = 0m;
            master.Rows.Add(row);
            var detail = dataSet.Tables[Detail]!;
            foreach (string product in products)
            {
                var line = detail.NewRow();
                line[SysFields.RowId] = Guid.NewGuid();
                line["product"] = product;
                detail.Rows.Add(line);
            }
            return dataSet;
        }

        [Fact]
        [DisplayName("A record with every required field filled reports nothing")]
        public void FindMissing_AllFilled_ReturnsEmpty()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid(), "Tea");

            Assert.Empty(RequiredFieldCheck.FindMissing(schema, dataSet));
        }

        [Fact]
        [DisplayName("White-space text and an empty GUID are reported, master first, and a zero number is not")]
        public void FindMissing_EmptyValues_ReportsMasterThenDetail()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "   ", Guid.Empty, "Tea", string.Empty);

            var missing = RequiredFieldCheck.FindMissing(schema, dataSet);

            Assert.Equal(["order_no", "cust_rowid", "product"], missing.Select(m => m.Field.FieldName));
            Assert.Equal([false, false, true], missing.Select(m => m.IsDetail));
        }

        [Fact]
        [DisplayName("A field empty in several detail rows is reported once")]
        public void FindMissing_EmptyInSeveralRows_ReportsFieldOnce()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid(), string.Empty, string.Empty);

            var missing = Assert.Single(RequiredFieldCheck.FindMissing(schema, dataSet));

            Assert.Equal("product", missing.Field.FieldName);
        }

        [Fact]
        [DisplayName("Unchanged and deleted rows are not judged, because a save does not write them")]
        public void FindMissing_UnchangedOrDeletedRows_AreSkipped()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, string.Empty, Guid.NewGuid(), string.Empty);
            dataSet.AcceptChanges();
            dataSet.Tables[Detail]!.Rows[0].Delete();

            Assert.Empty(RequiredFieldCheck.FindMissing(schema, dataSet));
        }

        [Fact]
        [DisplayName("A modified row that lacks a required column keeps its stored value and is not judged")]
        public void FindMissing_ModifiedRowWithoutColumn_IsNotJudged()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid());
            dataSet.Tables[Master]!.Columns.Remove("order_no");
            dataSet.AcceptChanges();
            dataSet.Tables[Master]!.Rows[0]["qty_total"] = 5m;

            Assert.Empty(RequiredFieldCheck.FindMissing(schema, dataSet));
        }

        [Fact]
        [DisplayName("A modified row with an emptied required field is reported")]
        public void FindMissing_ModifiedRowEmptied_IsReported()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid());
            dataSet.AcceptChanges();
            dataSet.Tables[Master]!.Rows[0]["order_no"] = string.Empty;

            var missing = Assert.Single(RequiredFieldCheck.FindMissing(schema, dataSet));

            Assert.Equal("order_no", missing.Field.FieldName);
        }

        [Fact]
        [DisplayName("An added row without the required column is judged by the type default an insert would store")]
        public void FindMissing_AddedRowWithoutColumn_JudgesTypeDefault()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid());
            dataSet.Tables[Master]!.Columns.Remove("order_no");
            dataSet.Tables[Master]!.Columns.Remove("cust_rowid");

            var missing = RequiredFieldCheck.FindMissing(schema, dataSet);

            Assert.Equal(["order_no", "cust_rowid"], missing.Select(m => m.Field.FieldName));
        }

        [Fact]
        [DisplayName("Relation, virtual and auto-increment fields are not checked even when marked required")]
        public void FindMissing_NonPersistedOrAutoIncrementFields_AreNotChecked()
        {
            var schema = BuildSchema();
            var fields = schema.MasterTable!.Fields!;
            var relation = fields.Add("ref_cust_name", "Customer Name", FieldDbType.String);
            relation.Type = FieldType.RelationField;
            relation.Required = true;
            fields.Add("seq", "Sequence", FieldDbType.AutoIncrement).Required = true;
            var dataSet = BuildDataSet(schema, "SO-1", Guid.NewGuid());
            dataSet.Tables[Master]!.Rows[0]["ref_cust_name"] = string.Empty;

            Assert.Empty(RequiredFieldCheck.FindMissing(schema, dataSet));
        }

        public static TheoryData<FieldDbType, object?, bool> EmptinessCases => new()
        {
            { FieldDbType.String, null, true },
            { FieldDbType.String, DBNull.Value, true },
            { FieldDbType.String, "", true },
            { FieldDbType.String, " \t", true },
            { FieldDbType.String, "x", false },
            { FieldDbType.String, Guid.Empty.ToString(), false },
            { FieldDbType.Guid, Guid.Empty, true },
            { FieldDbType.Guid, Guid.Empty.ToString(), true },
            { FieldDbType.Guid, Guid.NewGuid(), false },
            { FieldDbType.Binary, Array.Empty<byte>(), true },
            { FieldDbType.Binary, new byte[] { 1 }, false },
            { FieldDbType.Integer, 0, false },
            { FieldDbType.Decimal, 0m, false },
            { FieldDbType.Boolean, false, false },
            { FieldDbType.Date, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), false },
        };

        [Theory]
        // The values are deliberately `object` (a field receives whatever the DataRow holds), so they cannot be
        // serialized into separate data rows at discovery time. Discovery enumeration is therefore disabled (xUnit1045).
        [MemberData(nameof(EmptinessCases), DisableDiscoveryEnumeration = true)]
        [DisplayName("IsEmpty treats null, blank text, an empty Guid (also as text in a Guid field) and an empty byte array as empty, and never a number, boolean or date")]
        public void IsEmpty_Value_MatchesRule(FieldDbType dbType, object? value, bool expected)
        {
            var field = new FormField("f", "F", dbType);

            Assert.Equal(expected, RequiredFieldCheck.IsEmpty(field, value));
        }

        [Fact]
        [DisplayName("The prompt lists each field by caption, a detail field with its table, in the localizer's culture")]
        public void FormatPrompt_ListsCaptionsInCulture()
        {
            using var culture = new CultureScope("zh-TW");
            var schema = BuildSchema();
            var dataSet = BuildDataSet(schema, string.Empty, Guid.NewGuid(), string.Empty);
            var localizer = new LanguageResourceStringLocalizer<PolhemUIText>(
                new FrameworkLanguageService(null, static () => string.Empty));

            string prompt = RequiredFieldCheck.FormatPrompt(localizer, RequiredFieldCheck.FindMissing(schema, dataSet));

            Assert.Equal("請填寫必填欄位：Order No, Product (Order Lines)", prompt);
        }

        [Fact]
        [DisplayName("A field without a caption is named by its field name")]
        public void DisplayText_NoCaption_UsesFieldName()
        {
            var table = new FormTable(Master, string.Empty);
            var field = new FormField("order_no", string.Empty, FieldDbType.String);

            Assert.Equal("order_no", new MissingRequiredField(table, field, isDetail: false).DisplayText);
            Assert.Equal("order_no (Order)", new MissingRequiredField(table, field, isDetail: true).DisplayText);
        }
    }
}
