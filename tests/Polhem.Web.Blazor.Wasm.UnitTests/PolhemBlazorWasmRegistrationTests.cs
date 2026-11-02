using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.UI.Core;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;
using Polhem.Web.Blazor.Wasm.DependencyInjection;

namespace Polhem.Web.Blazor.Wasm.UnitTests
{
    public class PolhemBlazorWasmRegistrationTests
    {
        [Fact]
        [DisplayName("AddPolhemBlazor registers a remote client on the configured endpoint and the services components read")]
        public void AddPolhemBlazor_WithEndpoint_RegistersRemoteClientAndComponentServices()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(options =>
            {
                options.Endpoint = "https://example.test/api";
                options.ApiKey = "key";
                options.DefaultLanguage = "en-US";
            });
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var client = scope.ServiceProvider.GetRequiredService<PolhemApiClient>();
            Assert.False(client.IsLocal);
            Assert.Equal("https://example.test/api", client.Endpoint);
            Assert.Equal("en-US", scope.ServiceProvider.GetRequiredService<IBlazorDefinitionOptions>().DefaultLanguage);
            Assert.Same(scope.ServiceProvider.GetRequiredService<BlazorUserContext>(),
                scope.ServiceProvider.GetRequiredService<IUserContext>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStringLocalizer<PolhemUIText>>());
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [DisplayName("AddPolhemBlazor refuses a blank endpoint at registration")]
        public void AddPolhemBlazor_BlankEndpoint_Throws(string endpoint)
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentException>(() => services.AddPolhemBlazor(options => options.Endpoint = endpoint));
        }
    }
}
