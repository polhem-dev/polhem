using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class SqliteCreateTableCommandBuilderTests
    {
        private static TableSchema BuildSchema(FieldDbType dbType, int length = 0,
            int precision = 18, int scale = 0, bool allowNull = false, string defaultValue = "")
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            var field = schema.Fields.Add("col", "Col", dbType, length);
            field.Precision = precision;
            field.Scale = scale;
            field.AllowNull = allowNull;
            field.DefaultValue = defaultValue;
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);
            return schema;
        }

        #region GetSqliteType branches per FieldDbType

        [Theory]
        [InlineData(FieldDbType.Boolean, "BOOLEAN")]
        [InlineData(FieldDbType.Short, "SMALLINT")]
        [InlineData(FieldDbType.Integer, "INTEGER")]
        [InlineData(FieldDbType.Long, "BIGINT")]
        [InlineData(FieldDbType.Currency, "NUMERIC(19,4)")]
        [InlineData(FieldDbType.Date, "DATE")]
        [InlineData(FieldDbType.DateTime, "DATETIME")]
        [InlineData(FieldDbType.Time, "VARCHAR(5)")]
        [InlineData(FieldDbType.Guid, "UUID")]
        [InlineData(FieldDbType.Binary, "BLOB")]
        [InlineData(FieldDbType.Text, "TEXT")]
        [DisplayName("GetCommandText produces the matching SQLite type string for each FieldDbType")]
        public void GetCommandText_FieldDbType_GeneratesCorrectColumnType(FieldDbType dbType, string expectedFragment)
        {
            var schema = BuildSchema(dbType);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains(expectedFragment, sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses VARCHAR with the length for the String type")]
        public void GetCommandText_String_UsesVarcharLength()
        {
            var schema = BuildSchema(FieldDbType.String, length: 50);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("VARCHAR(50)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses NUMERIC(precision,scale) for Decimal")]
        public void GetCommandText_Decimal_UsesNumeric()
        {
            var schema = BuildSchema(FieldDbType.Decimal, precision: 12, scale: 3);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("NUMERIC(12,3)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText throws InvalidOperationException for an unsupported FieldDbType")]
        public void GetCommandText_UnknownDbType_Throws()
        {
            var schema = BuildSchema(FieldDbType.Unknown);
            var builder = new SqliteCreateTableCommandBuilder();

            Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
        }

        #endregion

        #region Structure and branches

        [Fact]
        [DisplayName("GetCommandText produces a CREATE TABLE statement with double-quoted identifiers")]
        public void GetCommandText_New_GeneratesCreateTable()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("-- Create table st_demo", sql);
            Assert.Contains("CREATE TABLE \"st_demo\"", sql);
        }

        [Fact]
        [DisplayName("A Guid PK produces a separate CONSTRAINT ... PRIMARY KEY statement")]
        public void GetCommandText_GuidPrimaryKey_GeneratesConstraint()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CONSTRAINT \"pk_st_demo\" PRIMARY KEY", sql);
            Assert.Contains("\"sys_rowid\"", sql);
        }

        [Fact]
        [DisplayName("Separate indexes produce CREATE INDEX and CREATE UNIQUE INDEX")]
        public void GetCommandText_Indexes_GeneratesCreateIndex()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.Indexes!.Add("ix_{0}_col", "col", false);
            schema.Indexes!.Add("uk_{0}_col", "col", true);

            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CREATE INDEX \"ix_st_demo_col\" ON \"st_demo\"", sql);
            Assert.Contains("CREATE UNIQUE INDEX \"uk_st_demo_col\" ON \"st_demo\"", sql);
        }

        [Fact]
        [DisplayName("An AllowNull field produces the NULL marker and no DEFAULT clause")]
        public void GetCommandText_AllowNull_GeneratesNullWithoutDefault()
        {
            var schema = BuildSchema(FieldDbType.Integer, allowNull: true);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("\"col\" INTEGER NULL", sql);
            Assert.DoesNotContain("\"col\" INTEGER NULL DEFAULT", sql);
        }

        [Fact]
        [DisplayName("A non-AllowNull Integer field produces NOT NULL DEFAULT 0")]
        public void GetCommandText_NotNullInteger_GeneratesDefaultZero()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("\"col\" INTEGER NOT NULL DEFAULT 0", sql);
        }

        [Fact]
        [DisplayName("A String field produces a '' default")]
        public void GetCommandText_String_GeneratesEmptyStringDefault()
        {
            var schema = BuildSchema(FieldDbType.String, length: 20);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT ''", sql);
        }

        [Fact]
        [DisplayName("A custom DefaultValue is written into the DEFAULT clause")]
        public void GetCommandText_CustomDefault_AppliedToColumn()
        {
            var schema = BuildSchema(FieldDbType.Integer, defaultValue: "42");
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT 42", sql);
        }

        [Fact]
        [DisplayName("A DateTime field uses CURRENT_TIMESTAMP as the default")]
        public void GetCommandText_DateTime_DefaultCurrentTimestamp()
        {
            var schema = BuildSchema(FieldDbType.DateTime);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT CURRENT_TIMESTAMP", sql);
        }

        [Fact]
        [DisplayName("A Guid field uses hex(randomblob(16)) as the default")]
        public void GetCommandText_Guid_DefaultHexRandomblob()
        {
            var schema = BuildSchema(FieldDbType.Guid);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (hex(randomblob(16)))", sql);
        }

        #endregion

        #region AutoIncrement inline PK + conflict detection

        [Fact]
        [DisplayName("An AutoIncrement field that is the single-column PK is inlined as INTEGER PRIMARY KEY AUTOINCREMENT")]
        public void GetCommandText_AutoIncrementAsPrimaryKey_InlinesAutoincrement()
        {
            var schema = new TableSchema { TableName = "st_seq" };
            schema.Fields!.Add(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Indexes!.AddPrimaryKey(SysFields.No);

            var builder = new SqliteCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("\"sys_no\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL", sql);
            Assert.DoesNotContain("CONSTRAINT", sql);
            // The AutoIncrement column line itself must not carry a DEFAULT clause.
            string autoIncrementLine = sql.Split("\r\n").Single(l => l.Contains("\"sys_no\""));
            Assert.DoesNotContain("DEFAULT", autoIncrementLine);
        }

        [Fact]
        [DisplayName("An AutoIncrement field with the PK on another field throws InvalidOperationException")]
        public void GetCommandText_AutoIncrementWithMismatchedPrimaryKey_Throws()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            // PK points to sys_rowid, NOT the AutoIncrement column.
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);

            var builder = new SqliteCreateTableCommandBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
            Assert.Contains("must be the single-column primary key", ex.Message);
        }

        [Fact]
        [DisplayName("An AutoIncrement field without a PK index is inlined as the PK (INTEGER PRIMARY KEY AUTOINCREMENT is the PK)")]
        public void GetCommandText_AutoIncrementWithoutPrimaryKey_InlinesPrimaryKey()
        {
            var schema = new TableSchema { TableName = "st_seq" };
            schema.Fields!.Add(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 20);
            // No primary key index declared — the inlined AUTOINCREMENT column is the PK.

            var builder = new SqliteCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("\"sys_no\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL", sql);
            Assert.DoesNotContain("CONSTRAINT", sql);
        }

        [Fact]
        [DisplayName("Several AutoIncrement fields throw InvalidOperationException")]
        public void GetCommandText_MultipleAutoIncrementFields_Throws()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add("a", "A", FieldDbType.AutoIncrement);
            schema.Fields!.Add("b", "B", FieldDbType.AutoIncrement);
            schema.Indexes!.AddPrimaryKey("a");

            var builder = new SqliteCreateTableCommandBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
            Assert.Contains("at most one AUTOINCREMENT", ex.Message);
        }

        #endregion

        #region COMMENT no-op

        [Fact]
        [DisplayName("GetCommandText produces no COMMENT statement (SQLite does not persist descriptions)")]
        public void GetCommandText_NeverEmitsCommentStatements()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.DisplayName = "示範資料表";
            schema.Fields!["col"].Caption = "數值欄位";
            schema.Fields![SysFields.RowId].Caption = "唯一識別";

            var builder = new SqliteCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.DoesNotContain("COMMENT", sql, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region COLLATE NOCASE for case-insensitive compare

        [Theory]
        [InlineData(FieldDbType.String, 50)]
        [InlineData(FieldDbType.Text, 0)]
        [DisplayName("GetCommandText adds COLLATE NOCASE to the column definitions of text fields (String/Text)")]
        public void GetCommandText_TextField_IncludesCollateNocase(FieldDbType dbType, int length)
        {
            var schema = BuildSchema(dbType, length: length);
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            // ERP needs case-insensitive comparison: WHERE name = 'jeff' must match 'Jeff'. The column-level
            // COLLATE NOCASE implements it.
            Assert.Contains("COLLATE NOCASE", sql);
        }

        [Fact]
        [DisplayName("GetCommandText has no COLLATE clause for a schema with no collated field")]
        public void GetCommandText_NonCollateSchema_OmitsCollate()
        {
            // Excludes String/Text/Guid (all three get COLLATE NOCASE) and keeps only numeric, time and binary fields.
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("id", "Id", FieldDbType.Integer);
            schema.Fields.Add("count", "Count", FieldDbType.Integer);
            schema.Fields.Add("amount", "Amount", FieldDbType.Decimal);
            schema.Fields.Add("created", "Created", FieldDbType.DateTime);
            schema.Fields.Add("data", "Data", FieldDbType.Binary);
            schema.Indexes!.AddPrimaryKey("id");
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.DoesNotContain("COLLATE", sql);
        }

        [Fact]
        [DisplayName("GetCommandText adds COLLATE NOCASE to the column definition of a Guid field (GUID comparison ignores case)")]
        public void GetCommandText_GuidField_IncludesCollateNocase()
        {
            // SQLite stores GUIDs as case-sensitive TEXT. COLLATE NOCASE lets GUID keys such as sys_master_rowid match
            // across casing, so details do not become orphans when a master-detail record is reloaded.
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            schema.Indexes!.AddPrimaryKey("sys_rowid");
            var builder = new SqliteCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("\"sys_rowid\" UUID COLLATE NOCASE", sql);
        }

        #endregion

        #region Composite PK / composite index

        [Fact]
        [DisplayName("GetCommandText produces a comma-separated PRIMARY KEY column list for a composite PK")]
        public void GetCommandText_CompositePrimaryKey_EmitsCommaSeparatedFields()
        {
            var schema = new TableSchema { TableName = "st_compo" };
            schema.Fields!.Add("a", "A", FieldDbType.Integer);
            schema.Fields.Add("b", "B", FieldDbType.Integer);
            schema.Indexes!.AddPrimaryKey("a,b");

            var builder = new SqliteCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("CONSTRAINT \"pk_st_compo\" PRIMARY KEY (\"a\" ASC, \"b\" ASC)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText produces a comma-separated INDEX column list for a composite secondary index")]
        public void GetCommandText_CompositeSecondaryIndex_EmitsCommaSeparatedFields()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            schema.Fields.Add("a", "A", FieldDbType.Integer);
            schema.Fields.Add("b", "B", FieldDbType.Integer);
            schema.Indexes!.AddPrimaryKey(SysFields.RowId);
            schema.Indexes.Add("ix_{0}_a_b", "a,b", false);

            var builder = new SqliteCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("CREATE INDEX \"ix_st_demo_a_b\" ON \"st_demo\" (\"a\" ASC, \"b\" ASC);", sql);
        }

        #endregion
    }
}
