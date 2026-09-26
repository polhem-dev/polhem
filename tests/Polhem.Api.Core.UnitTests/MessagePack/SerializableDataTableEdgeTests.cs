using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Edge case and error path tests for SerializableDataTable, covering a DBNull default becoming null, primary
    /// keys that are empty or name missing columns, and the row state and original / current values of rows after a
    /// round-trip.
    /// </summary>
    public class SerializableDataTableEdgeTests
    {
        [Fact]
        [DisplayName("FromDataTable turns a column's DBNull default into null")]
        public void FromDataTable_ColumnDBNullDefault_BecomesNull()
        {
            var dt = new DataTable("T");
            var col = new DataColumn("Name", typeof(string));
            Assert.Equal(DBNull.Value, col.DefaultValue);
            dt.Columns.Add(col);

            var sdt = SerializableDataTable.FromDataTable(dt);
            Assert.Single(sdt.Columns);
            Assert.Null(sdt.Columns[0].DefaultValue);
        }

        [Fact]
        [DisplayName("FromDataTable preserves a concrete column default")]
        public void FromDataTable_ColumnConcreteDefault_IsPreserved()
        {
            var dt = new DataTable("T");
            var col = new DataColumn("Age", typeof(int)) { DefaultValue = 42 };
            dt.Columns.Add(col);

            var sdt = SerializableDataTable.FromDataTable(dt);
            Assert.Equal(42, sdt.Columns[0].DefaultValue);

            var restored = SerializableDataTable.ToDataTable(sdt);
            Assert.Equal(42, restored.Columns["Age"]!.DefaultValue);
        }

        [Fact]
        [DisplayName("ToDataTable sets no PrimaryKey when the primary key list is empty")]
        public void ToDataTable_EmptyPrimaryKeys_NoPrimaryKeyApplied()
        {
            var sdt = new SerializableDataTable { TableName = "T" };
            sdt.Columns.Add(new SerializableDataColumn
            {
                ColumnName = "Id",
                DataType = Polhem.Base.Data.FieldDbType.Integer,
                AllowDBNull = true,
                ReadOnly = false,
                MaxLength = -1
            });

            var dt = SerializableDataTable.ToDataTable(sdt);
            Assert.Empty(dt.PrimaryKey);
        }

        [Fact]
        [DisplayName("ToDataTable filters out a primary key that names a missing column, leaving none")]
        public void ToDataTable_PrimaryKeyWithMissingColumn_IsFilteredOut()
        {
            var sdt = new SerializableDataTable { TableName = "T" };
            sdt.Columns.Add(new SerializableDataColumn
            {
                ColumnName = "Id",
                DataType = Polhem.Base.Data.FieldDbType.Integer,
                AllowDBNull = true,
                ReadOnly = false,
                MaxLength = -1
            });
            sdt.PrimaryKeys.Add("Ghost");

            var dt = SerializableDataTable.ToDataTable(sdt);
            Assert.Empty(dt.PrimaryKey);
        }

        [Fact]
        [DisplayName("ToDataTable applies only the existing columns when the primary key mixes existing and missing columns")]
        public void ToDataTable_PrimaryKeyMixedValidAndMissing_AppliesOnlyValid()
        {
            var sdt = new SerializableDataTable { TableName = "T" };
            sdt.Columns.Add(new SerializableDataColumn
            {
                ColumnName = "Id",
                DataType = Polhem.Base.Data.FieldDbType.Integer,
                AllowDBNull = true,
                ReadOnly = false,
                MaxLength = -1
            });
            sdt.PrimaryKeys.Add("Id");
            sdt.PrimaryKeys.Add("Ghost");

            var dt = SerializableDataTable.ToDataTable(sdt);
            Assert.Single(dt.PrimaryKey);
            Assert.Equal("Id", dt.PrimaryKey[0].ColumnName);
        }

        [Fact]
        [DisplayName("FromDataTable then ToDataTable preserves the Original and Current values of a Modified row")]
        public void RoundTrip_ModifiedRow_PreservesOriginalAndCurrent()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "原始");
            dt.AcceptChanges();
            dt.Rows[0]["Name"] = "修改後";

            var restored = SerializableDataTable.ToDataTable(SerializableDataTable.FromDataTable(dt));
            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal("原始", row["Name", DataRowVersion.Original]);
            Assert.Equal("修改後", row["Name", DataRowVersion.Current]);
        }

        [Fact]
        [DisplayName("FromDataTable then ToDataTable restores a Deleted row as Deleted and preserves its Original values")]
        public void RoundTrip_DeletedRow_PreservesOriginal()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(1, "待刪除");
            dt.AcceptChanges();
            dt.Rows[0].Delete();

            var restored = SerializableDataTable.ToDataTable(SerializableDataTable.FromDataTable(dt));
            var row = restored.Rows[0];
            Assert.Equal(DataRowState.Deleted, row.RowState);
            Assert.Equal(1, row["Id", DataRowVersion.Original]);
            Assert.Equal("待刪除", row["Name", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("FromDataTable then ToDataTable keeps an Unchanged row Unchanged")]
        public void RoundTrip_UnchangedRow_PreservesState()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Rows.Add(1);
            dt.AcceptChanges();

            var restored = SerializableDataTable.ToDataTable(SerializableDataTable.FromDataTable(dt));
            Assert.Equal(DataRowState.Unchanged, restored.Rows[0].RowState);
        }
    }
}
