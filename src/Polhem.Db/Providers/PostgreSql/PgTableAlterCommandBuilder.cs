using Polhem.Core;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.PostgreSql
{
    /// <summary>
    /// Generates PostgreSQL <c>ALTER TABLE</c> statements for a <see cref="ITableChange"/>.
    /// PG-specific dialect: <c>ALTER COLUMN ... TYPE</c>, <c>SET / DROP NOT NULL</c>,
    /// <c>SET / DROP DEFAULT</c>, <c>RENAME COLUMN</c>; constraint-based primary keys.
    /// Counterpart to <see cref="SqlServer.SqlTableAlterCommandBuilder"/>.
    /// </summary>
    public sealed class PgTableAlterCommandBuilder : ITableAlterCommandBuilder
    {
        /// <inheritdoc />
        public ChangeExecutionKind GetExecutionKind(ITableChange change)
            => AlterCompatibilityRules.GetExecutionKind(change);

        /// <inheritdoc />
        public bool IsNarrowingChange(ITableChange change)
            => AlterCompatibilityRules.IsNarrowingChange(change);

        /// <inheritdoc />
        public IReadOnlyList<string> GetStatements(string tableName, ITableChange change)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            switch (change)
            {
                case AddFieldChange add:
                    return new[] { BuildAddFieldStatement(tableName, add.Field) };
                case AlterFieldChange alter:
                    return BuildAlterFieldStatements(tableName, alter.OldField, alter.NewField);
                case RenameFieldChange rename:
                    return new[] { BuildRenameFieldStatement(tableName, rename) };
                case AddIndexChange addIndex:
                    return new[] { BuildAddIndexStatement(tableName, addIndex.Index) };
                case DropIndexChange dropIndex:
                    return new[] { BuildDropIndexStatement(tableName, dropIndex.Index) };
                default:
                    throw new InvalidOperationException($"Unsupported change type: {change.GetType().Name}");
            }
        }

        private static string BuildAddFieldStatement(string tableName, DbField field)
        {
            return $"ALTER TABLE {PgSchemaSyntax.QuoteName(tableName)} ADD COLUMN {PgSchemaSyntax.GetColumnDefinition(field)};";
        }

        /// <summary>
        /// Builds the PG <c>ALTER TABLE ... RENAME COLUMN</c> statement.
        /// </summary>
        private static string BuildRenameFieldStatement(string tableName, RenameFieldChange change)
        {
            return $"ALTER TABLE {PgSchemaSyntax.QuoteName(tableName)} RENAME COLUMN " +
                   $"{PgSchemaSyntax.QuoteName(change.OldFieldName)} TO {PgSchemaSyntax.QuoteName(change.NewField.FieldName)};";
        }

        /// <summary>
        /// Builds the (possibly multi-statement) sequence to apply a column alteration.
        /// PG splits type / nullability / default into separate ALTER COLUMN clauses.
        /// </summary>
        private static List<string> BuildAlterFieldStatements(string tableName, DbField oldField, DbField newField)
        {
            var statements = new List<string>();
            string quotedTable = PgSchemaSyntax.QuoteName(tableName);
            string quotedColumn = PgSchemaSyntax.QuoteName(newField.FieldName);

            // 1) Type change (length / precision / scale included)
            bool typeChanged = oldField.DbType != newField.DbType
                || oldField.Length != newField.Length
                || oldField.Precision != newField.Precision
                || oldField.Scale != newField.Scale;
            if (typeChanged)
            {
                string newType = PgTypeMapping.GetPgType(newField);
                statements.Add($"ALTER TABLE {quotedTable} ALTER COLUMN {quotedColumn} TYPE {newType};");
            }

            // 2) Nullability change
            if (oldField.AllowNull != newField.AllowNull)
            {
                statements.Add(newField.AllowNull
                    ? $"ALTER TABLE {quotedTable} ALTER COLUMN {quotedColumn} DROP NOT NULL;"
                    : $"ALTER TABLE {quotedTable} ALTER COLUMN {quotedColumn} SET NOT NULL;");
            }

            // 3) Default change (a nullable column has no DEFAULT in this framework, see
            // PgSchemaSyntax.GetDefaultExpression)
            bool defaultChanged = !StringUtilities.IsEquals(oldField.DefaultValue, newField.DefaultValue)
                || oldField.AllowNull != newField.AllowNull;
            if (defaultChanged)
            {
                string newDefault = PgSchemaSyntax.GetDefaultExpression(newField);
                if (StringUtilities.IsNotEmpty(newDefault))
                    statements.Add($"ALTER TABLE {quotedTable} ALTER COLUMN {quotedColumn} SET DEFAULT {newDefault};");
                else
                    statements.Add($"ALTER TABLE {quotedTable} ALTER COLUMN {quotedColumn} DROP DEFAULT;");
            }

            return statements;
        }

        private static string BuildAddIndexStatement(string tableName, DbTableIndex index)
        {
            string indexName = StringUtilities.Format(index.Name, tableName);

            if (index.PrimaryKey)
            {
                // PostgreSQL rejects `ASC` / `DESC` inside a `PRIMARY KEY` constraint.
                string pkFields = DdlFragments.BuildIndexFieldList(index, PgSchemaSyntax.QuoteName, includeSortDirection: false);
                return $"ALTER TABLE {PgSchemaSyntax.QuoteName(tableName)} ADD CONSTRAINT {PgSchemaSyntax.QuoteName(indexName)} PRIMARY KEY ({pkFields});";
            }

            string fields = DdlFragments.BuildIndexFieldList(index, PgSchemaSyntax.QuoteName, includeSortDirection: true);
            string uniqueClause = index.Unique ? "UNIQUE " : string.Empty;
            return $"CREATE {uniqueClause}INDEX {PgSchemaSyntax.QuoteName(indexName)} ON {PgSchemaSyntax.QuoteName(tableName)} ({fields});";
        }

        private static string BuildDropIndexStatement(string tableName, DbTableIndex index)
        {
            // Primary keys are constraints; everything else is an index.
            if (index.PrimaryKey)
                return $"ALTER TABLE {PgSchemaSyntax.QuoteName(tableName)} DROP CONSTRAINT {PgSchemaSyntax.QuoteName(index.Name)};";

            return $"DROP INDEX {PgSchemaSyntax.QuoteName(index.Name)};";
        }
    }
}
