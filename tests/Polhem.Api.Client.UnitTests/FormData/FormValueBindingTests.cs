using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Base.Data;
using Polhem.Definition.Forms;

namespace Polhem.Api.Client.UnitTests.FormData
{
    /// <summary>
    /// The value conversion rules of <see cref="FormValueBinding"/>.
    /// </summary>
    /// <remarks>
    /// These rules used to exist as private copies in two UI heads, whose docs admitted that "no mechanism keeps the
    /// two consistent", and the two did drift. Now that they are a single shared implementation, this is where they
    /// are verified.
    /// </remarks>
    public class FormValueBindingTests
    {
        private static DataColumn Column(Type type, bool allowDBNull, object? defaultValue = null)
        {
            var table = new DataTable("t");
            var column = new DataColumn("c", type) { AllowDBNull = allowDBNull };
            table.Columns.Add(column);
            if (defaultValue is not null) { column.DefaultValue = defaultValue; }
            return column;
        }

        [Fact]
        [DisplayName("An empty string written to a nullable column becomes DBNull")]
        public void ToColumnValue_EmptyIntoNullableColumn_ReturnsDBNull()
        {
            Assert.Equal(DBNull.Value, FormValueBinding.ToColumnValue(string.Empty, Column(typeof(string), allowDBNull: true)));
        }

        [Fact]
        [DisplayName("An empty string written to a NOT NULL column uses the column's seeded default value")]
        public void ToColumnValue_EmptyIntoNonNullableColumn_UsesSeededDefault()
        {
            var column = Column(typeof(string), allowDBNull: false, defaultValue: "seeded");

            Assert.Equal("seeded", FormValueBinding.ToColumnValue(string.Empty, column));
        }

        [Theory]
        [InlineData(typeof(string), "")]
        [InlineData(typeof(int), 0)]
        [DisplayName("Regression: a NOT NULL column whose DefaultValue is still DBNull does not get DBNull written back")]
        public void ToColumnValue_EmptyIntoNonNullableColumnWithUnseededDefault_ReturnsNonNull(Type type, object expected)
        {
            // The Blazor head's private copy returned DBNull here, and after writing it to a NOT NULL column
            // `EndEdit` threw `NoNullAllowedException`. The Avalonia head had long been fixed, but the other side
            // did not follow. Server responses often carry raw ADO.NET columns (whose `DefaultValue` is still
            // DBNull), so this is not a corner case.
            var column = Column(type, allowDBNull: false);
            Assert.Equal(DBNull.Value, column.DefaultValue); // Precondition: the column really has no seeded default.

            var actual = FormValueBinding.ToColumnValue(string.Empty, column);

            Assert.NotEqual(DBNull.Value, actual);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("string", typeof(string), "string")]
        [DisplayName("A string column is written as is")]
        public void ToColumnValue_String_PassesThrough(string value, Type type, string expected)
        {
            Assert.Equal(expected, FormValueBinding.ToColumnValue(value, Column(type, allowDBNull: true)));
        }

        [Fact]
        [DisplayName("Guid, byte[] and DateTime each use their own parsing path (Convert.ChangeType does not work for the first two)")]
        public void ToColumnValue_NonConvertibleTypes_UseDedicatedParsing()
        {
            var guid = Guid.NewGuid();
            Assert.Equal(guid, FormValueBinding.ToColumnValue(guid.ToString(), Column(typeof(Guid), allowDBNull: true)));

            byte[] bytes = [1, 2, 3];
            Assert.Equal(bytes, FormValueBinding.ToColumnValue(Convert.ToBase64String(bytes), Column(typeof(byte[]), allowDBNull: true)));

            Assert.Equal(
                new DateTime(2026, 9, 4, 13, 5, 0, DateTimeKind.Local),
                FormValueBinding.ToColumnValue("2026-09-04T13:05:00", Column(typeof(DateTime), allowDBNull: true)));
        }

        private static DataColumn TimeColumn()
        {
            var table = new DataTable("t");
            return table.AddColumn("c", FieldDbType.Time);
        }

        [Theory]
        [InlineData("8:30", "08:30")]
        [InlineData("08:30", "08:30")]
        [InlineData("0:05", "00:05")]
        [DisplayName("A time-of-day column is normalized to fixed-width HH:mm on write")]
        public void ToColumnValue_TimeColumn_NormalizesToFixedWidth(string value, string expected)
        {
            // Fixed width makes lexical order equal time order. Stored as is, "8:30" would sort after "10:00".
            Assert.Equal(expected, FormValueBinding.ToColumnValue(value, TimeColumn()));
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("08:99")]
        [InlineData("8:")]
        [InlineData("abc")]
        [DisplayName("A time-of-day column throws FormatException for unparsable input instead of writing it as is or clearing it")]
        public void ToColumnValue_TimeColumnInvalidInput_ThrowsFormatException(string value)
        {
            // A grid cell relies on this exception to keep the previous valid value. Returning an empty string would
            // wipe the data on a single typo.
            Assert.Throws<FormatException>(() => FormValueBinding.ToColumnValue(value, TimeColumn()));
        }

        [Fact]
        [DisplayName("Clearing a time-of-day column writes an empty string (unset), not 00:00")]
        public void ToColumnValue_TimeColumnEmpty_ReturnsUnset()
        {
            Assert.Equal(string.Empty, FormValueBinding.ToColumnValue(string.Empty, TimeColumn()));
        }

        [Fact]
        [DisplayName("A string column without the declared type marker is not normalized as a time of day")]
        public void ToColumnValue_UnmarkedStringColumn_DoesNotNormalizeTime()
        {
            // The decision is based on the column's `FieldDbType` marker, not on the value looking like a time.
            Assert.Equal("8:30", FormValueBinding.ToColumnValue("8:30", Column(typeof(string), allowDBNull: true)));
        }

        [Fact]
        [DisplayName("A date-only value is shown as yyyy-MM-dd, and the T time part is added only when there is a time")]
        public void ToBindingString_DateTime_UsesIso8601ByPrecision()
        {
            Assert.Equal("2026-09-04", FormValueBinding.ToBindingString(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Local)));
            Assert.Equal("2026-09-04T13:05:00", FormValueBinding.ToBindingString(new DateTime(2026, 9, 4, 13, 5, 0, DateTimeKind.Local)));
        }

        [Fact]
        [DisplayName("Numbers are shown with InvariantCulture regardless of the thread culture")]
        public void ToBindingString_Numeric_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                // The de-DE decimal separator is a comma, so without InvariantCulture this would return "1,5".
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("1.5", FormValueBinding.ToBindingString(1.5m));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Fact]
        [DisplayName("null and DBNull are both shown as an empty string")]
        public void ToBindingString_NullAndDBNull_ReturnEmpty()
        {
            Assert.Equal(string.Empty, FormValueBinding.ToBindingString(null));
            Assert.Equal(string.Empty, FormValueBinding.ToBindingString(DBNull.Value));
        }

        [Fact]
        [DisplayName("BuildEmptyDataSet creates an empty table for each schema table")]
        public void BuildEmptyDataSet_CreatesOneTablePerSchemaTable()
        {
            var schema = new FormSchema { ProgId = "test_form" };
            var table = new FormTable { TableName = "master" };
            table.Fields!.Add(new FormField { FieldName = "name", DbType = FieldDbType.String });
            schema.Tables!.Add(table);

            var dataSet = FormValueBinding.BuildEmptyDataSet(schema);

            Assert.Equal("test_form", dataSet.DataSetName);
            var built = Assert.IsType<DataTable>(dataSet.Tables["master"]);
            Assert.True(built.Columns.Contains("name"));
            Assert.Empty(built.Rows);
        }

        [Fact]
        [DisplayName("GetEmptyValue returns non-null for every column type")]
        public void GetEmptyValue_KnownTypes_AreNeverNull()
        {
            Assert.Equal(string.Empty, FormValueBinding.GetEmptyValue(typeof(string)));
            Assert.Equal(Guid.Empty, FormValueBinding.GetEmptyValue(typeof(Guid)));
            Assert.Equal(DateTime.MinValue, FormValueBinding.GetEmptyValue(typeof(DateTime)));
            Assert.Equal(Array.Empty<byte>(), FormValueBinding.GetEmptyValue(typeof(byte[])));
            Assert.Equal(0, FormValueBinding.GetEmptyValue(typeof(int)));
        }
    }
}
