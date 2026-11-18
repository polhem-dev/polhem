using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Contains, StartsWith and EndsWith treat only <c>%</c> as a wildcard: an underscore, the escape character and,
    /// on SQL Server, an opening bracket in the value match themselves on every database.
    /// </summary>
    /// <remarks>
    /// Without the escape, <c>A_0</c> also matched <c>AB01</c>, and on SQL Server <c>[A]</c> matched any value
    /// holding an <c>A</c>. Each database writes the <c>ESCAPE</c> clause into its own SQL, which only a real query
    /// proves.
    /// </remarks>
    public class DataFormRepositoryLikeEscapeDbTests : IClassFixture<SharedDbFixture>
    {
        private static readonly string[] s_codes = ["A_01", "AB01", "50%off", "50 off", "x!y", "[A]1"];

        private readonly SharedDbFixture _fx;

        public DataFormRepositoryLikeEscapeDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a LIKE filter matches an underscore, the escape character and a bracket literally and keeps percent a wildcard")]
        public void Like_SqlServer_OnlyPercentIsWildcard() => Run(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a LIKE filter matches an underscore, the escape character and a bracket literally and keeps percent a wildcard")]
        public void Like_PostgreSql_OnlyPercentIsWildcard() => Run(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a LIKE filter matches an underscore, the escape character and a bracket literally and keeps percent a wildcard")]
        public void Like_MySql_OnlyPercentIsWildcard() => Run(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a LIKE filter matches an underscore, the escape character and a bracket literally and keeps percent a wildcard")]
        public void Like_Sqlite_OnlyPercentIsWildcard() => Run(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a LIKE filter matches an underscore, the escape character and a bracket literally and keeps percent a wildcard")]
        public void Like_Oracle_OnlyPercentIsWildcard() => Run(DatabaseType.Oracle);

        private void Run(DatabaseType databaseType)
        {
            string progId = TransientForm.NewTableName("tb_lke_");
            var schema = new FormSchema(progId, "LIKE escape") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "LIKE escape");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(SysFields.Id, "Code", FieldDbType.String).MaxLength = 20;

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                foreach (var code in s_codes)
                {
                    form.DbAccess.ExecuteNonQuery(
                        $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(SysFields.Id)}, {form.Quote(SysFields.CompanyId)}) " +
                        "VALUES ({0}, {1}, {2})",
                        Guid.NewGuid(), code, TransientForm.CompanyId);
                }

                Assert.Equal(["A_01"], Codes(form, FilterCondition.Contains(SysFields.Id, "A_0")));
                Assert.Equal(["50 off", "50%off"], Codes(form, FilterCondition.StartsWith(SysFields.Id, "50%")));
                Assert.Equal(["x!y"], Codes(form, FilterCondition.Contains(SysFields.Id, "x!y")));
                Assert.Equal(["[A]1"], Codes(form, FilterCondition.Contains(SysFields.Id, "[A]")));
                Assert.Equal(["A_01"], Codes(form, FilterCondition.EndsWith(SysFields.Id, "_01")));
            }
            finally
            {
                form.DropTables();
            }
        }

        private static string[] Codes(TransientForm form, FilterNode filter)
            => form.Repository.GetList(SysFields.Id, filter, null).Table!.Rows.Cast<DataRow>()
                .Select(r => (string)r[SysFields.Id])
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToArray();
    }
}
