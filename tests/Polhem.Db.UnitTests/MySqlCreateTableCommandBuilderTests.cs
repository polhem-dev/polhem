using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.MySql;
using Polhem.Definition;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure-syntax tests for <see cref="MySqlCreateTableCommandBuilder"/>. No live database
    /// connection — the builder produces string output that is asserted via
    /// <see cref="Assert.Contains(string, string)"/> against the well-known fragments
    /// for MySQL 8.0+ dialect (backtick quoting, BIGINT AUTO_INCREMENT PRIMARY KEY,
    /// utf8mb4_0900_ai_ci CI collation table suffix).
    /// </summary>
    public class MySqlCreateTableCommandBuilderTests
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

        #region GetMySqlType branches per FieldDbType

        [Theory]
        [InlineData(FieldDbType.Boolean, "TINYINT(1)")]
        [InlineData(FieldDbType.Short, "SMALLINT")]
        [InlineData(FieldDbType.Integer, "INT")]
        [InlineData(FieldDbType.Long, "BIGINT")]
        [InlineData(FieldDbType.Currency, "DECIMAL(19,4)")]
        [InlineData(FieldDbType.Date, "DATE")]
        [InlineData(FieldDbType.DateTime, "DATETIME(6)")]
        [InlineData(FieldDbType.Time, "CHAR(5)")]
        [InlineData(FieldDbType.Guid, "CHAR(36)")]
        [InlineData(FieldDbType.Binary, "LONGBLOB")]
        [InlineData(FieldDbType.Text, "LONGTEXT")]
        [DisplayName("GetCommandText produces the matching MySQL type string for each FieldDbType")]
        public void GetCommandText_FieldDbType_GeneratesCorrectColumnType(FieldDbType dbType, string expectedFragment)
        {
            var schema = BuildSchema(dbType);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains(expectedFragment, sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses VARCHAR with the length for the String type")]
        public void GetCommandText_String_UsesVarcharLength()
        {
            var schema = BuildSchema(FieldDbType.String, length: 50);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("VARCHAR(50)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses DECIMAL(precision,scale) for Decimal")]
        public void GetCommandText_Decimal_UsesDecimal()
        {
            var schema = BuildSchema(FieldDbType.Decimal, precision: 12, scale: 3);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DECIMAL(12,3)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText throws InvalidOperationException for an unsupported FieldDbType")]
        public void GetCommandText_UnknownDbType_Throws()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add("col", "Col", (FieldDbType)999);
            var builder = new MySqlCreateTableCommandBuilder();

            Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
        }

        #endregion

        #region Table suffix and identifier quoting

        [Fact]
        [DisplayName("The CREATE TABLE suffix carries ENGINE=InnoDB and the utf8mb4_0900_ai_ci collation (case-insensitive comparison built in from day one)")]
        public void GetCommandText_TableSuffix_IncludesInnoDbAndCiCollation()
        {
            var schema = BuildSchema(FieldDbType.String, length: 50);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            // ERP needs case-insensitive comparison: WHERE name = 'jeff' must match 'Jeff'. The table-level
            // COLLATE=utf8mb4_0900_ai_ci applies it uniformly.
            Assert.Contains("ENGINE=InnoDB", sql);
            Assert.Contains("DEFAULT CHARSET=utf8mb4", sql);
            Assert.Contains("COLLATE=utf8mb4_0900_ai_ci", sql);
        }

        [Fact]
        [DisplayName("Identifiers are quoted with backticks")]
        public void GetCommandText_Identifiers_UseBackticks()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CREATE TABLE `st_demo`", sql);
            Assert.Contains("`col`", sql);
        }

        #endregion

        #region Default Expressions

        [Fact]
        [DisplayName("A non-AllowNull Integer field produces NOT NULL DEFAULT 0")]
        public void GetCommandText_NonNullInteger_GeneratesNotNullDefault0()
        {
            var schema = BuildSchema(FieldDbType.Integer, allowNull: false);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("`col` INT NOT NULL DEFAULT 0", sql);
        }

        [Fact]
        [DisplayName("A non-AllowNull String field produces an empty string DEFAULT")]
        public void GetCommandText_NonNullString_GeneratesEmptyStringDefault()
        {
            var schema = BuildSchema(FieldDbType.String, length: 50, allowNull: false);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT ''", sql);
        }

        [Fact]
        [DisplayName("An AllowNull field has no DEFAULT clause")]
        public void GetCommandText_AllowNullField_OmitsDefault()
        {
            var schema = BuildSchema(FieldDbType.Integer, allowNull: true);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("`col` INT NULL", sql);
            Assert.DoesNotContain("`col` INT NULL DEFAULT", sql);
        }

        [Fact]
        [DisplayName("A Guid field DEFAULT is the (UUID()) expression")]
        public void GetCommandText_NonNullGuid_GeneratesUuidDefault()
        {
            var schema = BuildSchema(FieldDbType.Guid, allowNull: false);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (UUID())", sql);
        }

        [Fact]
        [DisplayName("A DateTime field DEFAULT is (UTC_TIMESTAMP(6))")]
        public void GetCommandText_NonNullDateTime_GeneratesCurrentTimestamp()
        {
            var schema = BuildSchema(FieldDbType.DateTime, allowNull: false);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (UTC_TIMESTAMP(6))", sql);
        }

        #endregion

        #region AutoIncrement

        [Fact]
        [DisplayName("An AutoIncrement field is inlined as BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY")]
        public void GetCommandText_AutoIncrement_InlinedOnColumnLine()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("sys_no", "No", FieldDbType.AutoIncrement);
            schema.Indexes!.AddPrimaryKey("sys_no");
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("`sys_no` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY", sql);
            // AutoIncrement uses the inline form, so no separate CONSTRAINT PRIMARY KEY is emitted.
            Assert.DoesNotContain("CONSTRAINT", sql);
        }

        [Fact]
        [DisplayName("An AutoIncrement field that is not a single-column PK throws InvalidOperationException")]
        public void GetCommandText_AutoIncrementNotSinglePk_Throws()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add("sys_no", "No", FieldDbType.AutoIncrement);
            schema.Fields!.Add("other", "Other", FieldDbType.String, 10);
            schema.Indexes!.AddPrimaryKey("other");

            var builder = new MySqlCreateTableCommandBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
            Assert.Contains("must be the single-column primary key", ex.Message);
        }

        [Fact]
        [DisplayName("An AutoIncrement field without a declared PK still produces an inline PRIMARY KEY")]
        public void GetCommandText_AutoIncrementWithoutDeclaredPk_InlinesPrimaryKey()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add("sys_no", "No", FieldDbType.AutoIncrement);
            // No PK in Indexes; the AutoIncrement line provides the PK inline.
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("`sys_no` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY", sql);
            Assert.DoesNotContain("CONSTRAINT", sql);
        }

        [Fact]
        [DisplayName("Several AutoIncrement fields throw InvalidOperationException")]
        public void GetCommandText_MultipleAutoIncrement_Throws()
        {
            var schema = new TableSchema { TableName = "st_bad" };
            schema.Fields!.Add("a", "A", FieldDbType.AutoIncrement);
            schema.Fields!.Add("b", "B", FieldDbType.AutoIncrement);
            schema.Indexes!.AddPrimaryKey("a");

            var builder = new MySqlCreateTableCommandBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
            Assert.Contains("at most one AUTO_INCREMENT", ex.Message);
        }

        #endregion

        #region PRIMARY KEY and indexes

        [Fact]
        [DisplayName("A schema without AutoIncrement produces a separate PRIMARY KEY constraint")]
        public void GetCommandText_NonAutoIncrementSchema_EmitsPrimaryKeyConstraint()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new MySqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CONSTRAINT", sql);
            Assert.Contains("PRIMARY KEY", sql);
            Assert.Contains("`sys_rowid`", sql);
        }

        [Fact]
        [DisplayName("A non-PK index produces a CREATE INDEX statement")]
        public void GetCommandText_NonPkIndexes_EmitCreateIndexStatements()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.Indexes!.Add("ix_{0}_col", "col", false);
            schema.Indexes!.Add("uk_{0}_col", "col", true);

            var builder = new MySqlCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("CREATE INDEX `ix_st_demo_col` ON `st_demo`", sql);
            Assert.Contains("CREATE UNIQUE INDEX `uk_st_demo_col` ON `st_demo`", sql);
        }

        #endregion
    }
}
