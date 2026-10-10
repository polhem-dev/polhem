using System.ComponentModel;
using Polhem.Api.Client;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Polhem.Web.Blazor.Server.UnitTests.DependencyInjection
{
    /// <summary>
    /// Verifies that <c>AddPolhemBlazor</c> registers the resolved options as a singleton and a client per scope,
    /// and that the fluent options API picks up the chosen provider.
    /// </summary>
    public class PolhemBlazorServiceCollectionExtensionsTests
    {
        [Fact]
        [DisplayName("AddPolhemBlazor without configure defaults to Local mode")]
        public void AddPolhemBlazor_NoConfigure_DefaultsToLocal()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor();
            using var sp = services.BuildServiceProvider();

            var options = sp.GetRequiredService<PolhemBlazorOptions>();
            Assert.Equal(PolhemBlazorProviderMode.Local, options.Mode);
            Assert.Equal(string.Empty, options.Endpoint);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor with UseLocalProvider keeps Local mode")]
        public void AddPolhemBlazor_UseLocalProvider_StaysLocal()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(o => o.UseLocalProvider());
            using var sp = services.BuildServiceProvider();

            var options = sp.GetRequiredService<PolhemBlazorOptions>();
            Assert.Equal(PolhemBlazorProviderMode.Local, options.Mode);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor with UseRemoteProvider switches to Remote mode and keeps the endpoint")]
        public void AddPolhemBlazor_UseRemoteProvider_SwitchesToRemote()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(o => o.UseRemoteProvider("http://example.com/api", "app.key"));
            using var sp = services.BuildServiceProvider();

            var options = sp.GetRequiredService<PolhemBlazorOptions>();
            Assert.Equal(PolhemBlazorProviderMode.Remote, options.Mode);
            Assert.Equal("http://example.com/api", options.Endpoint);
            Assert.Equal("app.key", options.ApiKey);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor registers a PolhemApiClient per scope, in process under Local mode")]
        public void AddPolhemBlazor_Local_RegistersScopedLocalClient()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor();
            using var sp = services.BuildServiceProvider();
            using var first = sp.CreateScope();
            using var second = sp.CreateScope();

            var client = first.ServiceProvider.GetRequiredService<PolhemApiClient>();

            Assert.True(client.IsLocal);
            Assert.Same(client, first.ServiceProvider.GetRequiredService<PolhemApiClient>());
            Assert.NotSame(client, second.ServiceProvider.GetRequiredService<PolhemApiClient>());
        }

        [Fact]
        [DisplayName("Under Remote mode the circuit's client sends to the endpoint with the configured API key")]
        public void AddPolhemBlazor_Remote_ClientCarriesEndpointAndApiKey()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(o => o.UseRemoteProvider("http://example.com/api", "app.key"));
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var client = scope.ServiceProvider.GetRequiredService<PolhemApiClient>();

            Assert.False(client.IsLocal);
            Assert.Equal("http://example.com/api", client.Endpoint);
            Assert.Equal("app.key", client.ApiKey);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor registers a localizer for the components' text that serves the shipped translations")]
        public void AddPolhemBlazor_RegistersUITextLocalizer()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor();
            using var sp = services.BuildServiceProvider();
            using var culture = new CultureScope("zh-TW");

            var localizer = sp.GetRequiredService<IStringLocalizer<PolhemUIText>>();

            Assert.Equal("儲存", PolhemUIText.Get(localizer, PolhemUIText.Save));
        }

        [Fact]
        [DisplayName("A localizer the host registered before AddPolhemBlazor is kept")]
        public void AddPolhemBlazor_HostLocalizer_IsKept()
        {
            var hostLocalizer = new LanguageResourceStringLocalizer<PolhemUIText>(new FrameworkLanguageService(null));
            var services = new ServiceCollection();
            services.AddSingleton<IStringLocalizer<PolhemUIText>>(hostLocalizer);
            services.AddPolhemBlazor();
            using var sp = services.BuildServiceProvider();

            Assert.Same(hostLocalizer, sp.GetRequiredService<IStringLocalizer<PolhemUIText>>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("UseRemoteProvider rejects an empty endpoint")]
        public void UseRemoteProvider_EmptyEndpoint_Throws(string? endpoint)
        {
            var options = new PolhemBlazorOptions();
            Assert.ThrowsAny<ArgumentException>(() => options.UseRemoteProvider(endpoint!, string.Empty));
        }

        [Fact]
        [DisplayName("AddPolhemBlazor throws ArgumentNullException for null services")]
        public void AddPolhemBlazor_NullServices_Throws()
        {
            IServiceCollection? services = null;
            Assert.Throws<ArgumentNullException>(() => services!.AddPolhemBlazor());
        }
    }
}
