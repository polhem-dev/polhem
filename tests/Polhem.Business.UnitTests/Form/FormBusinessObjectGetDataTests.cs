using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <c>[DbFact]</c> integration tests for <see cref="FormBusinessObject.GetData"/>:
    /// seed one Employee → GetData(rowId) → compare the field values, and confirm that the returned
    /// DataSet keeps the framework invariants <c>DataSetName == ProgId</c> and <c>Tables[ProgId]</c> is the
    /// master, with every row state <see cref="DataRowState.Unchanged"/>.
    /// </summary>
    public class FormBusinessObjectGetDataTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectGetDataTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("GetData throws ArgumentNullException for null")]
        public void GetData_NullArgs_Throws()
        {
            var bo = new FormBusinessObject(TestPolhemContext.Create(_fx), Guid.NewGuid(),
                CrudTestContext.ProgId);
            Assert.Throws<ArgumentNullException>(() => bo.GetData(null!));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetData returns an existing Employee and keeps the DataSetName / master TableName convention")]
        public void GetData_Sqlite_ReturnsExistingRow()
            => RunReturnsExistingRow(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetData returns an existing Employee and keeps the DataSetName / master TableName convention")]
        public void GetData_SqlServer_ReturnsExistingRow()
            => RunReturnsExistingRow(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetData returns null for a RowId that does not exist")]
        public void GetData_Sqlite_NonExistentRowId_ReturnsNull()
            => RunNonExistentRowReturnsNull(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: GetData returns an existing Employee and keeps the DataSetName / master TableName convention")]
        public void GetData_Oracle_ReturnsExistingRow()
            => RunReturnsExistingRow(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: GetData returns null for a RowId that does not exist")]
        public void GetData_Oracle_NonExistentRowId_ReturnsNull()
            => RunNonExistentRowReturnsNull(DatabaseType.Oracle);

        // Oracle has no UUID type: sys_rowid is RAW(16) and ADO.NET hands it back as byte[].
        // Asserted on the column type rather than only on the value, because a table that
        // declares a column Guid while holding byte arrays reads correctly right here and fails
        // in every consumer that branches on the runtime type.
        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: the sys_rowid column returned by GetData is a Guid, not the byte[] of RAW(16)")]
        public void GetData_Oracle_RowIdColumnIsGuid()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.Oracle);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var employeeRowId = Guid.NewGuid();
            try
            {
                InsertEmployee(ctx, employeeRowId, $"E{runId}", "員工甲", Guid.Empty);

                var master = ctx.CreateBo()
                    .GetData(new GetDataArgs { RowId = employeeRowId })
                    .DataSet!.Tables[CrudTestContext.ProgId]!;

                Assert.Equal(typeof(Guid), master.Columns[SysFields.RowId]!.DataType);
                Assert.Equal(employeeRowId, master.Rows[0][SysFields.RowId]);
                Assert.Equal(DataRowState.Unchanged, master.Rows[0].RowState);
            }
            finally
            {
                TryDelete(ctx, employeeRowId);
            }
        }

        private void RunReturnsExistingRow(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var employeeRowId = Guid.NewGuid();

            try
            {
                InsertEmployee(ctx, employeeRowId, $"E{runId}", "員工甲", Guid.Empty);

                var bo = ctx.CreateBo();
                var result = bo.GetData(new GetDataArgs { RowId = employeeRowId });

                Assert.NotNull(result.DataSet);
                Assert.Equal(CrudTestContext.ProgId, result.DataSet!.DataSetName);
                Assert.True(result.DataSet.Tables.Contains(CrudTestContext.ProgId));

                var master = result.DataSet.Tables[CrudTestContext.ProgId]!;
                Assert.Single(master.Rows);
                // SQLite stores GUID as TEXT — compare via string round-trip so
                // the same assertion passes on every provider.
                Assert.Equal(employeeRowId, Guid.Parse(master.Rows[0][SysFields.RowId].ToString()!));
                Assert.Equal($"E{runId}", master.Rows[0]["sys_id"]);
                Assert.Equal("員工甲", master.Rows[0][SysFields.Name]);

                Assert.Equal(DataRowState.Unchanged, master.Rows[0].RowState);
            }
            finally
            {
                TryDelete(ctx, employeeRowId);
            }
        }

        private void RunNonExistentRowReturnsNull(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            var bo = ctx.CreateBo();

            var result = bo.GetData(new GetDataArgs { RowId = Guid.NewGuid() });

            Assert.Null(result.DataSet);
        }

        private static void InsertEmployee(CrudTestContext ctx, Guid rowId, string sysId, string sysName, Guid deptRowId)
        {
            var dt = new DataTable();
            dt.Columns.Add(SysFields.RowId, typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add(SysFields.Name, typeof(string));
            dt.Columns.Add("dept_rowid", typeof(Guid));
            var row = dt.NewRow();
            row[SysFields.RowId] = rowId;
            row["sys_id"] = sysId;
            row[SysFields.Name] = sysName;
            row["dept_rowid"] = deptRowId;
            var spec = new InsertCommandBuilder(ctx.EmployeeSchema, ctx.DbType).Build(CrudTestContext.ProgId, row);
            ctx.DbAccess.Execute(spec);
        }

        private static void TryDelete(CrudTestContext ctx, Guid rowId)
        {
            try
            {
                var spec = new DeleteCommandBuilder(ctx.EmployeeSchema, ctx.DbType)
                    .Build(CrudTestContext.ProgId, FilterCondition.Equal(SysFields.RowId, rowId));
                ctx.DbAccess.Execute(spec);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDataTests cleanup of Employee#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
