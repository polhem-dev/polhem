using System.ComponentModel;
using System.Globalization;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Server.Dispatch;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Api.Server.UnitTests.Dispatch
{
    /// <summary>
    /// A call without a session reports its messages in the language the caller asks for: the
    /// <c>Accept-Language</c> header over HTTP, and the caller's UI culture in process. The test deployment's default
    /// language is zh-TW, so an English answer shows the caller's language was used.
    /// </summary>
    /// <remarks>
    /// A user ID longer than sign-in accepts is refused before any database read, so these calls need no database.
    /// </remarks>
    public class RequestCultureTests : IClassFixture<PolhemTestFixture>
    {
        private const string English = "Invalid username or password.";
        private const string Chinese = "帳號或密碼錯誤。";

        private readonly PolhemTestFixture _fx;

        public RequestCultureTests(PolhemTestFixture fx) { _fx = fx; }

        private static TestRpcRequest FailedLogin() => new()
        {
            Method = $"{SysProgIds.System}.Login",
            Params = new TestPayload { Value = new LoginRequest { UserId = new string('x', 500), Password = "x" } },
            Id = "1",
        };

        [Theory]
        [InlineData("en-US,en;q=0.9", English)]
        [InlineData("zh-TW", Chinese)]
        [InlineData(null, Chinese)]
        [InlineData("not a language", Chinese)]
        [DisplayName("A failed sign-in over HTTP is reported in the first language of Accept-Language, or the default without a usable one")]
        public async Task HttpLogin_Failed_UsesAcceptLanguage(string? acceptLanguage, string expected)
        {
            var dispatcher = new TestDispatcher(_fx.Provider) { IsLocalCall = false, AcceptLanguage = acceptLanguage };

            var response = await dispatcher.ExecuteAsync(FailedLogin());

            Assert.Equal(expected, response.Error!.Message);
        }

        [Theory]
        [InlineData("en-US", English)]
        [InlineData("zh-TW", Chinese)]
        [DisplayName("A failed sign-in in process is reported in the caller's UI culture")]
        public async Task InProcessLogin_Failed_UsesCallerUICulture(string uiCulture, string expected)
        {
            var original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);

                var response = await new TestDispatcher(_fx.Provider).ExecuteAsync(FailedLogin());

                Assert.Equal(expected, response.Error!.Message);
            }
            finally
            {
                CultureInfo.CurrentUICulture = original;
            }
        }

        [Theory]
        [InlineData("en-US,en;q=0.9", "en-US")]
        [InlineData(" zh-TW ;q=1", "zh-TW")]
        [InlineData("fr", "fr")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("*", "")]
        [InlineData("not a language", "")]
        [DisplayName("Accept-Language yields its first known culture, and nothing for a value that names none")]
        public void FromAcceptLanguage_ReturnsFirstKnownCulture(string? header, string expected)
        {
            Assert.Equal(expected, RequestCultureParser.FromAcceptLanguage(header));
        }

        [Fact]
        [DisplayName("An Accept-Language tag longer than any language tag is ignored before the culture lookup")]
        public void FromAcceptLanguage_OverlongTag_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, RequestCultureParser.FromAcceptLanguage("en-" + new string('a', 200)));
        }
    }
}
