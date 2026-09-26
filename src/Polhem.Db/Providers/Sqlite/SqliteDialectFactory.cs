using Polhem.Base.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;

namespace Polhem.Db.Providers.Sqlite
{
    /// <summary>
    /// <see cref="IDialectFactory"/> implementation for SQLite.
    /// Skeleton: factory wiring is in place but the create-builder methods will land in the
    /// follow-up PRs (S2 form CRUD, S3 CREATE TABLE, S4 ALTER / REBUILD, S5 schema reader).
    /// </summary>
    public class SqliteDialectFactory : IDialectFactory
    {
        /// <inheritdoc />
        public ITableSchemaProvider CreateTableSchemaProvider(string databaseId, IDbConnectionManager connectionManager)
            => new SqliteTableSchemaProvider(databaseId, connectionManager);

        /// <inheritdoc />
        public ICreateTableCommandBuilder CreateCreateTableCommandBuilder() => new SqliteCreateTableCommandBuilder();

        /// <inheritdoc />
        public ITableAlterCommandBuilder CreateTableAlterCommandBuilder() => new SqliteTableAlterCommandBuilder();

        /// <inheritdoc />
        public ITableRebuildCommandBuilder CreateTableRebuildCommandBuilder() => new SqliteTableRebuildCommandBuilder();

        /// <inheritdoc />
        public IFormCommandBuilder CreateFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess)
            => new SqliteFormCommandBuilder(formDefine, defineAccess);

        /// <inheritdoc />
        public string GetDefaultValueExpression(FieldDbType dbType) =>
            SqliteSchemaSyntax.GetDefaultValueExpression(dbType);
    }
}
