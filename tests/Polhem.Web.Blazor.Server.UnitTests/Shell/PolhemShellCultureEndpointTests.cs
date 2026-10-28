using System.ComponentModel;
using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Shell
{
    /// <summary>
    /// Runs <see cref="PolhemShellEndpointRouteBuilderExtensions.MapPolhemShellCulture"/> and
    /// <see cref="PolhemShellApplicationBuilderExtensions.UsePolhemShellLocalization"/> in a real host on a loopback
    /// port, and calls them over HTTP with redirects off, so each test sees the endpoint's own answer.
    /// </summary>
    public sealed class PolhemShellCultureEndpointTests : IAsyncLifetime
    {
        private WebApplication? _app;
        private HttpClient? _http;

        private HttpClient Http => _http!;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddPolhemShell(shell => shell.DefaultCulture = "zh-TW");
            _app = builder.Build();
            _app.UsePolhemShellLocalization();
            _app.MapPolhemShellCulture();
            _app.MapGet("/whoami", () => CultureInfo.CurrentUICulture.Name);
            await _app.StartAsync();

            string address = _app.Services.GetRequiredService<IServer>().Features
                .GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                BaseAddress = new Uri(address),
            };
        }

        public async Task DisposeAsync()
        {
            _http?.Dispose();
            if (_app is not null)
                await _app.DisposeAsync();
        }

        private static string? CultureCookie(HttpResponseMessage response)
            => response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.SingleOrDefault(v => v.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal))
                : null;

        [Fact]
        [DisplayName("An offered culture is stored in the culture cookie and the endpoint returns to the page")]
        public async Task Culture_OfferedCulture_SetsCookieAndRedirectsBack()
        {
            using var response = await Http.GetAsync("/culture?culture=en-US&redirectUri=%2Fform%2FOrder");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/form/Order", response.Headers.Location?.OriginalString);
            string? cookie = CultureCookie(response);
            Assert.NotNull(cookie);
            Assert.Contains("c%3Den-US%7Cuic%3Den-US", cookie, StringComparison.Ordinal);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [DisplayName("A culture that is not offered stores nothing")]
        public async Task Culture_UnofferedCulture_SetsNoCookie()
        {
            using var response = await Http.GetAsync("/culture?culture=fr-FR&redirectUri=%2F");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Null(CultureCookie(response));
        }

        [Theory]
        [InlineData("https%3A%2F%2Fevil.example%2F")]
        [InlineData("%2F%2Fevil.example%2F")]
        [InlineData("%2F%5Cevil.example%2F")]
        [InlineData("")]
        [DisplayName("A return address that is not a path on this site returns to the root instead")]
        public async Task Culture_ForeignRedirect_ReturnsToRoot(string redirectUri)
        {
            using var response = await Http.GetAsync($"/culture?culture=en-US&redirectUri={redirectUri}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.OriginalString);
        }

        [Fact]
        [DisplayName("A request without a culture cookie gets the default culture")]
        public async Task Localization_NoCookie_UsesDefaultCulture()
        {
            Assert.Equal("zh-TW", await Http.GetStringAsync("/whoami"));
        }

        [Fact]
        [DisplayName("A request with the culture cookie gets that culture")]
        public async Task Localization_CultureCookie_UsesCookieCulture()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
            request.Headers.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US");

            using var response = await Http.SendAsync(request);

            Assert.Equal("en-US", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        [DisplayName("The shell's pipeline extensions name AddPolhemShell when it was not called")]
        public void Extensions_WithoutAddPolhemShell_Throw()
        {
            var app = WebApplication.CreateBuilder().Build();

            var ex = Assert.Throws<InvalidOperationException>(() => app.MapPolhemShellCulture());
            Assert.Contains("AddPolhemShell", ex.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => app.UsePolhemShellLocalization());
        }
    }
}
