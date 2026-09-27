using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;

namespace Polhem.Base.UnitTests
{
    public class DataRowExtensionsTests
    {
        private static DataRow BuildRow()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("Nullable", typeof(int));
            table.Columns["Nullable"]!.AllowDBNull = true;

            var row = table.NewRow();
            row["Id"] = 5;
            row["Name"] = "Alice";
            row["Amount"] = 12.5m;
            row["Nullable"] = DBNull.Value;
            table.Rows.Add(row);
            return row;
        }

        [Fact]
        [DisplayName("GetFieldValue<T> returns the field value converted to the requested type")]
        public void GetFieldValue_ReturnsTypedValue()
        {
            var row = BuildRow();

            Assert.Equal(5, row.GetFieldValue<int>("Id"));
            Assert.Equal("Alice", row.GetFieldValue<string>("Name"));
            Assert.Equal(12.5m, row.GetFieldValue<decimal>("Amount"));
        }

        [Fact]
        [DisplayName("GetFieldValue returns the type's default value for DBNull")]
        public void GetFieldValue_DbNull_ReturnsDefault()
        {
            var row = BuildRow();
            Assert.Equal(0, row.GetFieldValue<int>("Nullable"));
        }

        [Fact]
        [DisplayName("GetFieldValue throws ArgumentNullException for an empty column name")]
        public void GetFieldValue_EmptyColumnName_Throws()
        {
            var row = BuildRow();
            Assert.Throws<ArgumentNullException>(() => row.GetFieldValue<int>(string.Empty));
        }

        [Fact]
        [DisplayName("GetFieldValue throws InvalidOperationException when the column does not exist")]
        public void GetFieldValue_MissingColumn_Throws()
        {
            var row = BuildRow();
            Assert.Throws<InvalidOperationException>(() => row.GetFieldValue<int>("Missing"));
        }

        [Fact]
        [DisplayName("GetFieldValue throws InvalidOperationException when the value cannot be converted")]
        public void GetFieldValue_InvalidConversion_Throws()
        {
            var row = BuildRow();
            Assert.Throws<InvalidOperationException>(() => row.GetFieldValue<Guid>("Name"));
        }

        [Fact]
        [DisplayName("GetFieldValue(defaultValue) returns the default value when the column does not exist")]
        public void GetFieldValue_WithDefault_MissingColumn_ReturnsDefault()
        {
            var row = BuildRow();
            Assert.Equal(-1, row.GetFieldValue<int>("Missing", -1));
        }

        [Fact]
        [DisplayName("GetFieldValue(defaultValue) throws ArgumentNullException for an empty column name")]
        public void GetFieldValue_WithDefault_EmptyColumnName_Throws()
        {
            var row = BuildRow();
            Assert.Throws<ArgumentNullException>(() => row.GetFieldValue<int>(string.Empty, -1));
        }

        [Fact]
        [DisplayName("GetFieldValue(defaultValue) returns the converted value when the column exists and the value is valid")]
        public void GetFieldValue_WithDefault_ExistingColumn_ReturnsTypedValue()
        {
            var row = BuildRow();
            Assert.Equal(5, row.GetFieldValue<int>("Id", -1));
            Assert.Equal("Alice", row.GetFieldValue<string>("Name", "fallback"));
        }

        [Fact]
        [DisplayName("GetFieldValue(defaultValue) returns the type's default value when the field is DBNull")]
        public void GetFieldValue_WithDefault_DbNull_ReturnsTypeDefault()
        {
            var row = BuildRow();
            Assert.Equal(0, row.GetFieldValue<int>("Nullable", -1));
        }

        [Fact]
        [DisplayName("GetFieldValue(defaultValue) throws InvalidOperationException when the value cannot be converted")]
        public void GetFieldValue_WithDefault_InvalidConversion_Throws()
        {
            var row = BuildRow();
            Assert.Throws<InvalidOperationException>(() => row.GetFieldValue<Guid>("Name", Guid.Empty));
        }
    }

    public class DataTableExtensionsTests
    {
        [Fact]
        [DisplayName("AddColumn with a FieldDbType lowercases the column name and applies the type defaults")]
        public void AddColumn_FieldDbType_AppliesDefaultsAndLowercase()
        {
            var table = new DataTable();
            var col = table.AddColumn("Name", FieldDbType.String);

            Assert.Equal("name", col.ColumnName);
            Assert.Equal(typeof(string), col.DataType);
            Assert.Equal(string.Empty, col.DefaultValue);
            Assert.False(col.AllowDBNull);
        }

        [Theory]
        [InlineData(FieldDbType.Date)]
        [InlineData(FieldDbType.DateTime)]
        [DisplayName("AddColumn sets no default value on date columns, because a clock reading in a column default is stale for every later row")]
        public void AddColumn_DateTypes_HaveNoDefaultValue(FieldDbType dbType)
        {
            // A column default is a single value fixed when the column is created, and it would mask `FormRowDefaults` (ADR-032 D12).
            var table = new DataTable();
            var col = table.AddColumn("stamp", dbType);

            Assert.Equal(DBNull.Value, col.DefaultValue);
            Assert.Equal(DBNull.Value, table.NewRow()["stamp"]);
        }

        [Fact]
        [DisplayName("AddColumn with an explicit default value leaves AllowDBNull false")]
        public void AddColumn_WithExplicitDefault_SetsAllowDbNull()
        {
            var table = new DataTable();
            var col = table.AddColumn("Id", FieldDbType.Integer, 0);

            Assert.Equal("id", col.ColumnName);
            Assert.Equal(0, col.DefaultValue);
            Assert.False(col.AllowDBNull);
        }

        [Fact]
        [DisplayName("AddColumn with a caption argument applies the column caption")]
        public void AddColumn_WithCaption_AppliesCaption()
        {
            var table = new DataTable();
            var col = table.AddColumn("title", "Title", FieldDbType.String, "");
            Assert.Equal("Title", col.Caption);
        }

        [Fact]
        [DisplayName("HasField reports whether the column exists")]
        public void HasField_ReflectsSchema()
        {
            var table = new DataTable();
            table.AddColumn("id", FieldDbType.Integer);

            Assert.True(table.HasField("ID"));
            Assert.False(table.HasField("MISSING"));
        }

        [Fact]
        [DisplayName("IsEmpty returns true or false according to the row count")]
        public void IsEmpty_ReflectsRowCount()
        {
            var table = new DataTable();
            table.AddColumn("id", FieldDbType.Integer);

            Assert.True(table.IsEmpty());

            table.Rows.Add(1);
            Assert.False(table.IsEmpty());
        }

        [Fact]
        [DisplayName("LowercaseColumnNames converts every column name to lowercase")]
        public void LowercaseColumnNames_ConvertsAllColumnsToLowerCase()
        {
            var table = new DataTable();
            table.Columns.Add("NAME", typeof(string));
            table.Columns.Add("Age", typeof(int));

            table.LowercaseColumnNames();

            Assert.Equal("name", table.Columns[0].ColumnName);
            Assert.Equal("age", table.Columns[1].ColumnName);
        }
    }

    public class DataSetExtensionsTests
    {
        [Fact]
        [DisplayName("GetMasterTable returns the table named like the DataSetName")]
        public void GetMasterTable_ReturnsTableNamedLikeDataSet()
        {
            var ds = new DataSet("Orders");
            ds.Tables.Add(new DataTable("Detail"));
            ds.Tables.Add(new DataTable("Orders"));

            var master = ds.GetMasterTable();
            Assert.NotNull(master);
            Assert.Equal("Orders", master!.TableName);
        }

        [Fact]
        [DisplayName("GetMasterTable returns null when no matching table exists")]
        public void GetMasterTable_MissingTable_ReturnsNull()
        {
            var ds = new DataSet("Orders");
            Assert.Null(ds.GetMasterTable());
        }

        [Fact]
        [DisplayName("IsEmpty considers both Tables and the master rows")]
        public void IsEmpty_ConsidersTablesAndMasterRows()
        {
            Assert.True(new DataSet().IsEmpty());

            var ds = new DataSet("Orders");
            ds.Tables.Add(new DataTable("Orders"));
            Assert.True(ds.IsEmpty());

            ds.Tables["Orders"]!.Columns.Add("Id", typeof(int));
            ds.Tables["Orders"]!.Rows.Add(1);
            Assert.False(ds.IsEmpty());
        }
    }

    public class DataViewExtensionsTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable("T");
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(1);
            table.Rows.Add(2);
            table.Rows.Add(3);
            table.AcceptChanges();
            return table;
        }

        [Fact]
        [DisplayName("DeleteRows with acceptChanges removes every row in the view from the table")]
        public void DeleteRows_RemovesAllRowsAndOptionallyAccepts()
        {
            var table = BuildTable();
            var view = new DataView(table);

            view.DeleteRows(acceptChanges: true);

            Assert.Equal(0, table.Rows.Count);
        }

        [Fact]
        [DisplayName("HasField delegates to the underlying DataTable.HasField")]
        public void HasField_DelegatesToTable()
        {
            var view = new DataView(BuildTable());
            Assert.True(view.HasField("Id"));
            Assert.False(view.HasField("Missing"));
        }

        [Fact]
        [DisplayName("IsEmpty reflects DataView.Count")]
        public void IsEmpty_ReflectsRowCount()
        {
            var emptyTable = new DataTable();
            emptyTable.Columns.Add("Id", typeof(int));
            Assert.True(new DataView(emptyTable).IsEmpty());

            Assert.False(new DataView(BuildTable()).IsEmpty());
        }
    }

    public class DataRowViewExtensionsTests
    {
        [Fact]
        [DisplayName("DataRowView.GetFieldValue delegates to the underlying DataRow.GetFieldValue")]
        public void GetFieldValue_DelegatesToRow()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(7);
            var view = new DataView(table);

            Assert.Equal(7, view[0].GetFieldValue<int>("Id"));
            Assert.Equal(-1, view[0].GetFieldValue<int>("Missing", -1));
        }
    }
}
