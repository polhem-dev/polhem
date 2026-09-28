using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <c>[DbFact]</c> integration tests for <see cref="FormBusinessObject.GetNewData"/>:
    /// the skeleton DataSet contains a server-issued <c>sys_rowid</c>, the master row state
    /// is <see cref="DataRowState.Added"/>, and the framework invariants <c>DataSetName == ProgId</c>
    /// and <c>Tables[ProgId]</c> is the master hold.
    /// </summary>
    public class FormBusinessObjectGetNewDataTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectGetNewDataTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("GetNewData throws ArgumentNullException for null")]
        public void GetNewData_NullArgs_Throws()
        {
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(),
                CrudTestContext.ProgId);
            Assert.Throws<ArgumentNullException>(() => bo.GetNewData(null!));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetNewData returns a skeleton DataSet with sys_rowid prefilled on the server")]
        public void GetNewData_Sqlite_ReturnsSkeletonWithServerRowId()
            => RunSkeletonShape(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetNewData returns a skeleton DataSet with sys_rowid prefilled on the server")]
        public void GetNewData_SqlServer_ReturnsSkeletonWithServerRowId()
            => RunSkeletonShape(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetNewData hands the new record to IFormRuleProcessor.ApplyNewRowDefaults so DefaultValueExpression is evaluated on the server")]
        public void GetNewData_Sqlite_AppliesNewRowDefaults()
        {
            var ctx = new CrudTestContext(_fx, DatabaseType.SQLite);
            var recorder = new RecordingRuleProcessor();
            var bo = ctx.CreateBoWithOverrides((typeof(IFormRuleProcessor), recorder));

            var result = bo.GetNewData(new GetNewDataArgs());

            Assert.Same(result.DataSet, recorder.NewRowDataSet);
            Assert.Equal(CrudTestContext.ProgId, recorder.NewRowSchema?.ProgId);
        }

        private sealed class RecordingRuleProcessor : IFormRuleProcessor
        {
            public FormSchema? NewRowSchema { get; private set; }
            public DataSet? NewRowDataSet { get; private set; }

            public void ApplyNewRowDefaults(FormSchema schema, DataSet dataSet, string timeZoneId = "")
            {
                NewRowSchema = schema;
                NewRowDataSet = dataSet;
            }

            public void ApplyBeforeSave(FormSchema schema, DataSet dataSet, RoundingContext roundingContext, string timeZoneId = "")
                => throw new NotSupportedException();

            public void ApplyBeforeDelete(FormSchema schema, DataSet snapshot, string timeZoneId = "")
                => throw new NotSupportedException();
        }

        private void RunSkeletonShape(DatabaseType dbType)
        {
            var ctx = new CrudTestContext(_fx, dbType);
            var bo = ctx.CreateBo();

            var result = bo.GetNewData(new GetNewDataArgs());

            Assert.NotNull(result.DataSet);

            // Framework invariant: `DataSet.DataSetName` equals the ProgId, and `Tables[ProgId]` is the master.
            Assert.Equal(CrudTestContext.ProgId, result.DataSet!.DataSetName);
            Assert.True(result.DataSet.Tables.Contains(CrudTestContext.ProgId));

            var master = result.DataSet.Tables[CrudTestContext.ProgId]!;
            Assert.Single(master.Rows);

            // The server-issued `sys_rowid` must not be `Guid.Empty`, and the row state must be Added.
            var rowId = (Guid)master.Rows[0][SysFields.RowId];
            Assert.NotEqual(Guid.Empty, rowId);
            Assert.Equal(DataRowState.Added, master.Rows[0].RowState);
        }
    }
}
