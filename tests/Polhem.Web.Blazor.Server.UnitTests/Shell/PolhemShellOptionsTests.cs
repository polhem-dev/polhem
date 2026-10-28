using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Shell
{
    /// <summary>
    /// Covers <see cref="PolhemShellOptions"/>, its registration through
    /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/>, and the switch address of
    /// <see cref="PolhemShellCulture"/>.
    /// </summary>
    public class PolhemShellOptionsTests
    {
        [Fact]
        [DisplayName("The default options offer English and Traditional Chinese and fall back to English")]
        public void Defaults_OfferEnglishAndTraditionalChinese()
        {
            var options = new PolhemShellOptions();

            Assert.Equal(["en-US", "zh-TW"], options.Languages.Select(l => l.Culture));
            Assert.Equal("en-US", options.ResolveDefaultCulture());
        }

        [Fact]
        [DisplayName("A default culture names an offered language in its offered spelling, whatever its case")]
        public void ResolveDefaultCulture_OfferedCulture_ReturnsOfferedSpelling()
        {
            var options = new PolhemShellOptions { DefaultCulture = "ZH-tw" };

            Assert.Equal("zh-TW", options.ResolveDefaultCulture());
        }

        [Fact]
        [DisplayName("A default culture that is not offered is rejected")]
        public void ResolveDefaultCulture_UnofferedCulture_Throws()
        {
            var options = new PolhemShellOptions { DefaultCulture = "fr-FR" };

            Assert.Throws<InvalidOperationException>(options.ResolveDefaultCulture);
        }

        [Fact]
        [DisplayName("Options without any language are rejected")]
        public void ResolveDefaultCulture_NoLanguages_Throws()
        {
            var options = new PolhemShellOptions();
            options.Languages.Clear();

            Assert.Throws<InvalidOperationException>(options.ResolveDefaultCulture);
        }

        [Fact]
        [DisplayName("AddPolhemShell registers the configured options as one instance")]
        public void AddPolhemShell_RegistersConfiguredOptions()
        {
            var services = new ServiceCollection();
            services.AddPolhemShell(shell => shell.AppTitle = "Acme");

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<PolhemShellOptions>();
            Assert.Equal("Acme", options.AppTitle);
            Assert.Same(options, provider.GetRequiredService<PolhemShellOptions>());
        }

        [Fact]
        [DisplayName("AddPolhemShell fails at registration when the default culture is not offered")]
        public void AddPolhemShell_UnofferedDefaultCulture_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() => services.AddPolhemShell(shell => shell.DefaultCulture = "fr-FR"));
        }

        [Theory]
        [InlineData("form", "Order", "form/Order")]
        [InlineData("/forms/", "Order", "forms/Order")]
        [InlineData("form", "a b/c", "form/a%20b%2Fc")]
        [DisplayName("GetFormUri joins the form route and the escaped program id")]
        public void GetFormUri_JoinsRouteAndEscapedProgId(string route, string progId, string expected)
        {
            var options = new PolhemShellOptions { FormRoute = route };

            Assert.Equal(expected, options.GetFormUri(progId));
        }

        [Theory]
        [InlineData("/", true)]
        [InlineData("/form/Order", true)]
        [InlineData("/form/Order?x=1", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("form/Order", false)]
        [InlineData("https://evil.example/", false)]
        [InlineData("//evil.example/", false)]
        [InlineData("/\\evil.example/", false)]
        [InlineData("/\r\n//evil.example/", false)]
        [DisplayName("Only a path on this site counts as a local redirect")]
        public void IsLocalPath_AcceptsOnlySitePaths(string? redirectUri, bool expected)
        {
            Assert.Equal(expected, PolhemShellCulture.IsLocalPath(redirectUri));
        }

        [Fact]
        [DisplayName("The switch address escapes the culture and the return path")]
        public void BuildSwitchUri_EscapesArguments()
        {
            Assert.Equal("culture?culture=zh-TW&redirectUri=%2Fform%2FOrder%3Fx%3D1",
                PolhemShellCulture.BuildSwitchUri("zh-TW", "/form/Order?x=1"));
        }
    }
}
