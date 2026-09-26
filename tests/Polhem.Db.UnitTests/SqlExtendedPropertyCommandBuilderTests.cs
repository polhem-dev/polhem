using System.ComponentModel;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Schema;

namespace Polhem.Db.UnitTests
{
    public class SqlExtendedPropertyCommandBuilderTests
    {
        [Fact]
        [DisplayName("GetCommandText returns an empty string for null changes")]
        public void GetCommandText_NullChanges_ReturnsEmpty()
        {
            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", null);
            Assert.Equal(string.Empty, sql);
        }

        [Fact]
        [DisplayName("GetCommandText returns an empty string for an empty list")]
        public void GetCommandText_EmptyList_ReturnsEmpty()
        {
            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", Array.Empty<DescriptionChange>());
            Assert.Equal(string.Empty, sql);
        }

        [Fact]
        [DisplayName("IsNew=true uses sp_addextendedproperty")]
        public void GetCommandText_IsNew_GeneratesAdd()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Table, NewValue = "表說明", IsNew = true },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", changes);

            Assert.Contains("EXEC sp_addextendedproperty", sql);
            Assert.DoesNotContain("sp_updateextendedproperty", sql);
            Assert.Contains("@value=N'表說明'", sql);
            Assert.Contains("@level1type=N'TABLE', @level1name=N'st_demo'", sql);
        }

        [Fact]
        [DisplayName("IsNew=false uses sp_updateextendedproperty")]
        public void GetCommandText_NotNew_GeneratesUpdate()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Table, NewValue = "新表說明", IsNew = false },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", changes);

            Assert.Contains("EXEC sp_updateextendedproperty", sql);
        }

        [Fact]
        [DisplayName("A column-level DescriptionChange appends the level2 clause")]
        public void GetCommandText_ColumnLevel_IncludesLevel2Clause()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Column, FieldName = "sys_id", NewValue = "帳號", IsNew = true },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_user", changes);

            Assert.Contains("@level2type=N'COLUMN', @level2name=N'sys_id'", sql);
            Assert.Contains("@value=N'帳號'", sql);
        }

        [Fact]
        [DisplayName("A table-level DescriptionChange has no level2 clause")]
        public void GetCommandText_TableLevel_OmitsLevel2Clause()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Table, NewValue = "表說明", IsNew = true },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", changes);

            Assert.DoesNotContain("@level2type", sql);
        }

        [Fact]
        [DisplayName("A value containing a single quote is escaped as two single quotes")]
        public void GetCommandText_SingleQuote_Escaped()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Column, FieldName = "col", NewValue = "it's a value", IsNew = true },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("t'tbl", changes);

            Assert.Contains("@value=N'it''s a value'", sql);
            Assert.Contains("@level1name=N't''tbl'", sql);
        }

        [Fact]
        [DisplayName("Several changes produce their statements in order")]
        public void GetCommandText_MultipleChanges_GeneratesAllStatements()
        {
            var changes = new List<DescriptionChange>
            {
                new() { Level = DescriptionLevel.Table, NewValue = "表", IsNew = true },
                new() { Level = DescriptionLevel.Column, FieldName = "a", NewValue = "A 欄", IsNew = true },
                new() { Level = DescriptionLevel.Column, FieldName = "b", NewValue = "B 欄", IsNew = false },
            };

            string sql = SqlExtendedPropertyCommandBuilder.GetCommandText("st_demo", changes);

            Assert.Contains("@value=N'表'", sql);
            Assert.Contains("@level2name=N'a'", sql);
            Assert.Contains("@level2name=N'b'", sql);
            // The third one is an update.
            int updateIdx = sql.IndexOf("sp_updateextendedproperty", StringComparison.Ordinal);
            int bColumnIdx = sql.IndexOf("@level2name=N'b'", StringComparison.Ordinal);
            Assert.True(updateIdx >= 0 && updateIdx < bColumnIdx);
        }
    }
}
