using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Sorting;

namespace Polhem.Definition.UnitTests.Database
{
    /// <summary>
    /// Tests for <see cref="DbTableIndex.Compare"/>, which decides whether an index must be rebuilt during a schema
    /// upgrade.
    /// </summary>
    public class DbTableIndexCompareTests
    {
        [Fact]
        [DisplayName("Two indexes with the same uniqueness and fields compare as equal")]
        public void Compare_SameDefinition_ReturnsTrue()
        {
            Assert.True(Build(true, ("a", SortDirection.Asc)).Compare(Build(true, ("a", SortDirection.Asc)), DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("Indexes that differ in uniqueness do not compare as equal")]
        public void Compare_UniqueDiffers_ReturnsFalse()
        {
            Assert.False(Build(true, ("a", SortDirection.Asc)).Compare(Build(false, ("a", SortDirection.Asc)), DatabaseType.SQLite));
        }

        [Fact]
        [DisplayName("Indexes with a different number of fields do not compare as equal")]
        public void Compare_FieldCountDiffers_ReturnsFalse()
        {
            var target = Build(false, ("a", SortDirection.Asc), ("b", SortDirection.Asc));

            Assert.False(target.Compare(Build(false, ("a", SortDirection.Asc)), DatabaseType.SQLite));
        }

        [Fact]
        [DisplayName("Indexes over different fields do not compare as equal")]
        public void Compare_FieldMissing_ReturnsFalse()
        {
            Assert.False(Build(false, ("a", SortDirection.Asc)).Compare(Build(false, ("b", SortDirection.Asc)), DatabaseType.SQLite));
        }

        [Fact]
        [DisplayName("On SQL Server a different sort direction makes the indexes differ")]
        public void Compare_SortDirectionDiffersOnSqlServer_ReturnsFalse()
        {
            Assert.False(Build(false, ("a", SortDirection.Asc)).Compare(Build(false, ("a", SortDirection.Desc)), DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("On other databases the sort direction is ignored")]
        public void Compare_SortDirectionDiffersOnSqlite_ReturnsTrue()
        {
            Assert.True(Build(false, ("a", SortDirection.Asc)).Compare(Build(false, ("a", SortDirection.Desc)), DatabaseType.SQLite));
        }

        [Fact]
        [DisplayName("ToString returns the index name")]
        public void ToString_ReturnsName()
        {
            Assert.Equal("ix_test", Build(false).ToString());
        }

        private static DbTableIndex Build(bool unique, params (string Name, SortDirection Direction)[] fields)
        {
            var index = new DbTableIndex { Name = "ix_test", Unique = unique };
            foreach (var (name, direction) in fields)
                index.IndexFields.Add(name, direction);
            return index;
        }
    }
}
