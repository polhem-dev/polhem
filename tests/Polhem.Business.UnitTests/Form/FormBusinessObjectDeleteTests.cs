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
    /// <c>[DbFact]</c> integration tests for <see cref="FormBusinessObject.Delete"/>:
    /// seed one row → Delete(rowId) → check the returned RowsAffected and confirm with a SELECT that it
    /// is gone; a call with a RowId that does not exist returns 0.
    /// </summary>
    public class FormBusinessObjectDeleteTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectDeleteTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Delete throws ArgumentNullException for null")]
        public void Delete_NullArgs_Throws()
        {
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(),
                CrudTestContext.ProgId);
            Assert.Throws<ArgumentNullException>(() => bo.Delete(null!));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: Delete of an existing Employee returns RowsAffected=1 and removes it from the DB")]
        public void Delete_Sqlite_ExistingRow_Removes()
            => RunDeleteExistingRow(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: Delete of an existing Employee returns RowsAffected=1 and removes it from the DB")]
        public void Delete_SqlServer_ExistingRow_Removes()
            => RunDeleteExistingRow(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: Delete of a RowId that does not exist returns RowsAffected=0")]
        public void Delete_Sqlite_NonExistentRow_ReturnsZero()
            => RunDeleteNonExistentRow(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: Delete of an existing Employee returns RowsAffected=1 and removes it from the DB")]
        public void Delete_Oracle_ExistingRow_Removes()
            => RunDeleteExistingRow(DatabaseType.Oracle);

        private void RunDeleteExistingRow(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowId = Guid.NewGuid();

            try
            {
                InsertEmployee(ctx, rowId, $"X{runId}", "待刪", Guid.Empty);

                var result = ctx.CreateBo().Delete(new DeleteArgs { RowId = rowId });
                Assert.Equal(1, result.RowsAffected);

                var reloaded = ctx.CreateBo().GetData(new GetDataArgs { RowId = rowId });
                Assert.Null(reloaded.DataSet);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        private void RunDeleteNonExistentRow(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);

            var result = ctx.CreateBo().Delete(new DeleteArgs { RowId = Guid.NewGuid() });
            Assert.Equal(0, result.RowsAffected);
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
                Console.WriteLine($"DeleteTests cleanup of Employee#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
