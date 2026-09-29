using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableSchemaComparerTests : IClassFixture<SharedDbFixture>
    {
        public TableSchemaComparerTests(SharedDbFixture _) { }

        private static TableSchema BuildBaseSchema(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);
            return schema;
        }

        // Simulates a real table structure read back from the database:
        // index names are already formatted strings, not templates.
        private static TableSchema BuildRealSchema(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            var pk = new DbTableIndex
            {
                Name = $"pk_{tableName}",
                PrimaryKey = true,
                Unique = true
            };
            pk.IndexFields!.Add(SysFields.RowId);
            schema.Indexes!.Add(pk);
            return schema;
        }

        // ---------- Oracle: the definition's AllowNull is not comparable for String / Text ----------
        // Regression: Oracle always creates String/Text as nullable, so `OracleTableSchemaProvider` always reports
        // AllowNull=false to match definitions that say false. A definition that says AllowNull="true" never matched,
        // so every upgrade re-emitted an AlterFieldChange. For Text that is fatal (MODIFY restating CLOB raises
        // ORA-22859): the whole plan stops, and the real AddFieldChange in the same batch never lands.

        private static TableSchema BuildNullableTextSchema(bool defineAllowNull)
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            var text = schema.Fields!.Add("error_message", "Message", FieldDbType.Text);
            text.AllowNull = defineAllowNull;
            return schema;
        }

        [Fact]
        [DisplayName("Oracle produces no difference for a Text defined AllowNull=true against AllowNull=false read back")]
        public void CompareToDiff_OracleNullableText_ProducesNoChange()
        {
            var define = BuildNullableTextSchema(defineAllowNull: true);
            // `OracleTableSchemaProvider` always reports AllowNull=false for String/Text.
            var real = BuildNullableTextSchema(defineAllowNull: false);

            var diff = new TableSchemaComparer(define, real, DatabaseType.Oracle).CompareToDiff();

            Assert.Empty(diff.Changes);
        }

        [Fact]
        [DisplayName("Oracle produces no difference for a String defined AllowNull=true against AllowNull=false read back")]
        public void CompareToDiff_OracleNullableString_ProducesNoChange()
        {
            var define = new TableSchema { TableName = "st_demo" };
            define.Fields!.Add("user_id", "User", FieldDbType.String, 50).AllowNull = true;
            var real = new TableSchema { TableName = "st_demo" };
            real.Fields!.Add("user_id", "User", FieldDbType.String, 50).AllowNull = false;

            var diff = new TableSchemaComparer(define, real, DatabaseType.Oracle).CompareToDiff();

            Assert.Empty(diff.Changes);
        }

        [Fact]
        [DisplayName("Oracle ignoring nullable Text does not swallow a real new field in the same diff")]
        public void CompareToDiff_OracleNullableTextWithNewField_StillReportsAddField()
        {
            var define = BuildNullableTextSchema(defineAllowNull: true);
            define.Fields!.Add("api_key_id", "API Key", FieldDbType.String, 50).AllowNull = true;
            var real = BuildNullableTextSchema(defineAllowNull: false);

            var diff = new TableSchemaComparer(define, real, DatabaseType.Oracle).CompareToDiff();

            var change = Assert.Single(diff.Changes);
            Assert.Equal("api_key_id", Assert.IsType<AddFieldChange>(change).Field.FieldName);
        }

        [Fact]
        [DisplayName("Outside Oracle, an AllowNull difference on Text still counts as a difference")]
        public void CompareToDiff_NonOracleNullableText_ReportsAlterField()
        {
            // The other providers report the actual nullability faithfully,
            // so AllowNull is a real, comparable difference.
            var define = BuildNullableTextSchema(defineAllowNull: true);
            var real = BuildNullableTextSchema(defineAllowNull: false);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var change = Assert.Single(diff.Changes);
            Assert.Equal("error_message", Assert.IsType<AlterFieldChange>(change).NewField.FieldName);
        }

        [Fact]
        [DisplayName("The Comparer exposes the DefineTable and RealTable properties")]
        public void Properties_ExposeInputs()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            var comparer = new TableSchemaComparer(define, real, DatabaseType.SQLServer);

            Assert.Same(define, comparer.DefineTable);
            Assert.Same(real, comparer.RealTable);
        }

        // ---- CompareToDiff ----

        [Fact]
        [DisplayName("CompareToDiff gives IsNewTable=true and no Changes when RealTable is null")]
        public void CompareToDiff_NullRealTable_ReturnsNewTableDiffWithNoChanges()
        {
            var define = BuildBaseSchema();
            var comparer = new TableSchemaComparer(define, null, DatabaseType.SQLServer);

            var diff = comparer.CompareToDiff();

            Assert.True(diff.IsNewTable);
            Assert.Empty(diff.Changes);
        }

        [Fact]
        [DisplayName("CompareToDiff returns empty Changes when the structures are identical")]
        public void CompareToDiff_IdenticalSchemas_ReturnsNoChanges()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            var comparer = new TableSchemaComparer(define, real, DatabaseType.SQLServer);

            var diff = comparer.CompareToDiff();

            Assert.Empty(diff.Changes);
            Assert.True(diff.IsEmpty);
        }

        [Fact]
        [DisplayName("CompareToDiff produces an AddFieldChange for a field missing from the real table")]
        public void CompareToDiff_MissingField_EmitsAddFieldChange()
        {
            var define = BuildBaseSchema();
            define.Fields!.Add("age", "Age", FieldDbType.Integer);
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var addChange = Assert.Single(diff.Changes.OfType<AddFieldChange>());
            Assert.Equal("age", addChange.Field.FieldName);
            Assert.Equal(FieldDbType.Integer, addChange.Field.DbType);
        }

        [Fact]
        [DisplayName("CompareToDiff produces an AlterFieldChange (with Old/New) for a field with a different definition")]
        public void CompareToDiff_DifferentField_EmitsAlterFieldChange()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            real.Fields!["name"].Length = 30;

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var alterChange = Assert.Single(diff.Changes.OfType<AlterFieldChange>());
            Assert.Equal("name", alterChange.NewField.FieldName);
            Assert.Equal(50, alterChange.NewField.Length);
            Assert.Equal(30, alterChange.OldField.Length);
        }

        [Fact]
        [DisplayName("CompareToDiff produces an AddIndexChange for an index missing from the real table")]
        public void CompareToDiff_MissingIndex_EmitsAddIndexChange()
        {
            var define = BuildBaseSchema();
            define.Indexes!.Add("ix_{0}_name", "name", false);
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var addIndex = Assert.Single(diff.Changes.OfType<AddIndexChange>());
            Assert.Equal("ix_{0}_name", addIndex.Index.Name);
            Assert.Empty(diff.Changes.OfType<DropIndexChange>());
        }

        [Fact]
        [DisplayName("CompareToDiff produces both a Drop and an Add for an index with a different definition")]
        public void CompareToDiff_DifferentIndex_EmitsDropThenAdd()
        {
            var define = BuildBaseSchema();
            define.Indexes!.Add("ix_{0}_name", "name", true);
            var real = BuildRealSchema();
            real.Indexes!.Add("ix_st_demo_name", "name", false);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var drop = Assert.Single(diff.Changes.OfType<DropIndexChange>());
            var add = Assert.Single(diff.Changes.OfType<AddIndexChange>());
            Assert.Equal("ix_st_demo_name", drop.Index.Name);
            Assert.Equal("ix_{0}_name", add.Index.Name);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no Change for a field that exists only in the real table (keep policy)")]
        public void CompareToDiff_ExtraFieldInRealTable_EmitsNoChangeForExtensionField()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            real.Fields!.Add("legacy_col", "Legacy", FieldDbType.String, 10);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.Changes);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no Change for an index that exists only in the real table (keep policy)")]
        public void CompareToDiff_ExtraIndexInRealTable_EmitsNoChangeForExtensionIndex()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            real.Indexes!.Add("ix_st_demo_legacy", "name", false);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.Changes);
        }

        [Fact]
        [DisplayName("CompareToDiff puts description differences into diff.DescriptionChanges")]
        public void CompareToDiff_DescriptionDiff_PopulatesDiffDescriptionChanges()
        {
            var define = BuildBaseSchema();
            define.DisplayName = "示範資料表";
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var change = Assert.Single(diff.DescriptionChanges);
            Assert.Equal(DescriptionLevel.Table, change.Level);
            Assert.Equal("示範資料表", change.NewValue);
            Assert.True(change.IsNew);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no DescriptionChange for an empty description in the definition (conservative policy)")]
        public void CompareToDiff_EmptyDefineDescription_NoDescriptionChange()
        {
            var define = BuildBaseSchema();
            define.DisplayName = string.Empty;
            var real = BuildRealSchema();
            real.DisplayName = "DB 既有表說明";

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.DescriptionChanges);
        }

        [Fact]
        [DisplayName("CompareToDiff produces a column-level DescriptionChange in update mode for a Caption-only difference")]
        public void CompareToDiff_OnlyFieldCaptionDiffers_DescriptionChangeIsUpdate()
        {
            var define = BuildBaseSchema();
            define.Fields!["name"].Caption = "新名稱";
            var real = BuildRealSchema();
            real.Fields!["name"].Caption = "舊名稱"; // The database already has a different value.

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.Changes);
            var change = Assert.Single(diff.DescriptionChanges);
            Assert.Equal(DescriptionLevel.Column, change.Level);
            Assert.Equal("name", change.FieldName);
            Assert.Equal("新名稱", change.NewValue);
            Assert.False(change.IsNew);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no DescriptionChange for an empty field Caption in the definition (conservative policy)")]
        public void CompareToDiff_EmptyDefineCaption_NoDescriptionChange()
        {
            var define = BuildBaseSchema();
            define.Fields!["name"].Caption = string.Empty;
            var real = BuildRealSchema();
            real.Fields!["name"].Caption = "DB 既有欄位說明";

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.DescriptionChanges);
        }

        [Fact]
        [DisplayName("CompareToDiff reports both structural and description differences when both exist")]
        public void CompareToDiff_SchemaAndDescriptionDiffer_PopulatesBoth()
        {
            var define = BuildBaseSchema();
            define.DisplayName = "新表說明";
            var real = BuildRealSchema();
            real.Fields!["name"].Length = 30;

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Single(diff.Changes.OfType<AlterFieldChange>());
            Assert.Contains(diff.DescriptionChanges, c => c.Level == DescriptionLevel.Table);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no DescriptionChange when RealTable is null (the CREATE path handles descriptions)")]
        public void CompareToDiff_NullRealTable_DescriptionChangesEmpty()
        {
            var define = BuildBaseSchema();
            define.DisplayName = "示範資料表";

            var diff = new TableSchemaComparer(define, null, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.DescriptionChanges);
        }

        [Fact]
        [DisplayName("CompareToDiff produces no DescriptionChange for a field that exists only in the real table")]
        public void CompareToDiff_ExtraFieldInRealTable_NoColumnDescriptionChange()
        {
            var define = BuildBaseSchema();
            var real = BuildRealSchema();
            real.Fields!.Add("legacy_col", "舊欄位說明", FieldDbType.String, 10);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.DoesNotContain(diff.DescriptionChanges, c => c.FieldName == "legacy_col");
        }

        // ---- Rename detection via OriginalFieldName ----

        [Fact]
        [DisplayName("CompareToDiff produces a RenameFieldChange when the old name exists and the new name does not")]
        public void CompareToDiff_RenameHint_OldNameExists_EmitsRenameFieldChange()
        {
            var define = BuildBaseSchema();
            define.Fields!["name"].FieldName = "display_name";
            define.Fields!["display_name"].OriginalFieldName = "name";
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            var rename = Assert.Single(diff.Changes.OfType<RenameFieldChange>());
            Assert.Equal("name", rename.OldFieldName);
            Assert.Equal("display_name", rename.NewField.FieldName);
            // Same type (String 50 ↔ String 50), so no extra AlterFieldChange.
            Assert.Empty(diff.Changes.OfType<AlterFieldChange>());
        }

        [Fact]
        [DisplayName("CompareToDiff produces both a Rename and an Alter for a rename combined with a type change")]
        public void CompareToDiff_RenameWithTypeChange_EmitsRenameAndAlter()
        {
            var define = BuildBaseSchema();
            define.Fields!["name"].FieldName = "display_name";
            define.Fields!["display_name"].OriginalFieldName = "name";
            define.Fields!["display_name"].Length = 100; // Differs from the 50 in the real table.
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Single(diff.Changes.OfType<RenameFieldChange>());
            var alter = Assert.Single(diff.Changes.OfType<AlterFieldChange>());
            // The old side of the Alter is the post-rename projection (new name, old definition).
            Assert.Equal("display_name", alter.OldField.FieldName);
            Assert.Equal(50, alter.OldField.Length);
            Assert.Equal(100, alter.NewField.Length);
        }

        [Fact]
        [DisplayName("CompareToDiff treats a stale rename hint (the new name already exists in the database) as done and produces no Rename")]
        public void CompareToDiff_RenameHint_StaleHint_NoRenameEmitted()
        {
            var define = BuildBaseSchema();
            define.Fields!["name"].FieldName = "display_name";
            define.Fields!["display_name"].OriginalFieldName = "name";
            var real = BuildRealSchema();
            // The database has already completed the rename: display_name exists and name does not.
            real.Fields!.Remove("name");
            real.Fields!.Add("display_name", "Display Name", FieldDbType.String, 50);

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.Changes.OfType<RenameFieldChange>());
            Assert.Empty(diff.Changes.OfType<AddFieldChange>());
        }

        [Fact]
        [DisplayName("CompareToDiff falls back to an AddFieldChange when neither the old nor the new name exists (warning case)")]
        public void CompareToDiff_RenameHint_NeitherNameInRealTable_FallsBackToAddField()
        {
            var define = BuildBaseSchema();
            define.Fields!.Add("new_col", "New", FieldDbType.String, 20);
            define.Fields!["new_col"].OriginalFieldName = "ghost_col"; // The old name does not exist in the database.
            var real = BuildRealSchema();

            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();

            Assert.Empty(diff.Changes.OfType<RenameFieldChange>());
            var add = Assert.Single(diff.Changes.OfType<AddFieldChange>());
            Assert.Equal("new_col", add.Field.FieldName);
        }
    }
}
