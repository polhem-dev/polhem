using System.ComponentModel;
using System.Reflection;
using Polhem.Db.CacheNotify;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Unit tests for the constructor guard of <see cref="CacheNotifyReader"/>, and for the per-database branches
    /// and the throwing default path of
    /// <see cref="CacheNotifyReader.BaselineNowCommandText"/> / <c>ThresholdBinding</c>.
    /// </summary>
    /// <remarks>
    /// NOTE: the baseline half now **calls the internal method directly** (`InternalsVisibleTo` is enabled) instead
    /// of using reflection. The reflection version turned a rename into a runtime
    /// <c>Assert.NotNull() Failure: Value is null</c>, a message that points nowhere near the real cause, while the
    /// compiler could have caught it on the spot. <c>ThresholdBinding</c> is still private and still uses reflection.
    /// </remarks>
    public class CacheNotifyReaderUnitTests
    {
        private sealed class StubDbFactory : IDbAccessFactory
        {
            public DbAccess Create(string databaseId) => throw new NotImplementedException();
        }

        private static readonly object[] s_unknownDbTypeArg = [(DatabaseType)999];

        [Fact]
        [DisplayName("CacheNotifyReader constructor throws ArgumentNullException for a null dbAccessFactory")]
        public void Constructor_NullDbAccessFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CacheNotifyReader(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("ReadBaseline throws ArgumentException for an empty databaseId")]
        public void ReadBaseline_EmptyDatabaseId_ThrowsArgumentException(string? databaseId)
        {
            var reader = new CacheNotifyReader(new StubDbFactory());
            Assert.ThrowsAny<ArgumentException>(() => reader.ReadBaseline(databaseId!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("ReadChangesSince throws ArgumentException for an empty databaseId")]
        public void ReadChangesSince_EmptyDatabaseId_ThrowsArgumentException(string? databaseId)
        {
            var reader = new CacheNotifyReader(new StubDbFactory());
            Assert.ThrowsAny<ArgumentException>(() =>
                reader.ReadChangesSince(databaseId!, DateTime.UnixEpoch));
        }

        // --- BaselineNowCommandText branches per database type ---

        // NOTE: "a known dialect returns a non-empty string" is not tested here. It now looks up `DbDialectRegistry`,
        // which `SharedDatabaseState` populates. This class has no fixture, so running first gets an empty registry
        // (order dependent, showing up as intermittent failures). `CacheNotifyBaselineBasisTests` covers it instead:
        // it has the fixture and checks a stronger condition, that the expression is identical to the writer's.

        [Fact]
        [DisplayName("BaselineNowCommandText throws NotSupportedException for an unknown database type")]
        public void BaselineNowCommandText_UnknownDatabaseType_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(
                () => CacheNotifyReader.BaselineNowCommandText((DatabaseType)999));
        }

        // --- ThresholdBinding branches per database type ---

        private static MethodInfo GetThresholdBindingMethod()
        {
            var method = typeof(CacheNotifyReader).GetMethod(
                "ThresholdBinding", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return method!;
        }

        [Theory]
        [InlineData(DatabaseType.SQLServer)]
        [InlineData(DatabaseType.PostgreSQL)]
        [InlineData(DatabaseType.MySQL)]
        [InlineData(DatabaseType.Oracle)]
        [InlineData(DatabaseType.SQLite)]
        [DisplayName("ThresholdBinding returns a non-blank Format and CastTemplate for known database types")]
        public void ThresholdBinding_KnownDatabaseType_ReturnsBothFieldsNonEmpty(DatabaseType databaseType)
        {
            var method = GetThresholdBindingMethod();
            var result = method.Invoke(null, new object[] { databaseType });
            Assert.NotNull(result);

            var (format, castTemplate) = ((string, string))result!;
            Assert.NotEmpty(format);
            Assert.NotEmpty(castTemplate);
        }

        [Fact]
        [DisplayName("ThresholdBinding throws NotSupportedException for an unknown database type")]
        public void ThresholdBinding_UnknownDatabaseType_ThrowsNotSupportedException()
        {
            var method = GetThresholdBindingMethod();
            var ex = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, s_unknownDbTypeArg));
            Assert.IsType<NotSupportedException>(ex.InnerException);
        }
    }
}
