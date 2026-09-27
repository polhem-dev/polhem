using Polhem.Base.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;

namespace Polhem.Db.Providers.MySql
{
    /// <summary>
    /// <see cref="IDialectFactory"/> implementation for MySQL 8.0+.
    /// </summary>
    public sealed class MySqlDialectFactory : IDialectFactory
    {
        /// <inheritdoc />
        public ITableSchemaProvider CreateTableSchemaProvider(string databaseId, IDbConnectionManager connectionManager)
            => new MySqlTableSchemaProvider(databaseId, connectionManager);

        /// <inheritdoc />
        public ICreateTableCommandBuilder CreateCreateTableCommandBuilder() => new MySqlCreateTableCommandBuilder();

        /// <inheritdoc />
        public ITableAlterCommandBuilder CreateTableAlterCommandBuilder() => new MySqlTableAlterCommandBuilder();

        /// <inheritdoc />
        public ITableRebuildCommandBuilder CreateTableRebuildCommandBuilder() => new MySqlTableRebuildCommandBuilder();

        /// <inheritdoc />
        public IDescriptionSyncCommandBuilder? CreateDescriptionSyncCommandBuilder() => new MySqlDescriptionSyncCommandBuilder();

        /// <inheritdoc />
        public IFormCommandBuilder CreateFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess)
            => new MySqlFormCommandBuilder(formDefine, defineAccess);

        /// <inheritdoc />
        public string GetDefaultValueExpression(FieldDbType dbType) =>
            MySqlSchemaSyntax.GetDefaultValueExpression(dbType);
    }
}
