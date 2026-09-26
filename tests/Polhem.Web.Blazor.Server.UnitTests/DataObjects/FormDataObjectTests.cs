using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// Verifies that <see cref="FormDataObject"/> derives the correct <see cref="DataSet"/>
    /// shape from <see cref="FormSchema"/>, round-trips values through
    /// <see cref="FormDataObject.GetField"/> / <see cref="FormDataObject.SetField"/>, and
    /// drives the four async server methods through a supplied
    /// <see cref="FormApiConnector"/>.
    /// </summary>
    public class FormDataObjectTests
    {
        private const string TestProgId = "Employee";

        private static FormSchema BuildEmployeeSchema()
        {
            var schema = new FormSchema(TestProgId, TestProgId);
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("emp_id", "Employee ID", FieldDbType.String);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);
            master.Fields.Add("salary", "Salary", FieldDbType.Decimal);
            master.Fields.Add("manager_rowid", "Manager", FieldDbType.Long);

            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);

            return schema;
        }

        private static DataSet BuildServerDataSet(Guid rowId, string empName)
        {
            var dataSet = new DataSet(TestProgId);
            var master = new DataTable(TestProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("emp_name", typeof(string));
            master.Rows.Add(rowId, empName);
            dataSet.Tables.Add(master);
            dataSet.AcceptChanges();
            return dataSet;
        }

        [Fact]
        [DisplayName("The constructor derives the matching DataSet and columns from FormSchema")]
        public void Constructor_FromSchema_BuildsExpectedDataSetShape()
        {
            var schema = BuildEmployeeSchema();
            var dataObject = new FormDataObject(schema);

            Assert.Equal(TestProgId, dataObject.DataSet.DataSetName);
            Assert.Equal(2, dataObject.DataSet.Tables.Count);

            var master = dataObject.MasterTable;
            Assert.Equal(TestProgId, master.TableName);
            Assert.Equal(7, master.Columns.Count);
            Assert.True(master.Columns.Contains("emp_id"));
            Assert.True(master.Columns.Contains("hire_date"));
            Assert.Equal(typeof(DateTime), master.Columns["hire_date"]!.DataType);
            Assert.Equal(typeof(bool), master.Columns["is_active"]!.DataType);
            Assert.Equal(typeof(decimal), master.Columns["salary"]!.DataType);

            var details = dataObject.DetailTables.ToList();
            Assert.Single(details);
            Assert.Equal("EmployeePhone", details[0].TableName);
        }

        [Fact]
        [DisplayName("MasterRow is initially null and one empty row exists after InitializeNewMaster")]
        public void InitializeNewMaster_AddsSingleEmptyRow()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());

            Assert.Null(dataObject.MasterRow);

            dataObject.InitializeNewMaster();

            Assert.NotNull(dataObject.MasterRow);
            Assert.Equal(1, dataObject.MasterTable.Rows.Count);
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("GetField returns an empty string when there is no MasterRow")]
        public void GetField_NoMasterRow_ReturnsEmpty()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());

            Assert.Equal(string.Empty, dataObject.GetField("emp_id"));
        }

        [Fact]
        [DisplayName("SetField is a no-op when there is no MasterRow")]
        public void SetField_NoMasterRow_IsNoOp()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());

            dataObject.SetField("emp_id", "E001");

            Assert.Null(dataObject.MasterRow);
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("GetField reads back the same value after SetField writes a string field")]
        public void SetField_String_RoundTripsThroughGetField()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            dataObject.SetField("emp_name", "Alice");

            Assert.Equal("Alice", dataObject.GetField("emp_name"));
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("GetField returns a True/False string after SetField writes a Boolean field")]
        public void SetField_Boolean_RoundTripsThroughGetField()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            dataObject.SetField("is_active", "True");
            Assert.Equal("True", dataObject.GetField("is_active"));
            Assert.True((bool)dataObject.MasterRow!["is_active"]);

            dataObject.SetField("is_active", "False");
            Assert.Equal("False", dataObject.GetField("is_active"));
            Assert.False((bool)dataObject.MasterRow["is_active"]);
        }

        [Fact]
        [DisplayName("GetField returns ISO yyyy-MM-dd format after SetField writes a Date field")]
        public void SetField_Date_RoundTripsAsIsoDate()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            dataObject.SetField("hire_date", "2026-05-21");

            Assert.Equal("2026-05-21", dataObject.GetField("hire_date"));
            var stored = (DateTime)dataObject.MasterRow!["hire_date"];
            Assert.Equal(new DateTime(2026, 5, 21), stored);
        }

        [Fact]
        [DisplayName("GetField returns invariant format after SetField writes a Decimal field")]
        public void SetField_Decimal_UsesInvariantFormatting()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            dataObject.SetField("salary", "1234.56");

            Assert.Equal("1234.56", dataObject.GetField("salary"));
            Assert.Equal(1234.56m, dataObject.MasterRow!["salary"]);
        }

        [Fact]
        [DisplayName("SetField with an empty string sets a column that allows DBNull to DBNull")]
        public void SetField_EmptyString_OnNullableColumn_SetsDbNull()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();
            dataObject.SetField("manager_rowid", "42");
            Assert.Equal(42L, dataObject.MasterRow!["manager_rowid"]);

            dataObject.SetField("manager_rowid", string.Empty);

            Assert.Equal(DBNull.Value, dataObject.MasterRow["manager_rowid"]);
            Assert.Equal(string.Empty, dataObject.GetField("manager_rowid"));
        }

        [Fact]
        [DisplayName("SetField with an empty string falls back to the column default for a NOT NULL column")]
        public void SetField_EmptyString_OnNotNullColumn_FallsBackToDefault()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_name", "Bob");

            dataObject.SetField("emp_name", string.Empty);

            // FieldDbType.String defaults to string.Empty, so AllowDBNull is false and
            // the column reverts to the empty-string default rather than DBNull.
            Assert.Equal(string.Empty, dataObject.MasterRow!["emp_name"]);
            Assert.Equal(string.Empty, dataObject.GetField("emp_name"));
        }

        [Fact]
        [DisplayName("GetField/SetField do not throw when the column does not exist")]
        public void GetField_AndSetField_UnknownColumn_AreTolerated()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            Assert.Equal(string.Empty, dataObject.GetField("not_a_column"));

            var exception = Record.Exception(() => dataObject.SetField("not_a_column", "x"));
            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("GetFormField returns the FormField metadata of the master table")]
        public void GetFormField_ReturnsMasterFieldMetadata()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());

            var field = dataObject.GetFormField("emp_id");
            Assert.NotNull(field);
            Assert.Equal("Employee ID", field!.Caption);

            Assert.Null(dataObject.GetFormField("not_a_field"));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException when schema is null")]
        public void Constructor_NullSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FormDataObject(null!));
        }

        [Fact]
        [DisplayName("The constructor throws ArgumentException when ProgId is an empty string")]
        public void Constructor_EmptyProgId_Throws()
        {
            var schema = new FormSchema();
            Assert.Throws<ArgumentException>(() => new FormDataObject(schema));
        }

        // --- Phase 1b: server round-trip via FormApiConnector ---

        [Fact]
        [DisplayName("LoadAsync throws InvalidOperationException when there is no connector")]
        public async Task LoadAsync_NoConnector_Throws()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.LoadAsync(Guid.NewGuid()));
        }

        [Fact]
        [DisplayName("LoadAsync on success replaces the local DataSet with the server's and resets IsDirty")]
        public async Task LoadAsync_Success_ReplacesDataSetAndResetsDirty()
        {
            var schema = BuildEmployeeSchema();
            var rowId = Guid.NewGuid();
            var serverDataSet = BuildServerDataSet(rowId, "Alice");
            var connector = new FakeFormApiConnector
            {
                GetDataHandler = id => new GetDataResponse { DataSet = id == rowId ? serverDataSet : null },
            };
            var dataObject = new FormDataObject(schema, connector);
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_name", "stale");
            Assert.True(dataObject.IsDirty);

            await dataObject.LoadAsync(rowId);

            Assert.Same(serverDataSet, dataObject.DataSet);
            Assert.NotNull(dataObject.MasterRow);
            Assert.Equal(rowId, dataObject.MasterRow!["sys_rowid"]);
            Assert.Equal("Alice", dataObject.GetField("emp_name"));
            Assert.False(dataObject.IsDirty);
            Assert.False(dataObject.IsLoading);
        }

        [Fact]
        [DisplayName("LoadAsync throws InvalidOperationException when the server returns a null DataSet")]
        public async Task LoadAsync_NotFound_Throws()
        {
            var connector = new FakeFormApiConnector
            {
                GetDataHandler = _ => new GetDataResponse { DataSet = null },
            };
            var dataObject = new FormDataObject(BuildEmployeeSchema(), connector);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.LoadAsync(Guid.NewGuid()));
            Assert.False(dataObject.IsLoading);
        }

        [Fact]
        [DisplayName("NewAsync on success replaces the local DataSet with the server skeleton and resets IsDirty")]
        public async Task NewAsync_Success_ReplacesDataSetAndResetsDirty()
        {
            var rowId = Guid.NewGuid();
            var serverDataSet = BuildServerDataSet(rowId, string.Empty);
            var connector = new FakeFormApiConnector
            {
                GetNewDataHandler = () => new GetNewDataResponse { DataSet = serverDataSet },
            };
            var dataObject = new FormDataObject(BuildEmployeeSchema(), connector);

            await dataObject.NewAsync();

            Assert.Same(serverDataSet, dataObject.DataSet);
            Assert.Equal(rowId, dataObject.MasterRow!["sys_rowid"]);
            Assert.False(dataObject.IsDirty);
            Assert.False(dataObject.IsLoading);
        }

        [Fact]
        [DisplayName("NewAsync throws InvalidOperationException when the server returns a null DataSet")]
        public async Task NewAsync_NullDataSet_Throws()
        {
            var connector = new FakeFormApiConnector
            {
                GetNewDataHandler = () => new GetNewDataResponse { DataSet = null },
            };
            var dataObject = new FormDataObject(BuildEmployeeSchema(), connector);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.NewAsync());
        }

        [Fact]
        [DisplayName("NewAsync throws InvalidOperationException when there is no connector")]
        public async Task NewAsync_NoConnector_Throws()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.NewAsync());
        }

        [Fact]
        [DisplayName("SaveAsync on success replaces the local DataSet with the server's refreshed DataSet and resets IsDirty")]
        public async Task SaveAsync_Success_ReplacesDataSetAndResetsDirty()
        {
            var schema = BuildEmployeeSchema();
            var rowId = Guid.NewGuid();
            var refreshed = BuildServerDataSet(rowId, "Persisted");
            DataSet? capturedRequest = null;
            var connector = new FakeFormApiConnector
            {
                SaveHandler = ds =>
                {
                    capturedRequest = ds;
                    return new SaveResponse { DataSet = refreshed };
                },
            };
            var dataObject = new FormDataObject(schema, connector);
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_name", "Pending");
            Assert.True(dataObject.IsDirty);
            var requestedDataSet = dataObject.DataSet;

            await dataObject.SaveAsync();

            Assert.Same(requestedDataSet, capturedRequest);
            Assert.Same(refreshed, dataObject.DataSet);
            Assert.Equal("Persisted", dataObject.GetField("emp_name"));
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("SaveAsync keeps the local content and resets IsDirty when the server returns a null DataSet")]
        public async Task SaveAsync_NullRefreshedDataSet_KeepsLocalAndResetsDirty()
        {
            var connector = new FakeFormApiConnector
            {
                SaveHandler = _ => new SaveResponse { DataSet = null },
            };
            var dataObject = new FormDataObject(BuildEmployeeSchema(), connector);
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_name", "Pending");
            var beforeDataSet = dataObject.DataSet;
            Assert.True(dataObject.IsDirty);

            await dataObject.SaveAsync();

            Assert.Same(beforeDataSet, dataObject.DataSet);
            Assert.False(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("SaveAsync throws InvalidOperationException when there is no connector")]
        public async Task SaveAsync_NoConnector_Throws()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.SaveAsync());
        }

        [Fact]
        [DisplayName("DeleteAsync on success calls the connector and resets to an empty DataSet")]
        public async Task DeleteAsync_Success_ResetsToEmptyDataSet()
        {
            var schema = BuildEmployeeSchema();
            var rowId = Guid.NewGuid();
            var loaded = BuildServerDataSet(rowId, "Alice");
            Guid? deletedRowId = null;
            var connector = new FakeFormApiConnector
            {
                GetDataHandler = _ => new GetDataResponse { DataSet = loaded },
                DeleteHandler = id =>
                {
                    deletedRowId = id;
                    return new DeleteResponse { RowsAffected = 1 };
                },
            };
            var dataObject = new FormDataObject(schema, connector);
            await dataObject.LoadAsync(rowId);

            await dataObject.DeleteAsync();

            Assert.Equal(rowId, deletedRowId);
            Assert.Null(dataObject.MasterRow);
            Assert.False(dataObject.IsDirty);
            // DataSet should be the schema-derived empty skeleton, not the loaded one.
            Assert.NotSame(loaded, dataObject.DataSet);
            Assert.Equal(TestProgId, dataObject.DataSet.DataSetName);
            Assert.Equal(2, dataObject.DataSet.Tables.Count);
        }

        [Fact]
        [DisplayName("DeleteAsync throws InvalidOperationException when there is no MasterRow")]
        public async Task DeleteAsync_NoMasterRow_Throws()
        {
            var connector = new FakeFormApiConnector();
            var dataObject = new FormDataObject(BuildEmployeeSchema(), connector);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.DeleteAsync());
        }

        [Fact]
        [DisplayName("DeleteAsync throws InvalidOperationException when there is no connector")]
        public async Task DeleteAsync_NoConnector_Throws()
        {
            var dataObject = new FormDataObject(BuildEmployeeSchema());
            dataObject.InitializeNewMaster();

            await Assert.ThrowsAsync<InvalidOperationException>(() => dataObject.DeleteAsync());
        }

        /// <summary>
        /// Test double that bypasses the real JSON-RPC pipeline by overriding every
        /// virtual CRUD method on <see cref="FormApiConnector"/>. The base constructor
        /// still installs a <see cref="Polhem.Api.Client.Providers.LocalApiProvider"/>, but
        /// because all four methods short-circuit before reaching it the provider is
        /// never invoked.
        /// </summary>
        private sealed class FakeFormApiConnector : FormApiConnector
        {
            public FakeFormApiConnector() : base(Guid.NewGuid(), TestProgId) { }

            public Func<Guid, GetDataResponse>? GetDataHandler { get; set; }
            public Func<GetNewDataResponse>? GetNewDataHandler { get; set; }
            public Func<DataSet, SaveResponse>? SaveHandler { get; set; }
            public Func<Guid, DeleteResponse>? DeleteHandler { get; set; }

            public override Task<GetDataResponse> GetDataAsync(Guid rowId)
                => Task.FromResult((GetDataHandler ?? (_ => new GetDataResponse()))(rowId));

            public override Task<GetNewDataResponse> GetNewDataAsync()
                => Task.FromResult((GetNewDataHandler ?? (() => new GetNewDataResponse()))());

            public override Task<SaveResponse> SaveAsync(DataSet dataSet)
                => Task.FromResult((SaveHandler ?? (_ => new SaveResponse()))(dataSet));

            public override Task<DeleteResponse> DeleteAsync(Guid rowId)
                => Task.FromResult((DeleteHandler ?? (_ => new DeleteResponse()))(rowId));
        }
    }
}
