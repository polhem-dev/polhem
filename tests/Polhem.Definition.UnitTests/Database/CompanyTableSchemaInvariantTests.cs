using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Identity;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// Pins what every company table must carry for companies to share one company database: the
    /// owning company column, and unique indexes that are unique within a company only.
    /// </summary>
    /// <remarks>
    /// Covers the shipped defaults and the definitions that tests and the Northwind demo load. A table
    /// schema is a company table when it is filed under the <c>company</c> folder, which is how the
    /// seeder decides the database it is created in.
    /// </remarks>
    public class CompanyTableSchemaInvariantTests
    {
        private static readonly string[] s_defineRoots = ["tests/Define", "apps/Polhem.Northwind/Define"];

        [Fact]
        [DisplayName("Every company table schema declares sys_company_id as a non-nullable string of the company id width")]
        public void CompanyTableSchemas_DeclareCompanyIdColumn()
        {
            var offenders = new List<string>();
            foreach (var (path, schema) in LoadCompanyTableSchemas())
            {
                var field = schema.Fields!.Contains(SysFields.CompanyId) ? schema.Fields[SysFields.CompanyId] : null;
                bool ok = field != null
                    && field.DbType == FieldDbType.String
                    && field.Length == CompanyInfo.CompanyIdMaxLength
                    && !field.AllowNull;
                if (!ok) offenders.Add(path);
            }

            Assert.True(offenders.Count == 0,
                $"These company tables do not declare {SysFields.CompanyId} as String({CompanyInfo.CompanyIdMaxLength}) NOT NULL: "
                + string.Join(", ", offenders));
        }

        [Fact]
        [DisplayName("Every unique index of a company table, other than on sys_no or sys_rowid alone, leads with sys_company_id")]
        public void CompanyTableSchemas_UniqueIndexesLeadWithCompanyId()
        {
            var offenders = new List<string>();
            foreach (var (path, schema) in LoadCompanyTableSchemas())
            {
                foreach (var index in schema.Indexes!.Where(i => i.Unique && !IsRowIdentity(i)))
                {
                    if (!string.Equals(index.IndexFields![0].FieldName, SysFields.CompanyId, StringComparison.Ordinal))
                        offenders.Add($"{path} {index.Name}");
                }
            }

            Assert.True(offenders.Count == 0,
                $"These unique indexes would make one company's values collide with another's in a shared database; "
                + $"lead them with {SysFields.CompanyId}: " + string.Join(", ", offenders));
        }

        [Fact]
        [DisplayName("The invariant tests find company table schemas in the defaults and in each define folder they cover")]
        public void LoadCompanyTableSchemas_FindsEachSource()
        {
            var paths = LoadCompanyTableSchemas().Select(s => s.Path).ToList();

            Assert.Contains(paths, p => p.StartsWith("Defaults:", StringComparison.Ordinal));
            foreach (string root in s_defineRoots)
                Assert.Contains(paths, p => p.StartsWith(root, StringComparison.Ordinal));
        }

        private static bool IsRowIdentity(DbTableIndex index)
            => index.IndexFields!.Count == 1
               && (string.Equals(index.IndexFields[0].FieldName, SysFields.No, StringComparison.Ordinal)
                   || string.Equals(index.IndexFields[0].FieldName, SysFields.RowId, StringComparison.Ordinal));

        private static IEnumerable<(string Path, TableSchema Schema)> LoadCompanyTableSchemas()
        {
            string prefix = $"TableSchema/{DbCategoryIds.Company}/";
            foreach (string relative in Defaults.ListEmbedded().Where(p => p.StartsWith(prefix, StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(Defaults.OpenEmbedded(relative));
                yield return ($"Defaults:{relative}", XmlCodec.Deserialize<TableSchema>(reader.ReadToEnd())!);
            }

            string repoRoot = RepoRoot.Find();
            foreach (string root in s_defineRoots)
            {
                string folder = Path.Combine(repoRoot, root, "TableSchema", DbCategoryIds.Company);
                foreach (string file in Directory.EnumerateFiles(folder, "*.TableSchema.xml").Order(StringComparer.Ordinal))
                {
                    string display = $"{root}/TableSchema/{DbCategoryIds.Company}/{Path.GetFileName(file)}";
                    yield return (display, XmlCodec.Deserialize<TableSchema>(File.ReadAllText(file))!);
                }
            }
        }
    }
}
