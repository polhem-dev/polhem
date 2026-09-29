using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Core.Serialization;
using Polhem.Tests.Shared;

namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// JSON serialization tests for DataSet and DataTable.
    /// Verifies round-trips of the custom DataTableJsonConverter / DataSetJsonConverter through JsonCodec,
    /// covering every FieldDbType column type, RowState preservation, DBNull handling, DataRelation, PrimaryKey
    /// and edge cases. They serve as the regression acceptance criteria for the System.Text.Json converters.
    /// </summary>
    public class JsonDataSetSerializationTests
    {
        #region Helper

        /// <summary>
        /// Round-trips a DataTable through JSON with JsonCodec.
        /// </summary>
        private static DataTable JsonRoundTripTable(DataTable table)
        {
            string json = JsonCodec.Serialize(table);
            return JsonCodec.Deserialize<DataTable>(json)!;
        }

        /// <summary>
        /// Round-trips a DataSet through JSON with JsonCodec.
        /// </summary>
        private static DataSet JsonRoundTripDataSet(DataSet dataSet)
        {
            string json = JsonCodec.Serialize(dataSet);
            return JsonCodec.Deserialize<DataSet>(json)!;
        }

        #endregion

        #region 1. DataTable basic serialization

        /// <summary>
        /// Tests a basic DataTable round-trip: TableName, column count, row count and values are restored.
        /// </summary>
        [Fact]
        [DisplayName("DataTable round-trips through JSON serialization")]
        public void DataTable_JsonSerialize_RoundTrip()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Age", typeof(int));
            table.Rows.Add("Alice", 30);
            table.Rows.Add("Bob", 40);

            var restored = JsonRoundTripTable(table);

            Assert.Equal("TestTable", restored.TableName);
            Assert.Equal(2, restored.Columns.Count);
            Assert.Equal(2, restored.Rows.Count);
            Assert.Equal("Alice", restored.Rows[0]["Name"]);
            Assert.Equal(30, restored.Rows[0]["Age"]);
            Assert.Equal("Bob", restored.Rows[1]["Name"]);
            Assert.Equal(40, restored.Rows[1]["Age"]);
        }

        /// <summary>
        /// Tests serializing a DataTable with a DBNull value; IsNull is true after restoring.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization preserves DBNull values")]
        public void DataTable_JsonSerializeWithDbNull_PreservesValues()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));

            var row = table.NewRow();
            row["Id"] = 1;
            row["Name"] = DBNull.Value;
            table.Rows.Add(row);

            var restored = JsonRoundTripTable(table);

            Assert.Equal(1, restored.Rows[0]["Id"]);
            Assert.True(restored.Rows[0].IsNull("Name"));
        }

        /// <summary>
        /// Tests that DataTable serialization preserves the Added, Modified, Deleted and Unchanged row states.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization preserves RowState")]
        public void DataTable_JsonSerializeWithRowState_PreservesState()
        {
            var table = new DataTable("SampleTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));

            // Unchanged
            table.Rows.Add(1, "資料1");
            table.Rows.Add(2, "資料2");
            table.AcceptChanges();

            // Modified
            table.Rows[0]["Name"] = "修改後資料1";

            // Deleted
            table.Rows[1].Delete();

            // Added
            table.Rows.Add(3, "新增資料3");

            var restored = JsonRoundTripTable(table);

            Assert.True(DataTableComparer.IsEqual(table, restored),
                "The restored DataTable is not equal to the original DataTable");
        }

        #endregion

        #region 2. Coverage of every FieldDbType

        // The test data mixes several .NET types (string, int, DateTime, Guid, byte[] and others), so the value
        // type argument of `TheoryData` can only be `object`. The xUnit1045 warning does not apply to this deliberate design.
#pragma warning disable xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
        /// <summary>
        /// Provides test data for every FieldDbType.
        /// </summary>
        public static TheoryData<string, Type, object> AllFieldDbTypeTestData()
        {
            return new TheoryData<string, Type, object>
            {
                { "StringCol", typeof(string), "Hello 測試" },
                { "TextCol", typeof(string), "Long text content with 中文字" },
                { "BoolCol", typeof(bool), true },
                { "AutoIncCol", typeof(int), 42 },
                { "ShortCol", typeof(short), (short)12345 },
                { "IntCol", typeof(int), int.MaxValue },
                { "LongCol", typeof(long), long.MaxValue },
                { "DecimalCol", typeof(decimal), 123456.789m },
                { "CurrencyCol", typeof(decimal), 99999.99m },
                { "DateCol", typeof(DateTime), new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Unspecified) },
                { "DateTimeCol", typeof(DateTime), new DateTime(2026, 4, 15, 10, 30, 45, DateTimeKind.Unspecified) },
                { "GuidCol", typeof(Guid), new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890") },
                { "BinaryCol", typeof(byte[]), new byte[] { 0x01, 0x02, 0xAB, 0xFF } },
            };
        }

        /// <summary>
        /// Tests that the .NET type of every FieldDbType round-trips correctly.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllFieldDbTypeTestData))]
        [DisplayName("DataTable JSON serialization supports every FieldDbType column type")]
        public void DataTable_JsonSerialize_AllFieldDbTypes(string columnName, Type columnType, object testValue)
        {
            var table = new DataTable("TypeTestTable");
            table.Columns.Add(columnName, columnType);

            var row = table.NewRow();
            row[columnName] = testValue;
            table.Rows.Add(row);

            var restored = JsonRoundTripTable(table);

            Assert.Equal(1, restored.Rows.Count);
            Assert.Equal(columnType, restored.Columns[columnName]!.DataType);

            var restoredValue = restored.Rows[0][columnName];

            if (testValue is byte[] expectedBytes)
            {
                Assert.IsType<byte[]>(restoredValue);
                Assert.Equal(expectedBytes, (byte[])restoredValue);
            }
            else
            {
                Assert.Equal(testValue, restoredValue);
            }
        }
#pragma warning restore xUnit1045

        #endregion

        #region 3. DataSet with multiple tables and relations

        /// <summary>
        /// Tests a round-trip of a DataSet that contains several DataTables.
        /// </summary>
        [Fact]
        [DisplayName("DataSet round-trips through JSON serialization")]
        public void DataSet_JsonSerialize_RoundTrip()
        {
            var dataSet = new DataSet("TestDataSet");

            var table1 = new DataTable("Orders");
            table1.Columns.Add("OrderId", typeof(int));
            table1.Columns.Add("Customer", typeof(string));
            table1.Rows.Add(1, "Alice");
            table1.Rows.Add(2, "Bob");

            var table2 = new DataTable("Products");
            table2.Columns.Add("ProductId", typeof(int));
            table2.Columns.Add("Price", typeof(decimal));
            table2.Rows.Add(101, 19.99m);
            table2.Rows.Add(102, 45.50m);

            dataSet.Tables.Add(table1);
            dataSet.Tables.Add(table2);

            var restored = JsonRoundTripDataSet(dataSet);

            Assert.Equal("TestDataSet", restored.DataSetName);
            Assert.Equal(2, restored.Tables.Count);

            var rt1 = restored.Tables["Orders"];
            Assert.NotNull(rt1);
            Assert.Equal(2, rt1.Rows.Count);
            Assert.Equal("Alice", rt1.Rows[0]["Customer"]);
            Assert.Equal(2, rt1.Rows[1]["OrderId"]);

            var rt2 = restored.Tables["Products"];
            Assert.NotNull(rt2);
            Assert.Equal(2, rt2.Rows.Count);
            Assert.Equal(19.99m, rt2.Rows[0]["Price"]);
            Assert.Equal(45.50m, rt2.Rows[1]["Price"]);
        }

        /// <summary>
        /// Tests that serialization preserves a master-detail DataRelation in a DataSet.
        /// </summary>
        [Fact]
        [DisplayName("DataSet JSON serialization preserves DataRelation")]
        public void DataSet_JsonSerializeWithRelation_PreservesRelation()
        {
            var dataSet = new DataSet("OrderSystem");

            var master = new DataTable("Order");
            master.Columns.Add("OrderId", typeof(int));
            master.Columns.Add("Customer", typeof(string));
            master.PrimaryKey = new[] { master.Columns["OrderId"]! };
            master.Rows.Add(1, "Alice");

            var detail = new DataTable("OrderDetail");
            detail.Columns.Add("DetailId", typeof(int));
            detail.Columns.Add("OrderId", typeof(int));
            detail.Columns.Add("Product", typeof(string));
            detail.Rows.Add(10, 1, "Pen");
            detail.Rows.Add(11, 1, "Notebook");

            dataSet.Tables.Add(master);
            dataSet.Tables.Add(detail);
            dataSet.Relations.Add("Order_Detail",
                master.Columns["OrderId"]!,
                detail.Columns["OrderId"]!);

            var restored = JsonRoundTripDataSet(dataSet);

            Assert.Single(restored.Relations);
            var rel = restored.Relations[0];
            Assert.Equal("Order_Detail", rel.RelationName);
            Assert.Equal("Order", rel.ParentTable.TableName);
            Assert.Equal("OrderDetail", rel.ChildTable.TableName);
            Assert.Equal("OrderId", rel.ParentColumns[0].ColumnName);
            Assert.Equal("OrderId", rel.ChildColumns[0].ColumnName);
        }

        #endregion

        #region 4. Column metadata preservation

        /// <summary>
        /// Tests that DataTable serialization preserves column metadata (AllowDBNull, ReadOnly, MaxLength, Caption).
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization preserves column metadata")]
        public void DataTable_JsonSerialize_PreservesColumnMetadata()
        {
            var table = new DataTable("MetaTable");

            var col1 = new DataColumn("Code", typeof(string))
            {
                AllowDBNull = false,
                MaxLength = 20,
                Caption = "代碼"
            };

            var col2 = new DataColumn("Amount", typeof(decimal))
            {
                ReadOnly = true,
                Caption = "金額"
            };

            table.Columns.Add(col1);
            table.Columns.Add(col2);
            table.Rows.Add("A001", 100.50m);

            var restored = JsonRoundTripTable(table);

            var rc1 = restored.Columns["Code"]!;
            Assert.False(rc1.AllowDBNull);
            Assert.Equal(20, rc1.MaxLength);
            Assert.Equal("代碼", rc1.Caption);

            var rc2 = restored.Columns["Amount"]!;
            Assert.True(rc2.ReadOnly);
            Assert.Equal("金額", rc2.Caption);
        }

        /// <summary>
        /// Tests that DataTable serialization preserves the PrimaryKey.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization preserves PrimaryKey")]
        public void DataTable_JsonSerialize_PreservesPrimaryKey()
        {
            var table = new DataTable("PkTable");
            table.Columns.Add("CompanyId", typeof(string));
            table.Columns.Add("DeptId", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.PrimaryKey = new[]
            {
                table.Columns["CompanyId"]!,
                table.Columns["DeptId"]!
            };
            table.Rows.Add("C01", "D01", "研發部");

            var restored = JsonRoundTripTable(table);

            Assert.Equal(2, restored.PrimaryKey.Length);
            Assert.Equal("CompanyId", restored.PrimaryKey[0].ColumnName);
            Assert.Equal("DeptId", restored.PrimaryKey[1].ColumnName);
        }

        #endregion

        #region 5. RowState details

        /// <summary>
        /// Tests that a Modified row keeps its Original and Current values after serialization.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization keeps the original values of a Modified row")]
        public void DataTable_JsonSerialize_ModifiedRow_PreservesOriginalValues()
        {
            var table = new DataTable("ModifiedTest");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "原始值");
            table.AcceptChanges();

            table.Rows[0]["Name"] = "修改後";

            var restored = JsonRoundTripTable(table);

            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal("修改後", row["Name", DataRowVersion.Current]);
            Assert.Equal("原始值", row["Name", DataRowVersion.Original]);
            Assert.Equal(1, row["Id", DataRowVersion.Current]);
        }

        /// <summary>
        /// Tests that a Deleted row keeps its Original values after serialization.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization keeps the original values of a Deleted row")]
        public void DataTable_JsonSerialize_DeletedRow_PreservesOriginalValues()
        {
            var table = new DataTable("DeletedTest");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "待刪除");
            table.AcceptChanges();

            table.Rows[0].Delete();

            var restored = JsonRoundTripTable(table);

            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Deleted, row.RowState);
            Assert.Equal(1, row["Id", DataRowVersion.Original]);
            Assert.Equal("待刪除", row["Name", DataRowVersion.Original]);
        }

        #endregion

        #region 6. Edge cases

        /// <summary>
        /// Tests serializing an empty DataTable that has columns but no rows.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization round-trips an empty table")]
        public void DataTable_JsonSerializeEmptyTable_RoundTrip()
        {
            var table = new DataTable("EmptyTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));

            var restored = JsonRoundTripTable(table);

            Assert.Equal("EmptyTable", restored.TableName);
            Assert.Equal(2, restored.Columns.Count);
            Assert.Equal(0, restored.Rows.Count);
        }

        /// <summary>
        /// Tests serializing a row whose fields are all DBNull.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization round-trips a row of all nulls")]
        public void DataTable_JsonSerializeAllNullRow_RoundTrip()
        {
            var table = new DataTable("NullTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));

            var row = table.NewRow();
            // Every field is left as DBNull.
            table.Rows.Add(row);

            var restored = JsonRoundTripTable(table);

            Assert.Equal(1, restored.Rows.Count);
            Assert.True(restored.Rows[0].IsNull("Id"));
            Assert.True(restored.Rows[0].IsNull("Name"));
            Assert.True(restored.Rows[0].IsNull("Amount"));
        }

        /// <summary>
        /// Tests serializing an empty DataSet with no DataTable.
        /// </summary>
        [Fact]
        [DisplayName("DataSet JSON serialization round-trips an empty DataSet")]
        public void DataSet_JsonSerializeEmptyDataSet_RoundTrip()
        {
            var dataSet = new DataSet("EmptySet");

            var restored = JsonRoundTripDataSet(dataSet);

            Assert.Equal("EmptySet", restored.DataSetName);
            Assert.Empty(restored.Tables);
        }

        /// <summary>
        /// Tests that a null DataTable is restored as null after serialization.
        /// </summary>
        [Fact]
        [DisplayName("DataTable JSON serialization restores a null DataTable as null")]
        public void DataTable_JsonSerialize_Null_ReturnsNull()
        {
            string json = JsonCodec.Serialize((DataTable?)null!);
            var restored = JsonCodec.Deserialize<DataTable?>(json);

            Assert.Null(restored);
        }

        [Fact]
        [DisplayName("A row value key whose casing differs from its column definition is still converted with the column's type")]
        public void DataTable_JsonDeserialize_ValueKeyCasingDiffers_ConvertsWithColumnType()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("Amount", typeof(decimal));
            table.Rows.Add(1234.5m);
            string json = JsonCodec.Serialize(table);
            string rewritten = json.Replace("\"current\":{\"Amount\"", "\"current\":{\"AMOUNT\"", StringComparison.Ordinal);
            Assert.NotEqual(json, rewritten);

            // A culture whose decimal separator is a comma: without the column's type the quoted value would
            // be converted by `DataTable` under this culture instead of parsed as an invariant decimal.
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                var restored = JsonCodec.Deserialize<DataTable>(rewritten)!;

                Assert.Equal(1234.5m, restored.Rows[0]["Amount"]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        #endregion
    }
}
