using Polhem.Api.Core.MessagePack;
using Polhem.Definition.Collections;
using Polhem.Definition.Filters;
using System.Data;
using Polhem.Base.Data;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// MessagePack serialization tests.
    /// </summary>
    public class MessagePackTests
    {
        /// <summary>
        /// Tests that MessagePack serializes and deserializes a DataSet correctly.
        /// </summary>
        [Fact(DisplayName = "DataSet round-trips through MessagePack")]
        public void DataSet_Serialize_RoundTrip()
        {
            var dataSet = new DataSet("TestDataSet");

            var table1 = new DataTable("Table1");
            table1.Columns.Add("Name", typeof(string));
            table1.Columns.Add("Age", typeof(int));
            table1.Rows.Add("Alice", 30);
            table1.Rows.Add("Bob", 40);

            var table2 = new DataTable("Table2");
            table2.Columns.Add("Product", typeof(string));
            table2.Columns.Add("Price", typeof(decimal));
            table2.Rows.Add("Pen", 1.5m);
            table2.Rows.Add("Notebook", 3.2m);

            dataSet.Tables.Add(table1);
            dataSet.Tables.Add(table2);

            byte[] serialized = MessagePackCodec.Serialize(dataSet);

            var deserialized = MessagePackCodec.Deserialize<DataSet>(serialized);

            Assert.Equal(2, deserialized.Tables.Count);

            var dt1 = deserialized.Tables["Table1"];
            Assert.NotNull(dt1);
            Assert.Equal(2, dt1.Rows.Count);
            Assert.Equal("Alice", dt1.Rows[0]["Name"]);
            Assert.Equal(30, dt1.Rows[0]["Age"]);
            Assert.Equal("Bob", dt1.Rows[1]["Name"]);
            Assert.Equal(40, dt1.Rows[1]["Age"]);

            var dt2 = deserialized.Tables["Table2"];
            Assert.NotNull(dt2);
            Assert.Equal(2, dt2.Rows.Count);
            Assert.Equal("Pen", dt2.Rows[0]["Product"]);
            Assert.Equal(1.5m, dt2.Rows[0]["Price"]);
            Assert.Equal("Notebook", dt2.Rows[1]["Product"]);
            Assert.Equal(3.2m, dt2.Rows[1]["Price"]);
        }

        /// <summary>
        /// Tests that MessagePack serializes and deserializes a DataTable correctly.
        /// </summary>
        [Fact(DisplayName = "DataTable round-trips through MessagePack")]
        public void DataTable_Serialize_RoundTrip()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("Column1", typeof(string));
            table.Columns.Add("Column2", typeof(int));
            table.Rows.Add("Test1", 100);
            table.Rows.Add("Test2", 200);

            byte[] serialized = MessagePackCodec.Serialize(table);
            var deserialized = MessagePackCodec.Deserialize<DataTable>(serialized);

            Assert.Equal(2, deserialized.Rows.Count);
            Assert.Equal("Test1", deserialized.Rows[0]["Column1"]);
            Assert.Equal(100, deserialized.Rows[0]["Column2"]);
            Assert.Equal("Test2", deserialized.Rows[1]["Column1"]);
            Assert.Equal(200, deserialized.Rows[1]["Column2"]);
        }

        /// <summary>
        /// Tests that a DBNull.Value cell survives the conversion to SerializableDataTable and back.
        /// </summary>
        [Fact(DisplayName = "DataTable conversion through SerializableDataTable preserves DBNull values")]
        public void DataTable_SerializeWithDbNull_PreservesValues()
        {
            // Arrange
            var dt = new DataTable("TestTable");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));

            var row = dt.NewRow();
            row["Id"] = 1;
            row["Name"] = DBNull.Value;
            dt.Rows.Add(row);

            // Act
            var serializable = SerializableDataTable.FromDataTable(dt);
            var restored = SerializableDataTable.ToDataTable(serializable);

            // Assert
            Assert.Equal(1, restored.Rows[0]["Id"]);
            Assert.True(restored.Rows[0].IsNull("Name"));
        }

        /// <summary>
        /// Tests that a DataTable keeps each row's RowState after serialization.
        /// </summary>
        [Fact(DisplayName = "DataTable serialization preserves RowState")]
        public void DataTable_SerializeWithRowState_PreservesState()
        {
            var table = new DataTable("SampleTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));

            var row1 = table.NewRow();
            row1["Id"] = 1;
            row1["Name"] = "資料1";
            table.Rows.Add(row1);

            var row2 = table.NewRow();
            row2["Id"] = 2;
            row2["Name"] = "資料2";
            table.Rows.Add(row2);

            // Accept the first two rows so they become Unchanged before the edits below.
            table.AcceptChanges();

            // RowState becomes Modified.
            table.Rows[0]["Name"] = "修改後資料1";

            // RowState becomes Deleted.
            table.Rows[1].Delete();

            // RowState becomes Added.
            var row3 = table.NewRow();
            row3["Id"] = 3;
            row3["Name"] = "新增資料3";
            table.Rows.Add(row3);

            // Serialize & Deserialize
            var bytes = MessagePackCodec.Serialize(table);
            var restored = MessagePackCodec.Deserialize<DataTable>(bytes);

            if (!DataTableComparer.IsEqual(table, restored))
            {
                Assert.Fail("The DataTable restored after serialization does not equal the original DataTable.");
            }
        }

        /// <summary>
        /// Tests serialization and deserialization of ListItemCollection.
        /// </summary>
        [Fact(DisplayName = "ListItemCollection round-trips through MessagePack")]
        public void TListItemCollection_Serialize_RoundTrip()
        {
            var original = new ListItemCollection()
            {
                new ListItem("A001", "選項一"),
                new ListItem("A002", "選項二"),
                new ListItem("A003", "選項三")
            };

            var bytes = MessagePackCodec.Serialize(original);

            var restored = MessagePackCodec.Deserialize<ListItemCollection>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(original.Count, restored.Count);

            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].Value, restored[i].Value);
                Assert.Equal(original[i].Text, restored[i].Text);
            }
        }

        /// <summary>
        /// Tests that ParameterCollection serializes and deserializes values of several types.
        /// </summary>
        [Fact(DisplayName = "ParameterCollection round-trips values of several types and keeps their types")]
        public void TParameterCollection_Serialize_RoundTrip()
        {
            var original = new ParameterCollection
            {
                { "IntValue", 123 },
                { "StringValue", "測試字串" },
                { "BoolValue", true },
                { "DateTimeValue", new DateTime(2025, 5, 16, 10, 30, 0) },
                { "DecimalValue", 123.45m },
                { "DoubleValue", 9876.54321 },
                { "NullValue", null! }
            };

            var bytes = MessagePackCodec.Serialize(original);

            var restored = MessagePackCodec.Deserialize<ParameterCollection>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(original.Count, restored.Count);

            foreach (var param in original)
            {
                Assert.True(restored.Contains(param.Name));

                var originalValue = param.Value;
                var restoredValue = restored[param.Name].Value;

                if (originalValue == null)
                {
                    Assert.Null(restoredValue);
                }
                else
                {
                    Assert.NotNull(restoredValue);
                    Assert.Equal(originalValue.GetType(), restoredValue!.GetType());
                    Assert.Equal(originalValue, restoredValue);
                }
            }
        }

        /// <summary>
        /// Tests that a ParameterCollection holding a DataTable serializes correctly.
        /// </summary>
        [Fact(DisplayName = "ParameterCollection holding a DataTable round-trips through MessagePack")]
        public void TParameterCollection_SerializeWithDataTable_RoundTrip()
        {
            var table = new DataTable("TestTable");
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, "Alice");
            table.Rows.Add(2, "Bob");

            var parameters = new ParameterCollection
            {
                { "Data", table }
            };

            var bytes = MessagePackCodec.Serialize(parameters);

            var restored = MessagePackCodec.Deserialize<ParameterCollection>(bytes);

            Assert.NotNull(restored);
            Assert.True(restored.Contains("Data"));
            Assert.IsType<DataTable>(restored["Data"].Value);

            var restoredTable = (DataTable)restored["Data"].Value!;
            Assert.Equal("TestTable", restoredTable.TableName);
            Assert.Equal(2, restoredTable.Rows.Count);
            Assert.Equal("Alice", restoredTable.Rows[0]["Name"]);
            Assert.Equal("Bob", restoredTable.Rows[1]["Name"]);
        }

        /// <summary>
        /// Tests that PropertyCollection serializes and restores its property data.
        /// </summary>
        [Fact(DisplayName = "PropertyCollection round-trips through MessagePack")]
        public void TPropertyCollection_Serialize_RoundTrip()
        {
            var properties = new Polhem.Definition.Collections.PropertyCollection
            {
                { "AppName", "PolhemERP" },
                { "Enabled", "true" },
                { "RetryCount", "3" }
            };

            var bytes = MessagePackCodec.Serialize(properties);

            var restored = MessagePackCodec.Deserialize<Polhem.Definition.Collections.PropertyCollection>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(3, restored.Count);
            Assert.Equal("PolhemERP", restored.GetValue("AppName", "DefaultApp"));
            Assert.True(restored.GetValue("Enabled", false));
            Assert.Equal(3, restored.GetValue("RetryCount", 0));

            // Keys that do not exist fall back to the supplied default.
            Assert.Equal("Default", restored.GetValue("NotExist", "Default"));
            Assert.False(restored.GetValue("NotExistBool", false));
            Assert.Equal(999, restored.GetValue("NotExistInt", 999));
        }

        /// <summary>
        /// Tests that a nested filter tree serializes and restores its structure and values.
        /// </summary>
        [Fact(DisplayName = "Nested FilterGroup round-trips through MessagePack")]
        public void Filters_Serialize_RoundTrip()
        {
            var root = FilterGroup.All(
                FilterCondition.Equal("DeptId", 10),
                FilterGroup.Any(
                    FilterCondition.Contains("Name", "Lee"),
                    FilterCondition.Between("HireDate", new DateTime(2024, 1, 1), new DateTime(2024, 12, 31))
                )
            );

            var bytes = MessagePackCodec.Serialize(root);

            var restored = MessagePackCodec.Deserialize<FilterGroup>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(2, restored.Nodes.Count);

            var cond1 = restored.Nodes[0] as FilterCondition;
            Assert.NotNull(cond1);
            Assert.Equal("DeptId", cond1.FieldName);
            Assert.Equal(ComparisonOperator.Equal, cond1.Operator);
            Assert.Equal(10, cond1.Value);

            var group2 = restored.Nodes[1] as FilterGroup;
            Assert.NotNull(group2);
            Assert.Equal(2, group2.Nodes.Count);

            var cond2 = group2.Nodes[0] as FilterCondition;
            Assert.NotNull(cond2);
            Assert.Equal("Name", cond2.FieldName);
            Assert.Equal(ComparisonOperator.Contains, cond2.Operator);
            Assert.Equal("Lee", cond2.Value);

            var cond3 = group2.Nodes[1] as FilterCondition;
            Assert.NotNull(cond3);
            Assert.Equal("HireDate", cond3.FieldName);
            Assert.Equal(ComparisonOperator.Between, cond3.Operator);
            Assert.Equal(new DateTime(2024, 1, 1), cond3.Value);
            Assert.Equal(new DateTime(2024, 12, 31), cond3.SecondValue);
        }

        /// <summary>
        /// Tests serialization of the Ping method's request and response.
        /// </summary>
        [Fact(DisplayName = "Ping request and response round-trip through MessagePack")]
        public void Ping_Serialize_RoundTrip()
        {
            var args = new PingRequest
            {
                ClientName = "TestClient",
                TraceId = Guid.NewGuid().ToString()
            };
            args.Parameters!.Add("Env", "UAT");
            args.Parameters!.Add("Verbose", true);

            TestFunc.TestMessagePackSerialization(args);

            var result = new PingResponse
            {
                Status = "pong",
                ServerTime = new DateTime(2025, 5, 16, 8, 30, 0, DateTimeKind.Utc),
                Version = "1.2.3",
                TraceId = Guid.NewGuid().ToString()
            };
            result.Parameters!.Add("Region", "TW");
            result.Parameters!.Add("Elapsed", 42);

            TestFunc.TestMessagePackSerialization(result);
        }

        /// <summary>
        /// Tests serialization of the ExecFunc method's request and response.
        /// </summary>
        [Fact(DisplayName = "ExecFunc request and response round-trip through MessagePack")]
        public void ExecFunc_Serialize_RoundTrip()
        {
            var args = new ExecFuncRequest
            {
                FuncId = "CustomFunction123"
            };
            args.Parameters!.Add("Key1", "Value1");
            args.Parameters!.Add("Key2", 42);

            TestFunc.TestMessagePackSerialization(args);

            var result = new ExecFuncResponse();
            result.Parameters!.Add("ResultKey", "ResultValue");
            result.Parameters!.Add("ResultCount", 100);
            result.Parameters!.Add("ResultDate", new DateTime(2025, 5, 16, 12, 0, 0, DateTimeKind.Utc));

            TestFunc.TestMessagePackSerialization(result);
        }

        /// <summary>
        /// Tests serialization of the CreateSession method's request and response.
        /// </summary>
        [Fact(DisplayName = "CreateSession request and response round-trip through MessagePack")]
        public void CreateSession_Serialize_RoundTrip()
        {
            // Arrange
            var args = new CreateSessionRequest
            {
                UserID = "TestUser",
                ExpiresIn = 7200,
                OneTime = true
            };

            // Act & Assert
            TestFunc.TestMessagePackSerialization(args);

            // Arrange
            var result = new CreateSessionResponse
            {
                AccessToken = Guid.NewGuid(),
                ExpiredAt = new DateTime(2025, 5, 16, 12, 0, 0, DateTimeKind.Utc)
            };

            // Act & Assert
            TestFunc.TestMessagePackSerialization(result);
        }

        /// <summary>
        /// Tests serialization of the GetDefine method's request and response.
        /// </summary>
        [Fact(DisplayName = "GetDefine request and response round-trip through MessagePack")]
        public void GetDefine_Serialize_RoundTrip()
        {
            // Arrange
            var args = new GetDefineRequest
            {
                DefineType = DefineType.FormSchema,
                Keys = new[] { "Key1", "Key2", "Key3" }
            };

            // Act & Assert
            TestFunc.TestMessagePackSerialization(args);

            // Arrange
            var result = new GetDefineResponse
            {
                Xml = "<Define><Item Key='Key1'>Value1</Item></Define>"
            };

            // Act & Assert
            TestFunc.TestMessagePackSerialization(result);
        }

        /// <summary>
        /// Tests serialization of the GetCommonConfiguration method's request and response.
        /// </summary>
        [Fact(DisplayName = "GetCommonConfiguration request and response round-trip through MessagePack")]
        public void GetCommonConfiguration_Serialize_RoundTrip()
        {
            // Arrange: the request has no properties of its own, only the inherited `Parameters`.
            var args = new GetCommonConfigurationRequest();
            args.Parameters!.Add("AppId", "PolhemERP");
            args.Parameters!.Add("Env", "Production");

            // Act & Assert
            TestFunc.TestMessagePackSerialization(args);

            // Arrange
            var result = new GetCommonConfigurationResponse
            {
                CommonConfiguration = "<Config><Setting Key='Theme'>Dark</Setting></Config>"
            };
            result.Parameters!.Add("CacheHit", true);

            // Act & Assert
            TestFunc.TestMessagePackSerialization(result);
        }

        /// <summary>
        /// Tests serialization of the SaveDefine method's request and response.
        /// </summary>
        [Fact(DisplayName = "SaveDefine request and response round-trip through MessagePack")]
        public void SaveDefine_Serialize_RoundTrip()
        {
            // Arrange
            var args = new SaveDefineRequest
            {
                DefineType = DefineType.FormSchema,
                Xml = "<Define><Item Key='OrderForm'>FormData</Item></Define>",
                Keys = new[] { "OrderForm", "CustomerForm" }
            };
            args.Parameters!.Add("UserId", "admin");

            // Act & Assert
            TestFunc.TestMessagePackSerialization(args);

            // Arrange: the response has no properties of its own, only the inherited `Parameters`.
            var result = new SaveDefineResponse();
            result.Parameters!.Add("AffectedRows", 2);
            result.Parameters!.Add("Timestamp", new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc));

            // Act & Assert
            TestFunc.TestMessagePackSerialization(result);
        }
    }
}

