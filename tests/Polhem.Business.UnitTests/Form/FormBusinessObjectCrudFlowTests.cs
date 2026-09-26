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
    /// Integration tests chaining the CRUD actions, covering the complete new-and-save and edit-and-save
    /// flows, to show that <c>GetNewData → Save → GetData</c> and
    /// <c>GetData → edit → Save → GetData</c> work end to end on a real DB.
    /// </summary>
    public class FormBusinessObjectCrudFlowTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectCrudFlowTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: the new-and-save flow GetNewData → fill fields → Save → GetData round-trips")]
        public void NewAndSaveFlow_Sqlite()
            => RunNewAndSaveFlow(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: the new-and-save flow GetNewData → fill fields → Save → GetData round-trips")]
        public void NewAndSaveFlow_SqlServer()
            => RunNewAndSaveFlow(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: the edit-and-save flow GetData → edit → Save → GetData round-trips")]
        public void LoadAndSaveFlow_Sqlite()
            => RunLoadAndSaveFlow(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: the new-and-save flow GetNewData → fill fields → Save → GetData round-trips")]
        public void NewAndSaveFlow_Oracle()
            => RunNewAndSaveFlow(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: the edit-and-save flow GetData → edit → Save → GetData round-trips")]
        public void LoadAndSaveFlow_Oracle()
            => RunLoadAndSaveFlow(DatabaseType.Oracle);

        private void RunNewAndSaveFlow(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var bo = ctx.CreateBo();

            // 1. GetNewData
            var skeleton = bo.GetNewData(new GetNewDataArgs()).DataSet;
            Assert.NotNull(skeleton);
            var master = skeleton!.Tables[CrudTestContext.ProgId]!;
            var rowId = (Guid)master.Rows[0][SysFields.RowId];

            try
            {
                // 2. Fill in the fields
                master.Rows[0]["sys_id"] = $"F{runId}";
                master.Rows[0][SysFields.Name] = "整合流程員工";

                // 3. Save
                var saveResult = bo.Save(new SaveArgs { DataSet = skeleton });
                Assert.Equal(1, saveResult.AffectedRows[CrudTestContext.ProgId]);

                // 4. GetData reads it back and the fields match
                var reloaded = bo.GetData(new GetDataArgs { RowId = rowId }).DataSet;
                Assert.NotNull(reloaded);
                Assert.Equal($"F{runId}", reloaded!.Tables[CrudTestContext.ProgId]!.Rows[0]["sys_id"]);
                Assert.Equal("整合流程員工", reloaded.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name]);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
        }

        private void RunLoadAndSaveFlow(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowId = Guid.NewGuid();
            var bo = ctx.CreateBo();

            try
            {
                InsertEmployee(ctx, rowId, $"L{runId}", "原始名稱", Guid.Empty);

                // 1. GetData
                var loaded = bo.GetData(new GetDataArgs { RowId = rowId }).DataSet;
                Assert.NotNull(loaded);

                // 2. Edit
                loaded!.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name] = "修改後名稱";

                // 3. Save
                var saveResult = bo.Save(new SaveArgs { DataSet = loaded });
                Assert.Equal(1, saveResult.AffectedRows[CrudTestContext.ProgId]);

                // 4. GetData reads it back
                var reloaded = bo.GetData(new GetDataArgs { RowId = rowId }).DataSet;
                Assert.Equal("修改後名稱",
                    reloaded!.Tables[CrudTestContext.ProgId]!.Rows[0][SysFields.Name]);
            }
            finally
            {
                TryDelete(ctx, rowId);
            }
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
                Console.WriteLine($"CrudFlowTests cleanup of Employee#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
