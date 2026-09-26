using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class SqlCreateTableCommandBuilderTests
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

        #region ConverDbType branches per FieldDbType

        [Theory]
        [InlineData(FieldDbType.Boolean, "[bit]")]
        [InlineData(FieldDbType.AutoIncrement, "[int] IDENTITY(1,1)")]
        [InlineData(FieldDbType.Short, "[smallint]")]
        [InlineData(FieldDbType.Integer, "[int]")]
        [InlineData(FieldDbType.Long, "[bigint]")]
        [InlineData(FieldDbType.Currency, "[decimal](19,4)")]
        [InlineData(FieldDbType.Date, "[date]")]
        [InlineData(FieldDbType.DateTime, "[datetime2](7)")]
        [InlineData(FieldDbType.Time, "[nchar](5)")]
        [InlineData(FieldDbType.Guid, "[uniqueidentifier]")]
        [InlineData(FieldDbType.Binary, "[varbinary](max)")]
        [InlineData(FieldDbType.Text, "[nvarchar](max)")]
        [DisplayName("GetCommandText produces the matching SQL Server type string for each FieldDbType")]
        public void GetCommandText_FieldDbType_GeneratesCorrectColumnType(FieldDbType dbType, string expectedFragment)
        {
            var schema = BuildSchema(dbType);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains(expectedFragment, sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses the given length for the String type")]
        public void GetCommandText_String_UsesLength()
        {
            var schema = BuildSchema(FieldDbType.String, length: 50);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("[nvarchar](50)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText uses the given Precision/Scale for Decimal")]
        public void GetCommandText_Decimal_UsesPrecisionAndScale()
        {
            var schema = BuildSchema(FieldDbType.Decimal, precision: 12, scale: 3);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("[decimal](12,3)", sql);
        }

        [Fact]
        [DisplayName("GetCommandText throws InvalidOperationException for an unsupported FieldDbType")]
        public void GetCommandText_UnknownDbType_Throws()
        {
            var schema = BuildSchema(FieldDbType.Unknown);
            var builder = new SqlCreateTableCommandBuilder();

            Assert.Throws<InvalidOperationException>(() => builder.GetCommandText(schema));
        }

        #endregion

        #region Structure and branches

        [Fact]
        [DisplayName("GetCommandText produces a CREATE TABLE statement for UpgradeAction.New")]
        public void GetCommandText_New_GeneratesCreateTable()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.UpgradeAction = DbUpgradeAction.New;
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("-- Create table st_demo", sql);
            Assert.Contains("CREATE TABLE [st_demo]", sql);
        }

        [Fact]
        [DisplayName("A PrimaryKey index produces a CONSTRAINT ... PRIMARY KEY statement")]
        public void GetCommandText_PrimaryKey_GeneratesConstraint()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CONSTRAINT [pk_st_demo] PRIMARY KEY", sql);
            Assert.Contains("[sys_rowid]", sql);
        }

        [Fact]
        [DisplayName("Separate indexes produce CREATE INDEX and CREATE UNIQUE INDEX")]
        public void GetCommandText_Indexes_GeneratesCreateIndex()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.Indexes!.Add("ix_{0}_col", "col", false);
            schema.Indexes!.Add("uk_{0}_col", "col", true);

            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("CREATE INDEX [ix_st_demo_col] ON [st_demo]", sql);
            Assert.Contains("CREATE UNIQUE INDEX [uk_st_demo_col] ON [st_demo]", sql);
        }

        [Fact]
        [DisplayName("An AllowNull field produces the NULL marker and no DEFAULT clause")]
        public void GetCommandText_AllowNull_GeneratesNullWithoutDefault()
        {
            var schema = BuildSchema(FieldDbType.Integer, allowNull: true);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("[col] [int] NULL", sql);
            Assert.DoesNotContain("[col] [int] NULL DEFAULT", sql);
        }

        [Fact]
        [DisplayName("A non-AllowNull Integer field produces NOT NULL DEFAULT (0)")]
        public void GetCommandText_NotNullInteger_GeneratesDefaultZero()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("[col] [int] NOT NULL DEFAULT (0)", sql);
        }

        [Fact]
        [DisplayName("A String field produces an N'...' default")]
        public void GetCommandText_String_GeneratesNStringDefault()
        {
            var schema = BuildSchema(FieldDbType.String, length: 20);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (N'')", sql);
        }

        [Fact]
        [DisplayName("A custom DefaultValue is written into the DEFAULT clause")]
        public void GetCommandText_CustomDefault_AppliedToColumn()
        {
            var schema = BuildSchema(FieldDbType.Integer, defaultValue: "42");
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (42)", sql);
        }

        [Fact]
        [DisplayName("A DateTime field uses getutcdate() as the default")]
        public void GetCommandText_DateTime_DefaultGetdate()
        {
            var schema = BuildSchema(FieldDbType.DateTime);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (getutcdate())", sql);
        }

        [Fact]
        [DisplayName("A Guid field uses newid() as the default")]
        public void GetCommandText_Guid_DefaultNewid()
        {
            var schema = BuildSchema(FieldDbType.Guid);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("DEFAULT (newid())", sql);
        }

        [Fact]
        [DisplayName("An AutoIncrement field produces no DEFAULT clause")]
        public void GetCommandText_AutoIncrement_NoDefault()
        {
            var schema = BuildSchema(FieldDbType.AutoIncrement);
            var builder = new SqlCreateTableCommandBuilder();

            string sql = builder.GetCommandText(schema);

            Assert.Contains("[col] [int] IDENTITY(1,1) NOT NULL", sql);
            Assert.DoesNotContain("[col] [int] IDENTITY(1,1) NOT NULL DEFAULT", sql);
        }

        #endregion

        #region Extended property (description) output

        [Fact]
        [DisplayName("GetCommandText produces sp_addextendedproperty when DisplayName and Caption are set")]
        public void GetCommandText_WithDisplayNameAndCaption_IncludesExtendedProperty()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.DisplayName = "示範資料表";
            schema.Fields!["col"].Caption = "數值欄位";

            var builder = new SqlCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            // Table level.
            Assert.Contains("EXEC sp_addextendedproperty", sql);
            Assert.Contains("@value=N'示範資料表'", sql);
            Assert.Contains("@level1type=N'TABLE', @level1name=N'st_demo'", sql);
            // Field level.
            Assert.Contains("@value=N'數值欄位'", sql);
            Assert.Contains("@level2type=N'COLUMN', @level2name=N'col'", sql);
        }

        [Fact]
        [DisplayName("GetCommandText produces no table-level sp_addextendedproperty when DisplayName is empty")]
        public void GetCommandText_WithEmptyDisplayName_OmitsTableExtendedProperty()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            // DisplayName is empty by default; only the field Caption is set.
            schema.Fields!["col"].Caption = "數值欄位";

            var builder = new SqlCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            // No table-level extended property (the one without level2),
            Assert.DoesNotContain("@level1type=N'TABLE', @level1name=N'st_demo';", sql);
            // but the field level is still produced.
            Assert.Contains("@level2type=N'COLUMN', @level2name=N'col'", sql);
        }

        [Fact]
        [DisplayName("GetCommandText produces no field-level sp_addextendedproperty for a field with an empty Caption")]
        public void GetCommandText_WithEmptyCaption_OmitsColumnExtendedProperty()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.DisplayName = "示範資料表";
            schema.Fields!["col"].Caption = string.Empty;
            schema.Fields![SysFields.RowId].Caption = string.Empty;

            var builder = new SqlCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            // Table level present.
            Assert.Contains("@value=N'示範資料表'", sql);
            // Field level absent.
            Assert.DoesNotContain("@level2type=N'COLUMN'", sql);
        }

        [Fact]
        [DisplayName("GetCommandText escapes a single quote as two single quotes")]
        public void GetCommandText_WithSingleQuote_EscapesCorrectly()
        {
            var schema = BuildSchema(FieldDbType.Integer);
            schema.DisplayName = "O'Brien 表";
            schema.Fields!["col"].Caption = "it's a field";

            var builder = new SqlCreateTableCommandBuilder();
            string sql = builder.GetCommandText(schema);

            Assert.Contains("@value=N'O''Brien 表'", sql);
            Assert.Contains("@value=N'it''s a field'", sql);
        }

        #endregion
    }
}
