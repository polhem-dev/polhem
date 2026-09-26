using System.ComponentModel;
using Polhem.Base.Security;

namespace Polhem.Base.UnitTests
{
    /// <summary>
    /// Unit tests for ApiKeyFormat: composing and parsing the two-part {sysId}.{secret} format, and validating the sys_id character set.
    /// </summary>
    public class ApiKeyFormatTests
    {
        [Fact]
        [DisplayName("CreateSecret produces a URL-safe 256-bit secret that differs every time")]
        public void CreateSecret_ProducesUrlSafeUniqueValues()
        {
            string first = ApiKeyFormat.CreateSecret();
            string second = ApiKeyFormat.CreateSecret();

            Assert.NotEqual(first, second);
            // 43 characters is the base64url length of 32 bytes without padding.
            Assert.Equal(43, first.Length);
            Assert.DoesNotContain('+', first);
            Assert.DoesNotContain('/', first);
            Assert.DoesNotContain('=', first);
            Assert.DoesNotContain(ApiKeyFormat.Separator, first);
        }

        [Fact]
        [DisplayName("TryParse after Compose restores the original two parts")]
        public void Compose_ThenTryParse_RoundTrips()
        {
            string secret = ApiKeyFormat.CreateSecret();
            string key = ApiKeyFormat.Compose("northwind-desktop", secret);

            bool parsed = ApiKeyFormat.TryParse(key, out string sysId, out string parsedSecret);

            Assert.True(parsed);
            Assert.Equal("northwind-desktop", sysId);
            Assert.Equal(secret, parsedSecret);
        }

        [Theory]
        [DisplayName("IsValidSysId accepts valid identifiers")]
        [InlineData("abc")]
        [InlineData("northwind-desktop")]
        [InlineData("vendor-x-2026")]
        public void IsValidSysId_ValidValues_ReturnsTrue(string sysId)
        {
            Assert.True(ApiKeyFormat.IsValidSysId(sysId));
        }

        [Theory]
        [DisplayName("IsValidSysId rejects invalid identifiers")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ab")]                    // shorter than the minimum
        [InlineData("-leading")]              // leading hyphen
        [InlineData("trailing-")]             // trailing hyphen
        [InlineData("Has-Upper")]             // uppercase
        [InlineData("has.dot")]               // contains the separator, which would make splitting ambiguous
        [InlineData("has_underscore")]
        [InlineData("has space")]
        public void IsValidSysId_InvalidValues_ReturnsFalse(string? sysId)
        {
            Assert.False(ApiKeyFormat.IsValidSysId(sysId));
        }

        [Fact]
        [DisplayName("IsValidSysId rejects an identifier longer than the maximum length")]
        public void IsValidSysId_TooLong_ReturnsFalse()
        {
            string sysId = new string('a', ApiKeyFormat.MaxSysIdLength + 1);

            Assert.False(ApiKeyFormat.IsValidSysId(sysId));
        }

        [Theory]
        [DisplayName("TryParse returns false and outputs no parts for a malformed key")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no-separator")]
        [InlineData(".leading-separator")]
        [InlineData("trailing-separator.")]
        [InlineData("Bad-SysId.secret")]
        public void TryParse_Malformed_ReturnsFalse(string? key)
        {
            bool parsed = ApiKeyFormat.TryParse(key, out string sysId, out string secret);

            Assert.False(parsed);
            Assert.Equal(string.Empty, sysId);
            Assert.Equal(string.Empty, secret);
        }

        [Fact]
        [DisplayName("TryParse splits on the first separator, so separators inside the secret do not matter")]
        public void TryParse_SplitsOnFirstSeparator()
        {
            bool parsed = ApiKeyFormat.TryParse("app-id.secret.with.dots", out string sysId, out string secret);

            Assert.True(parsed);
            Assert.Equal("app-id", sysId);
            Assert.Equal("secret.with.dots", secret);
        }
    }
}
