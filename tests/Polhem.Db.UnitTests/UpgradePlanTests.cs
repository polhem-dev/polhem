using System.ComponentModel;
using Polhem.Db.Schema;

namespace Polhem.Db.UnitTests
{
    public class UpgradePlanTests
    {
        private static readonly string[] s_createTableSql = { "CREATE TABLE t1 (id INT)" };
        private static readonly string[] s_createIndexSql = { "CREATE INDEX idx ON t1(id)", "CREATE INDEX idx2 ON t1(id)" };
        private static readonly string[] s_narrowingWarning = { "Narrowing change" };

        [Fact]
        [DisplayName("AllStatements with several stages yields every SQL statement in order")]
        public void AllStatements_MultipleStages_YieldsAllInOrder()
        {
            var stage1 = new UpgradeStage(UpgradeStageKind.CreateTable, s_createTableSql);
            var stage2 = new UpgradeStage(UpgradeStageKind.CreateIndexes, s_createIndexSql);
            UpgradeStage[] stages = { stage1, stage2 };
            var plan = new UpgradePlan(UpgradeExecutionMode.Create, stages);

            var statements = plan.AllStatements.ToList();

            Assert.Equal(3, statements.Count);
            Assert.Equal(s_createTableSql[0], statements[0]);
        }

        [Fact]
        [DisplayName("AllStatements returns an empty collection when a stage has no statements")]
        public void AllStatements_NoStages_ReturnsEmpty()
        {
            var plan = new UpgradePlan(UpgradeExecutionMode.NoChange);

            Assert.Empty(plan.AllStatements);
        }

        [Fact]
        [DisplayName("The constructor stores the warnings passed to it")]
        public void Constructor_WithWarnings_StoresWarnings()
        {
            var plan = new UpgradePlan(UpgradeExecutionMode.Alter, null, s_narrowingWarning);

            Assert.Single(plan.Warnings);
            Assert.Equal("Narrowing change", plan.Warnings[0]);
        }
    }
}
