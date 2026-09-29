using Polhem.Core.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;

namespace Polhem.Db.Providers.Oracle
{
    /// <summary>
    /// <see cref="IDialectFactory"/> implementation for Oracle 19c+.
    /// </summary>
    public sealed class OracleDialectFactory : IDialectFactory
    {
        /// <inheritdoc />
        public ITableSchemaProvider CreateTableSchemaProvider(string databaseId, IDbConnectionManager connectionManager)
            => new OracleTableSchemaProvider(databaseId, connectionManager);

        /// <inheritdoc />
        public ICreateTableCommandBuilder CreateCreateTableCommandBuilder() => new OracleCreateTableCommandBuilder();

        /// <inheritdoc />
        public ITableAlterCommandBuilder CreateTableAlterCommandBuilder() => new OracleTableAlterCommandBuilder();

        /// <inheritdoc />
        public ITableRebuildCommandBuilder CreateTableRebuildCommandBuilder() => new OracleTableRebuildCommandBuilder();

        /// <inheritdoc />
        public IDescriptionSyncCommandBuilder? CreateDescriptionSyncCommandBuilder() => new OracleDescriptionSyncCommandBuilder();

        /// <inheritdoc />
        public IFormCommandBuilder CreateFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess)
            => new OracleFormCommandBuilder(formDefine, defineAccess);

        /// <inheritdoc />
        public string GetDefaultValueExpression(FieldDbType dbType) =>
            OracleSchemaSyntax.GetDefaultValueExpression(dbType);
    }
}
