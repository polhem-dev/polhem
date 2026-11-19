using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.Sqlite
{
    /// <summary>
    /// Generates SQLite <c>ALTER TABLE</c> statements for a <see cref="ITableChange"/>.
    /// SQLite supports <c>ADD COLUMN</c>, <c>RENAME COLUMN</c>, and index management in place;
    /// every other column-level mutation falls back to a full table rebuild via
    /// <see cref="SqliteTableRebuildCommandBuilder"/>.
    /// </summary>
    public sealed class SqliteTableAlterCommandBuilder : ITableAlterCommandBuilder
    {
        /// <inheritdoc />
        public ChangeExecutionKind GetExecutionKind(ITableChange change)
        {
            switch (change)
            {
                case AddFieldChange add:
                    return CanAddInPlace(add.Field) ? ChangeExecutionKind.Alter : ChangeExecutionKind.Rebuild;
                case RenameFieldChange _:
                case AddIndexChange _:
                case DropIndexChange _:
                    return ChangeExecutionKind.Alter;
                case AlterFieldChange alter:
                    return SqliteAlterCompatibilityRules.GetKindForTypeChange(alter.OldField.DbType, alter.NewField.DbType);
                default:
                    return ChangeExecutionKind.NotSupported;
            }
        }

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
                case RenameFieldChange rename:
                    return new[] { BuildRenameFieldStatement(tableName, rename) };
                case AddIndexChange addIndex:
                    return new[] { BuildAddIndexStatement(tableName, addIndex.Index) };
                case DropIndexChange dropIndex:
                    return new[] { BuildDropIndexStatement(dropIndex.Index) };
                case AlterFieldChange _:
                    throw new InvalidOperationException(
                        "SQLite does not support ALTER COLUMN; AlterFieldChange must go through the rebuild path.");
                default:
                    throw new InvalidOperationException($"Unsupported change type: {change.GetType().Name}");
            }
        }

        /// <summary>
        /// Returns true when SQLite's <c>ADD COLUMN</c> accepts the column as the framework defines it.
        /// </summary>
        /// <remarks>
        /// SQLite refuses to add a column whose default is one of its current-time keywords, such as
        /// <c>CURRENT_TIMESTAMP</c>, or an expression in parentheses, a <c>NOT NULL</c> column without a default, and a
        /// primary key column. The built-in defaults of Date, DateTime and the row id are such expressions, so adding one
        /// of those columns goes through the rebuild, whose copy leaves the new column to its default. Any other Guid
        /// column defaults to the empty GUID literal and is added in place.
        /// </remarks>
        /// <param name="field">The column to add.</param>
        private static bool CanAddInPlace(DbField field)
        {
            if (field.DbType == FieldDbType.AutoIncrement)
                return false;
            string defaultExpression = SqliteSchemaSyntax.GetDefaultExpression(field);
            if (StringUtilities.IsEmpty(defaultExpression))
                return field.AllowNull;
            return !defaultExpression.StartsWith('(')
                && !defaultExpression.StartsWith("CURRENT_", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildAddFieldStatement(string tableName, DbField field)
        {
            return $"ALTER TABLE {SqliteSchemaSyntax.QuoteName(tableName)} ADD COLUMN {SqliteSchemaSyntax.GetColumnDefinition(field)};";
        }

        /// <summary>
        /// Builds the SQLite <c>ALTER TABLE ... RENAME COLUMN</c> statement (requires SQLite 3.25+).
        /// </summary>
        private static string BuildRenameFieldStatement(string tableName, RenameFieldChange change)
        {
            return $"ALTER TABLE {SqliteSchemaSyntax.QuoteName(tableName)} RENAME COLUMN " +
                   $"{SqliteSchemaSyntax.QuoteName(change.OldFieldName)} TO {SqliteSchemaSyntax.QuoteName(change.NewField.FieldName)};";
        }

        /// <summary>
        /// Builds the index creation statement. SQLite has no <c>ADD CONSTRAINT PRIMARY KEY</c>
        /// for an existing table; declaring a new primary key on a populated table requires a
        /// rebuild and so should not reach this method as an Alter change.
        /// </summary>
        private static string BuildAddIndexStatement(string tableName, DbTableIndex index)
        {
            if (index.PrimaryKey)
                throw new NotSupportedException(
                    "SQLite cannot add a PRIMARY KEY to an existing table via ALTER; this requires a rebuild.");

            string indexName = StringUtilities.Format(index.Name, tableName);
            string fields = DdlFragments.BuildIndexFieldList(index, SqliteSchemaSyntax.QuoteName);
            string uniqueClause = index.Unique ? "UNIQUE " : string.Empty;
            return $"CREATE {uniqueClause}INDEX {SqliteSchemaSyntax.QuoteName(indexName)} ON {SqliteSchemaSyntax.QuoteName(tableName)} ({fields});";
        }

        /// <summary>
        /// Builds the index drop statement. SQLite drops PRIMARY KEY only via table rebuild,
        /// so a PK in DropIndexChange is rejected here.
        /// </summary>
        private static string BuildDropIndexStatement(DbTableIndex index)
        {
            if (index.PrimaryKey)
                throw new NotSupportedException(
                    "SQLite cannot drop a PRIMARY KEY from an existing table via ALTER; this requires a rebuild.");

            return $"DROP INDEX {SqliteSchemaSyntax.QuoteName(index.Name)};";
        }
    }
}
