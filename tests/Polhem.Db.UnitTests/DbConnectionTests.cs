using System.ComponentModel;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class DbConnectionTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DbConnectionTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("OpenConnection 使用環境變數連線字串應成功連線")]
        public void OpenConnection_WithEnvConnStr_Succeeds()
        {
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            conn.Open();
            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }
    }
}
