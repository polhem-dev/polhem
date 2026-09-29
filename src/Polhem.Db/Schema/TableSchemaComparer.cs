using Polhem.Definition.Database;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Db.Schema.Changes;

namespace Polhem.Db.Schema
{
    /// <summary>
    /// Compares a defined table schema against the actual database table schema.
    /// </summary>
    public sealed class TableSchemaComparer
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableSchemaComparer"/>.
        /// </summary>
        /// <param name="defineTable">The defined table schema.</param>
        /// <param name="realTable">The actual table schema from the database.</param>
        /// <param name="databaseType">The database type whose index/field semantics should drive comparison
        /// (e.g., SQL Server distinguishes ASC/DESC at the index-field level; others do not).</param>
        public TableSchemaComparer(TableSchema defineTable, TableSchema? realTable, DatabaseType databaseType)
        {
            DefineTable = defineTable;
            RealTable = realTable;
            DatabaseType = databaseType;
        }

        /// <summary>
        /// Gets the defined table schema.
        /// </summary>
        public TableSchema DefineTable { get; }

        /// <summary>
        /// Gets the actual table schema from the database.
        /// </summary>
        public TableSchema? RealTable { get; }

        /// <summary>
        /// Gets the database type driving the comparison semantics.
        /// </summary>
        public DatabaseType DatabaseType { get; }

        /// <summary>
        /// Populates the given list with description drift entries between the defined and actual schema.
        /// Empty-define / non-empty-real is treated as no drift (conservative policy; avoids accidental removal).
        /// </summary>
        /// <param name="target">The list to populate.</param>
        private void PopulateDescriptionChanges(List<DescriptionChange> target)
        {
            // SQLite has no COMMENT facility, so `SqliteTableSchemaProvider` reads every caption back
            // as empty and every captioned table would report drift that no statement can ever clear.
            // `TableSchemaDiff.IsEmpty` counts description drift, so reporting it here would make
            // "is this table up to date?" permanently answer no while producing no SQL to fix it.
            if (this.DatabaseType == DatabaseType.SQLite)
                return;

            var real = this.RealTable!;
            // Table-level: DisplayName
            AddDescriptionDrift(target, DescriptionLevel.Table, string.Empty,
                this.DefineTable.DisplayName, real.DisplayName);
            // Column-level: Caption — only when the column exists in both schemas
            foreach (DbField defineField in this.DefineTable.Fields!)
            {
                if (!real.Fields!.Contains(defineField.FieldName))
                    continue;
                var realField = real.Fields[defineField.FieldName];
                AddDescriptionDrift(target, DescriptionLevel.Column, defineField.FieldName,
                    defineField.Caption, realField.Caption);
            }
        }

        /// <summary>
        /// Adds a <see cref="DescriptionChange"/> to the target list if the defined value differs from the real value,
        /// following the conservative policy (empty define → no drift).
        /// </summary>
        private static void AddDescriptionDrift(List<DescriptionChange> target, DescriptionLevel level, string fieldName, string defineValue, string realValue)
        {
            // Conservative: empty define is treated as "not specified", do not remove existing DB description
            if (StringUtilities.IsEmpty(defineValue))
                return;
            // No drift when values match
            if (StringUtilities.IsEquals(defineValue, realValue))
                return;
            target.Add(new DescriptionChange
            {
                Level = level,
                FieldName = fieldName,
                NewValue = defineValue,
                IsNew = StringUtilities.IsEmpty(realValue),
            });
        }

        /// <summary>
        /// Compares a defined field against its database counterpart under the active dialect's
        /// nullability semantics.
        /// </summary>
        /// <param name="defineField">The field as declared in the definition.</param>
        /// <param name="realField">The field as read back from the database.</param>
        private bool CompareField(DbField defineField, DbField realField)
        {
            return NormalizeNullability(defineField).Compare(NormalizeNullability(realField));
        }

        /// <summary>
        /// Collapses the nullability of a field whose effective nullability the active dialect does not
        /// let the definition control, so the two sides of a comparison stay comparable.
        /// </summary>
        /// <remarks>
        /// Oracle equates the empty string with NULL, so `OracleSchemaSyntax` emits every String / Text /
        /// Time column as nullable no matter what the definition says, and `OracleTableSchemaProvider`
        /// reports those columns back as non-nullable to line up with a definition that declares
        /// `AllowNull=false`. A definition that declares `AllowNull="true"` therefore never matched its own
        /// read-back and re-issued an ALTER on every upgrade. That is fatal for Text: Oracle rejects any
        /// MODIFY that restates a LOB column's type with ORA-22859, so the whole upgrade aborted and the
        /// genuine changes in the same plan never landed.
        /// </remarks>
        /// <param name="field">The field to normalize.</param>
        private DbField NormalizeNullability(DbField field)
        {
            if (this.DatabaseType != DatabaseType.Oracle || !field.AllowNull)
                return field;
            if (field.DbType != FieldDbType.String && field.DbType != FieldDbType.Text
                && field.DbType != FieldDbType.Time)
                return field;
            var normalized = field.Clone();
            normalized.AllowNull = false;
            return normalized;
        }

        /// <summary>
        /// Locates the matching index in <see cref="RealTable"/>. PK identity is the
        /// "single PK per table" flag (MySQL hardcodes the PK name as <c>PRIMARY</c>, so
        /// matching by formatted name would always miss); other indexes are matched by
        /// the formatted name.
        /// </summary>
        private DbTableIndex? FindRealIndex(DbTableIndex defineIndex, string tableName)
        {
            if (defineIndex.PrimaryKey)
            {
                foreach (DbTableIndex idx in this.RealTable!.Indexes!)
                {
                    if (idx.PrimaryKey) return idx;
                }
                return null;
            }

            string formattedName = StringUtilities.Format(defineIndex.Name, tableName);
            return this.RealTable!.Indexes!.Contains(formattedName)
                ? this.RealTable.Indexes[formattedName]
                : null;
        }

        /// <summary>
        /// Produces a structured diff describing the differences between the defined schema and the actual database schema.
        /// Each difference is represented as an <see cref="ITableChange"/> record.
        /// Fields and indexes present only in the actual database (not in the defined schema) are preserved and produce no change entries.
        /// </summary>
        public TableSchemaDiff CompareToDiff()
        {
            var diff = new TableSchemaDiff(this.DefineTable, this.RealTable);
            // New-table path: caller inspects IsNewTable; no changes are emitted
            if (this.RealTable == null)
                return diff;
            // Field-level changes
            CollectFieldChanges(diff);
            // Index-level changes
            CollectIndexChanges(diff);
            // Description drift (table DisplayName and column Caption)
            PopulateDescriptionChanges(diff.DescriptionChangeList);
            return diff;
        }

        /// <summary>
        /// Collects field-level changes into the given diff. Fields present only in the actual database are preserved.
        /// When <see cref="DbField.OriginalFieldName"/> is set on a defined field, rename intent is resolved:
        /// the DB column with the old name is renamed if the new name does not yet exist; otherwise the hint is
        /// treated as already-applied (stale) and the normal comparison path is used.
        /// </summary>
        /// <param name="diff">The diff to populate.</param>
        private void CollectFieldChanges(TableSchemaDiff diff)
        {
            foreach (DbField defineField in this.DefineTable.Fields!)
            {
                if (this.RealTable!.Fields!.Contains(defineField.FieldName))
                {
                    // DB already has a column with the target name; compare definitions directly.
                    // Any stale OriginalFieldName hint is treated as already-applied and ignored here.
                    var realField = this.RealTable.Fields[defineField.FieldName];
                    if (!CompareField(defineField, realField))
                        diff.ChangeList.Add(new AlterFieldChange(realField.Clone(), defineField.Clone()));
                    continue;
                }

                // DB does not yet have the target name — check for a rename hint.
                if (StringUtilities.IsNotEmpty(defineField.OriginalFieldName)
                    && this.RealTable.Fields.Contains(defineField.OriginalFieldName))
                {
                    var oldRealField = this.RealTable.Fields[defineField.OriginalFieldName];
                    diff.ChangeList.Add(new RenameFieldChange(defineField.OriginalFieldName, defineField.Clone()));
                    // After rename, the column definition may still differ from the target; emit an
                    // AlterFieldChange against a projection of the real column under the new name.
                    var postRenameField = oldRealField.Clone();
                    postRenameField.FieldName = defineField.FieldName;
                    if (!CompareField(defineField, postRenameField))
                        diff.ChangeList.Add(new AlterFieldChange(postRenameField, defineField.Clone()));
                    continue;
                }

                // No column under the target name and no applicable rename hint — add as a new column.
                diff.ChangeList.Add(new AddFieldChange(defineField.Clone()));
            }
        }

        /// <summary>
        /// Collects index-level changes into the given diff. Indexes present only in the actual database are preserved.
        /// When a defined index differs from its database counterpart, a <see cref="DropIndexChange"/> and
        /// a corresponding <see cref="AddIndexChange"/> are both emitted so the orchestrator can drop-then-recreate.
        /// </summary>
        /// <param name="diff">The diff to populate.</param>
        private void CollectIndexChanges(TableSchemaDiff diff)
        {
            foreach (DbTableIndex defineIndex in this.DefineTable.Indexes!)
            {
                var realIndex = FindRealIndex(defineIndex, this.DefineTable.TableName);
                if (realIndex != null)
                {
                    if (!defineIndex.Compare(realIndex, DatabaseType))
                    {
                        diff.ChangeList.Add(new DropIndexChange(realIndex.Clone()));
                        diff.ChangeList.Add(new AddIndexChange(defineIndex.Clone()));
                    }
                }
                else
                {
                    diff.ChangeList.Add(new AddIndexChange(defineIndex.Clone()));
                }
            }
        }
    }
}
