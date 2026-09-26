using System.ComponentModel;
using Polhem.Db.CacheNotify;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Pure unit tests for the argument validation of <see cref="CacheNotifyService"/> (no database connection).
    /// They cover the argument guards of <c>TouchAsync</c>: the existing database integration tests only call
    /// <c>Touch</c>, so the guard lines of <c>TouchAsync</c> never ran.
    /// </summary>
    public class CacheNotifyServiceArgumentTests
    {
        private static readonly CacheNotifyService s_service = new();

        #region TouchAsync argument guards

        [Fact]
        [DisplayName("TouchAsync throws ArgumentNullException for a null cacheKey")]
        public async Task TouchAsync_NullCacheKey_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => s_service.TouchAsync(null!, null!, DatabaseType.SQLServer));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("TouchAsync throws ArgumentException for a blank cacheKey")]
        public async Task TouchAsync_WhitespaceCacheKey_ThrowsArgumentException(string cacheKey)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => s_service.TouchAsync(cacheKey, null!, DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("TouchAsync throws ArgumentNullException for a null transaction")]
        public async Task TouchAsync_NullTransaction_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => s_service.TouchAsync("key", null!, DatabaseType.SQLServer));
        }

        #endregion

        #region Touch argument guards

        [Fact]
        [DisplayName("Touch throws ArgumentNullException for a null cacheKey")]
        public void Touch_NullCacheKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => s_service.Touch(null!, null!, DatabaseType.SQLServer));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("Touch throws ArgumentException for a blank cacheKey")]
        public void Touch_WhitespaceCacheKey_ThrowsArgumentException(string cacheKey)
        {
            Assert.Throws<ArgumentException>(() => s_service.Touch(cacheKey, null!, DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("Touch throws ArgumentNullException for a null transaction")]
        public void Touch_NullTransaction_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => s_service.Touch("key", null!, DatabaseType.SQLServer));
        }

        #endregion
    }
}
