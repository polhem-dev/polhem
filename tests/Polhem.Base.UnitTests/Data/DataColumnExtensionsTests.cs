using System.ComponentModel;
using System.Data;
using System.Text.Json;
using System.Xml;
using Polhem.Base.Data;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Data
{
    /// <summary>
    /// Verifies the column semantic marker (<see cref="DataColumnExtensions"/>) and how the JSON wire path carries it.
    /// Several FieldDbType values share one CLR type (Date/DateTime → DateTime, Text/String → string,
    /// Currency/Decimal → decimal); the marker exists to restore the information that this mapping flattens.
    /// </summary>
    public class DataColumnExtensionsTests
    {
        // The literal is hard-coded on purpose. The key lands in persisted XML as an msprop name, so renaming it
        // would make existing files unreadable.
        private const string MarkerKey = "Polhem.FieldDbType";

        private static JsonSerializerOptions Options()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new DataTableJsonConverter());
            return opts;
        }

        private static DataSet XmlRoundTrip(DataTable table)
        {
            using var source = new DataSet("ds");
            source.Tables.Add(table);
            using var writer = new StringWriter();
            source.WriteXml(writer, XmlWriteMode.WriteSchema);

            var restored = new DataSet();
            using var stringReader = new StringReader(writer.ToString());
            using var reader = XmlReader.Create(stringReader,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            restored.ReadXml(reader, XmlReadMode.ReadSchema);
            return restored;
        }

        [Fact]
        [DisplayName("ResolveFieldDbType on an unmarked column falls back to inferring from the CLR type")]
        public void ResolveFieldDbType_NoMarker_FallsBackToClrType()
        {
            var column = new DataColumn("d", typeof(DateTime));

            Assert.Null(column.GetDeclaredFieldDbType());
            Assert.Equal(FieldDbType.DateTime, column.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("ResolveFieldDbType on a marked column returns the marker instead of the inferred type")]
        public void ResolveFieldDbType_WithMarker_PrefersMarker()
        {
            var column = new DataColumn("d", typeof(DateTime));
            column.ApplyFieldDbType(FieldDbType.Date);

            Assert.Equal(FieldDbType.Date, column.GetDeclaredFieldDbType());
            Assert.Equal(FieldDbType.Date, column.ResolveFieldDbType());
        }

        [Theory]
        [InlineData(FieldDbType.Date, typeof(DateTime))]
        [InlineData(FieldDbType.Text, typeof(string))]
        [InlineData(FieldDbType.Currency, typeof(decimal))]
        [InlineData(FieldDbType.AutoIncrement, typeof(int))]
        [DisplayName("AddColumn records the marker for FieldDbType values that share a CLR type")]
        public void AddColumn_SharedClrType_RecordsDeclaredType(FieldDbType dbType, Type expectedClrType)
        {
            var table = new DataTable("t");
            var column = table.AddColumn("f", dbType);

            Assert.Equal(expectedClrType, column.DataType);
            Assert.Equal(dbType, column.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("Every FieldDbType overload of AddColumn records the marker")]
        public void AddColumn_AllFieldDbTypeOverloads_RecordDeclaredType()
        {
            var table = new DataTable("t");

            var byType = table.AddColumn("a", FieldDbType.Date);
            var byTypeAndDefault = table.AddColumn("b", FieldDbType.Date, DateTime.Today);
            var byCaption = table.AddColumn("c", "訂單日期", FieldDbType.Date, DateTime.Today);

            Assert.Equal(FieldDbType.Date, byType.ResolveFieldDbType());
            Assert.Equal(FieldDbType.Date, byTypeAndDefault.ResolveFieldDbType());
            Assert.Equal(FieldDbType.Date, byCaption.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("SetDateColumns marks the named columns as Date and leaves the other columns unchanged")]
        public void SetDateColumns_MarksOnlyNamedColumns()
        {
            var table = new DataTable("t");
            table.Columns.Add("order_date", typeof(DateTime));
            table.Columns.Add("created_at", typeof(DateTime));

            table.SetDateColumns("order_date");

            Assert.Equal(FieldDbType.Date, table.Columns["order_date"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.DateTime, table.Columns["created_at"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("SetDateColumns matches column names case-insensitively")]
        public void SetDateColumns_ColumnNameMatchIsCaseInsensitive()
        {
            var table = new DataTable("t");
            table.Columns.Add("order_date", typeof(DateTime));

            table.SetDateColumns("ORDER_DATE");

            Assert.Equal(FieldDbType.Date, table.Columns["order_date"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("SetDateColumns throws for an unknown column name instead of silently ignoring it")]
        public void SetDateColumns_UnknownColumn_Throws()
        {
            var table = new DataTable("t");
            table.Columns.Add("order_date", typeof(DateTime));

            // A typo that looks declared but has no effect is exactly the silent failure the marker exists to remove.
            var ex = Assert.Throws<ArgumentException>(() => table.SetDateColumns("oder_date"));
            Assert.Contains("oder_date", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("JSON round-trip preserves the Date marker instead of falling back to DateTime")]
        public void JsonRoundTrip_PreservesDateMarker()
        {
            var table = new DataTable("t");
            table.AddColumn("order_date", FieldDbType.Date, DateTime.Today);
            table.AddColumn("created_at", FieldDbType.DateTime, DateTime.Now);

            var json = JsonSerializer.Serialize(table, Options());
            var restored = JsonSerializer.Deserialize<DataTable>(json, Options());

            Assert.NotNull(restored);
            Assert.Equal(FieldDbType.Date, restored!.Columns["order_date"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.DateTime, restored.Columns["created_at"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("The type field of the JSON payload is written as Date, not DateTime")]
        public void JsonPayload_TypeFieldCarriesDate()
        {
            var table = new DataTable("t");
            table.AddColumn("order_date", FieldDbType.Date, DateTime.Today);

            var json = JsonSerializer.Serialize(table, Options());

            Assert.Contains("\"type\":\"Date\"", json, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A calendar-date column keeps DateTime as its CLR type after a JSON round-trip")]
        public void JsonRoundTrip_DateColumnStaysDateTimeClrType()
        {
            var table = new DataTable("t");
            table.AddColumn("order_date", FieldDbType.Date, DateTime.Today);
            var row = table.NewRow();
            row["order_date"] = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified);
            table.Rows.Add(row);

            var json = JsonSerializer.Serialize(table, Options());
            var restored = JsonSerializer.Deserialize<DataTable>(json, Options());

            // The marker approach deliberately keeps the CLR type. String write-back, `RowFilter` and `Compute`
            // on a `DataColumn` all depend on `DataType` being `DateTime`, and `DateOnly` would break those paths.
            Assert.Equal(typeof(DateTime), restored!.Columns["order_date"]!.DataType);
            Assert.Equal(new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified), restored.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("The marker is written to ExtendedProperties and the XML schema under the Polhem.FieldDbType key")]
        public void ApplyFieldDbType_WritesMarkerUnderPublishedKey()
        {
            // Writer and reader share one constant, so every other test here passes whatever the key
            // is. The key is persisted, though: the audit log stores it as an msprop attribute.
            var table = new DataTable("t");
            table.Columns.Add("d", typeof(DateTime)).ApplyFieldDbType(FieldDbType.Date);

            Assert.Equal(FieldDbType.Date, table.Columns["d"]!.ExtendedProperties[MarkerKey]);

            using var ds = new DataSet("ds");
            ds.Tables.Add(table);
            using var writer = new StringWriter();
            ds.WriteXmlSchema(writer);
            Assert.Contains("msprop:" + MarkerKey + "=\"Date\"", writer.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("DataSet XML round-trip restores the Date, DateTime and Time markers")]
        public void XmlRoundTrip_PreservesTemporalMarkers()
        {
            var table = new DataTable("t");
            table.AddColumn("hire_date", FieldDbType.Date);
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.AddColumn("work_start", FieldDbType.Time);

            using var restored = XmlRoundTrip(table);
            var columns = restored.Tables["t"]!.Columns;

            // `ReadXml` reads the marker back as a string, not an enum value. An unparsed marker would make the
            // Date column fall back to being inferred as `DateTime`.
            Assert.Equal(FieldDbType.Date, columns["hire_date"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.DateTime, columns["created_at"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.Time, columns["work_start"]!.ResolveFieldDbType());
        }

        [Theory]
        [InlineData("date")]
        [InlineData(" Date")]
        [InlineData("99")]
        [InlineData("3")]
        [InlineData("Date, String")]
        [InlineData("NotAType")]
        [DisplayName("A marker string that is not an exact member name is treated as no marker and falls back to the CLR type")]
        public void GetDeclaredFieldDbType_StringNotExactMemberName_TreatedAsNoMarker(string marker)
        {
            var column = new DataColumn("d", typeof(DateTime));
            column.ExtendedProperties[MarkerKey] = marker;

            Assert.Null(column.GetDeclaredFieldDbType());
            Assert.Equal(FieldDbType.DateTime, column.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("An unmarked DataTable behaves the same after a JSON round-trip")]
        public void JsonRoundTrip_UnmarkedTable_BehaviourUnchanged()
        {
            var table = new DataTable("t");
            table.Columns.Add("created_at", typeof(DateTime));
            table.Columns.Add("name", typeof(string));

            var json = JsonSerializer.Serialize(table, Options());
            var restored = JsonSerializer.Deserialize<DataTable>(json, Options());

            Assert.Equal(FieldDbType.DateTime, restored!.Columns["created_at"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.String, restored.Columns["name"]!.ResolveFieldDbType());
        }
    }
}
