using Polhem.Core.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;

namespace Polhem.Db.Providers.PostgreSql
{
    /// <summary>
    /// <see cref="IDialectFactory"/> implementation for PostgreSQL.
    /// </summary>
    public sealed class PgDialectFactory : IDialectFactory
    {
        /// <inheritdoc />
        public ITableSchemaProvider CreateTableSchemaProvider(string databaseId, IDbConnectionManager connectionManager)
            => new PgTableSchemaProvider(databaseId, connectionManager);

        /// <inheritdoc />
        public ICreateTableCommandBuilder CreateCreateTableCommandBuilder() => new PgCreateTableCommandBuilder();

        /// <inheritdoc />
        public ITableAlterCommandBuilder CreateTableAlterCommandBuilder() => new PgTableAlterCommandBuilder();

        /// <inheritdoc />
        public ITableRebuildCommandBuilder CreateTableRebuildCommandBuilder() => new PgTableRebuildCommandBuilder();

        /// <inheritdoc />
        public IDescriptionSyncCommandBuilder? CreateDescriptionSyncCommandBuilder() => new PgDescriptionSyncCommandBuilder();

        /// <inheritdoc />
        public IFormCommandBuilder CreateFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess)
            => new PgFormCommandBuilder(formDefine, defineAccess);

        /// <inheritdoc />
        public string GetDefaultValueExpression(FieldDbType dbType) => PgSchemaSyntax.GetDefaultValueExpression(dbType);
    }
}
