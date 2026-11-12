using System.ComponentModel;

namespace Polhem.Api.Client.UnitTests
{
    public class HttpUtilitiesTests
    {
        [Theory]
        [InlineData("http://example.com", true)]
        [InlineData("https://example.com/path?x=1", true)]
        [InlineData("HTTP://example.com", true)]
        [InlineData("ftp://example.com", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData("/local/path", false)]
        [DisplayName("IsUrl returns true only for absolute http/https URLs")]
        public void IsUrl_RecognizesHttpSchemes(string input, bool expected)
        {
            Assert.Equal(expected, HttpUtilities.IsUrl(input));
        }

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, true)]
        [DisplayName("UsesPlatformDefaultHandler is false only for desktop and server hosts")]
        public void UsesPlatformDefaultHandler_ByPlatform_ReturnsExpected(bool isBrowser, bool isAndroid, bool isAppleMobile, bool expected)
        {
            Assert.Equal(expected, HttpUtilities.UsesPlatformDefaultHandler(isBrowser, isAndroid, isAppleMobile));
        }

        [Fact]
        [DisplayName("UsesPlatformDefaultHandler keeps SocketsHttpHandler on the desktop test host")]
        public void UsesPlatformDefaultHandler_DesktopHost_ReturnsFalse()
        {
            Assert.False(HttpUtilities.UsesPlatformDefaultHandler());
        }
    }
}
