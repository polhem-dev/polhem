using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Covers <see cref="SqliteAlterCompatibilityRules"/>, the only difference SQLite has from the shared
    /// <see cref="AlterCompatibilityRules"/>: ALTER COLUMN is not supported, so every type change takes Rebuild.
    /// The narrowing checks are shared; their tests are in <see cref="AlterCompatibilityRulesTests"/>.
    /// </summary>
    public class SqliteAlterCompatibilityRulesTests
    {
        [Theory]
        [InlineData(FieldDbType.String, FieldDbType.String)]
        [InlineData(FieldDbType.String, FieldDbType.Integer)]
        [InlineData(FieldDbType.Integer, FieldDbType.Decimal)]
        [InlineData(FieldDbType.Date, FieldDbType.DateTime)]
        [InlineData(FieldDbType.Time, FieldDbType.Time)]
        [InlineData(FieldDbType.AutoIncrement, FieldDbType.AutoIncrement)]
        [DisplayName("SQLite GetKindForTypeChange returns Rebuild for every known type change")]
        public void GetKindForTypeChange_KnownTypes_ReturnsRebuild(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.Rebuild, SqliteAlterCompatibilityRules.GetKindForTypeChange(from, to));
        }

        [Theory]
        [InlineData(FieldDbType.Unknown, FieldDbType.Integer)]
        [InlineData(FieldDbType.String, FieldDbType.Unknown)]
        [InlineData(FieldDbType.Unknown, FieldDbType.Unknown)]
        [DisplayName("SQLite GetKindForTypeChange returns NotSupported when either side is Unknown")]
        public void GetKindForTypeChange_Unknown_ReturnsNotSupported(FieldDbType from, FieldDbType to)
        {
            Assert.Equal(ChangeExecutionKind.NotSupported,
                SqliteAlterCompatibilityRules.GetKindForTypeChange(from, to));
        }
    }
}
