using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;

namespace Polhem.Definition.UnitTests.Storage
{
    /// <summary>
    /// Covers the read-only guards of <see cref="CustomizeOnlyStorage"/> not covered elsewhere:
    /// <see cref="CustomizeOnlyStorage.SaveDbCategorySettings"/> and
    /// <see cref="CustomizeOnlyStorage.SaveTableSchema"/>, which both throw <see cref="NotSupportedException"/>.
    /// </summary>
    public sealed class CustomizeOnlyStorageSaveTests
    {
        private static CustomizeOnlyStorage CreateStorage()
            => new(new CustomizeOnlyPathOptions(Path.GetTempPath(), "test-cust"));

        [Fact]
        [DisplayName("SaveDbCategorySettings throws NotSupportedException (the override layer is strictly read-only)")]
        public void SaveDbCategorySettings_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => CreateStorage().SaveDbCategorySettings(new DbCategorySettings()));
        }

        [Fact]
        [DisplayName("SaveTableSchema throws NotSupportedException (the override layer is strictly read-only)")]
        public void SaveTableSchema_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => CreateStorage().SaveTableSchema("common", new TableSchema()));
        }
    }
}
