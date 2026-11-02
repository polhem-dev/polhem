using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Polhem.Web.Blazor.Shell;
using Polhem.Web.Blazor.Wasm.Shell;

namespace Polhem.Web.Blazor.Wasm.UnitTests
{
    public class PolhemShellWasmTests
    {
        [Theory]
        [InlineData("zh-TW", "en-US", "zh-TW")]
        [InlineData("ZH-tw", "en-US", "zh-TW")]
        [InlineData("fr-FR", "zh-TW", "zh-TW")]
        [InlineData(null, "zh-TW", "zh-TW")]
        [InlineData(null, "de-DE", "en-US")]
        [DisplayName("The starting culture is the stored one, then the browser's, then the default")]
        public void SelectCulture_ByPrecedence_ReturnsExpected(string? stored, string browser, string expected)
        {
            string culture = PolhemShellServiceProviderExtensions.SelectCulture(
                new PolhemShellOptions(), stored, CultureInfo.GetCultureInfo(browser));

            Assert.Equal(expected, culture);
        }

        [Fact]
        [DisplayName("A browser language falls back through its parents to an offered language")]
        public void SelectCulture_BrowserChild_FallsBackToParent()
        {
            var options = new PolhemShellOptions();
            options.Languages.Add(new PolhemShellLanguage("fr", "Français"));

            Assert.Equal("fr", PolhemShellServiceProviderExtensions.SelectCulture(
                options, null, CultureInfo.GetCultureInfo("fr-CA")));
        }

        [Fact]
        [DisplayName("Switching language stores the culture and reloads the current page")]
        public void Switch_StoresCultureAndForcesReload()
        {
            var js = new RecordingJSRuntime();
            var navigation = new RecordingNavigationManager();
            var switcher = new ShellLanguageSwitcher(js, navigation);

            switcher.Switch(new PolhemShellLanguage("zh-TW", "繁體中文"));

            Assert.Equal(ShellLanguageSwitcher.StorageKey, js.Stored.Key);
            Assert.Equal("zh-TW", js.Stored.Value);
            Assert.True(navigation.ForcedLoad);
            Assert.Equal(navigation.Uri, navigation.Target);
        }

        [Fact]
        [DisplayName("AddPolhemShell registers the language switch and rejects a default culture it does not offer")]
        public void AddPolhemShell_RegistersSwitcher_AndValidatesDefaultCulture()
        {
            var services = new ServiceCollection();
            services.AddPolhemShell();
            Assert.Contains(services, d => d.ServiceType == typeof(IShellLanguageSwitcher));

            Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPolhemShell(
                options => options.DefaultCulture = "fr-FR"));
        }

        [Fact]
        [DisplayName("ApplyPolhemShellCulture without AddPolhemShell says what is missing")]
        public void ApplyPolhemShellCulture_ShellNotRegistered_Throws()
        {
            using var provider = new ServiceCollection().BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(() => provider.ApplyPolhemShellCulture());
        }

        private sealed class RecordingJSRuntime : IJSInProcessRuntime
        {
            public KeyValuePair<string?, string?> Stored { get; private set; }

            public TResult Invoke<TResult>(string identifier, params object?[]? args)
            {
                if (identifier == "localStorage.setItem")
                    Stored = new KeyValuePair<string?, string?>((string?)args![0], (string?)args[1]);
                return default!;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
                => throw new NotSupportedException();

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => throw new NotSupportedException();
        }

        private sealed class RecordingNavigationManager : NavigationManager
        {
            public RecordingNavigationManager() => Initialize("https://example.test/", "https://example.test/form/Order");

            public string? Target { get; private set; }

            public bool ForcedLoad { get; private set; }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                Target = uri;
                ForcedLoad = forceLoad;
            }
        }
    }
}
