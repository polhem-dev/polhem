using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// <see cref="TableSchema.ApplyCompanyScope"/> and <see cref="TableSchema.HasCompanyScope"/>.
    /// </summary>
    public class TableSchemaCompanyScopeTests
    {
        private static readonly string[] s_companyAndId = [SysFields.CompanyId, SysFields.Id];
        private static readonly string[] s_companyRoleUser = [SysFields.CompanyId, "role_id", "user_id"];

        [Fact]
        [DisplayName("ApplyCompanyScope adds sys_company_id after sys_rowid at the company id's width")]
        public void ApplyCompanyScope_NoColumn_AddsItAfterRowId()
        {
            var schema = BuildSchema();

            Assert.True(schema.ApplyCompanyScope());

            var names = schema.Fields!.Select(f => f.FieldName).ToList();
            Assert.Equal(names.IndexOf(SysFields.RowId) + 1, names.IndexOf(SysFields.CompanyId));
            var field = schema.Fields![SysFields.CompanyId];
            Assert.Equal(FieldDbType.String, field.DbType);
            Assert.Equal(CompanyInfo.CompanyIdMaxLength, field.Length);
        }

        [Fact]
        [DisplayName("ApplyCompanyScope leads every business unique index with sys_company_id and leaves the row identity indexes alone")]
        public void ApplyCompanyScope_UniqueIndexes_LeadsBusinessKeysOnly()
        {
            var schema = BuildSchema();

            schema.ApplyCompanyScope();

            Assert.Equal(s_companyAndId, Keys(schema, "uk_{0}"));
            Assert.Equal(s_companyRoleUser, Keys(schema, "ux_{0}_role"));
            Assert.Equal([SysFields.No], Keys(schema, "pk_{0}"));
            Assert.Equal([SysFields.RowId], Keys(schema, "rx_{0}"));
            Assert.Equal(["role_id"], Keys(schema, "ix_{0}_role"));
        }

        [Fact]
        [DisplayName("ApplyCompanyScope changes nothing on a table that already has the company scope")]
        public void ApplyCompanyScope_AlreadyScoped_ReturnsFalse()
        {
            var schema = BuildSchema();
            schema.ApplyCompanyScope();

            Assert.True(schema.HasCompanyScope());
            Assert.False(schema.ApplyCompanyScope());
            Assert.Single(schema.Fields!, f => f.FieldName == SysFields.CompanyId);
        }

        [Fact]
        [DisplayName("HasCompanyScope is false while a business unique index does not lead with sys_company_id")]
        public void HasCompanyScope_ColumnButUnscopedIndex_ReturnsFalse()
        {
            var schema = BuildSchema();
            schema.Fields!.Add(SysFields.CompanyId, "Company", FieldDbType.String, CompanyInfo.CompanyIdMaxLength);

            Assert.False(schema.HasCompanyScope());
        }

        private static string[] Keys(TableSchema schema, string indexName)
            => [.. schema.Indexes![indexName].IndexFields!.Select(f => f.FieldName)];

        private static TableSchema BuildSchema()
        {
            var schema = new TableSchema { TableName = "st_demo" };
            schema.Fields!.Add(SysFields.No, "No", FieldDbType.AutoIncrement);
            schema.Fields.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            schema.Fields.Add(SysFields.Id, "Number", FieldDbType.String, 20);
            schema.Fields.Add("role_id", "Role", FieldDbType.String, 20);
            schema.Fields.Add("user_id", "User", FieldDbType.String, 20);
            schema.Indexes!.AddPrimaryKey(SysFields.No);
            schema.Indexes.Add("rx_{0}", SysFields.RowId, true);
            schema.Indexes.Add("uk_{0}", SysFields.Id, true);
            schema.Indexes.Add("ux_{0}_role", "role_id,user_id", true);
            schema.Indexes.Add("ix_{0}_role", "role_id", false);
            return schema;
        }
    }
}
