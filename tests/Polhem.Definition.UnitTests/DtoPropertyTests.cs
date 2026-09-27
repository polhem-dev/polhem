using System.ComponentModel;
using Polhem.Definition.Identity;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// Basic property tests for DTOs such as SessionInfo, SessionUser and UserInfo.
    /// </summary>
    public class DtoPropertyTests
    {
        [Fact]
        [DisplayName("SessionInfo GetKey returns the AccessToken as a string")]
        public void SessionInfo_GetKey_ReturnsAccessTokenString()
        {
            // Arrange
            var token = Guid.NewGuid();
            var info = new SessionInfo { AccessToken = token };

            // Act & Assert
            Assert.Equal(token.ToString(), info.GetKey());
        }

        [Fact]
        [DisplayName("SessionInfo defaults to an empty Guid and an empty culture and time zone")]
        public void SessionInfo_Defaults_ReturnsExpectedValues()
        {
            // Act
            var info = new SessionInfo();

            // Assert
            Assert.Equal(Guid.Empty, info.AccessToken);
            Assert.Null(info.CompanyId);
            // Empty means unspecified: the actual culture and time zone are filled in at login
            // (from st_user.culture / st_user.time_zone, otherwise DefaultLanguage / DefaultTimeZone).
            Assert.Empty(info.Culture);
            Assert.Empty(info.TimeZone);
            Assert.Empty(info.ApiEncryptionKey);
        }

        [Fact]
        [DisplayName("CompanyInfo GetKey returns CompanyId")]
        public void CompanyInfo_GetKey_ReturnsCompanyId()
        {
            // Arrange
            var info = new CompanyInfo
            {
                CompanyId = "C001",
                CompanyName = "Acme",
                CompanyDatabaseId = "biz_shared_01"
            };

            // Act & Assert
            Assert.Equal("C001", info.GetKey());
        }

        [Fact]
        [DisplayName("CompanyInfo default constructor produces empty string fields")]
        public void CompanyInfo_Defaults_ReturnsEmptyStrings()
        {
            // Act
            var info = new CompanyInfo();

            // Assert
            Assert.Equal(string.Empty, info.CompanyId);
            Assert.Equal(string.Empty, info.CompanyName);
            Assert.Equal(string.Empty, info.CompanyDatabaseId);
        }

        [Fact]
        [DisplayName("CompanyInfo ToString returns the 'CompanyId : CompanyName' format")]
        public void CompanyInfo_ToString_ReturnsFormattedString()
        {
            // Arrange
            var info = new CompanyInfo { CompanyId = "C001", CompanyName = "Acme" };

            // Act & Assert
            Assert.Equal("C001 : Acme", info.ToString());
        }

        [Fact]
        [DisplayName("SessionInfo ToString returns the 'UserId : UserName' format")]
        public void SessionInfo_ToString_ReturnsFormattedString()
        {
            // Arrange
            var info = new SessionInfo { UserId = "U01", UserName = "Alice" };

            // Act & Assert
            Assert.Equal("U01 : Alice", info.ToString());
        }

        [Fact]
        [DisplayName("SessionUser ToString returns the 'UserId : UserName' format")]
        public void SessionUser_ToString_ReturnsFormattedString()
        {
            // Arrange
            var user = new SessionUser { UserId = "U02", UserName = "Bob" };

            // Act & Assert
            Assert.Equal("U02 : Bob", user.ToString());
        }

        [Fact]
        [DisplayName("SessionUser defaults to an empty Guid and DateTime.MinValue")]
        public void SessionUser_Defaults_ReturnsExpectedValues()
        {
            // Act
            var user = new SessionUser();

            // Assert
            Assert.Equal(Guid.Empty, user.AccessToken);
            Assert.Equal(DateTime.MinValue, user.EndTime);
        }

        [Fact]
        [DisplayName("UserInfo defaults to an empty culture and an empty time zone, both supplied by the server at login")]
        public void UserInfo_Defaults_ReturnsExpectedCultureAndTimeZone()
        {
            // Act
            var user = new UserInfo();

            // Assert
            // Empty means the client keeps its own UI culture until login supplies the user's.
            Assert.Empty(user.Culture);
            // Empty means UTC: the actual time zone is supplied by the server at login.
            Assert.Empty(user.TimeZone);
        }
    }
}
