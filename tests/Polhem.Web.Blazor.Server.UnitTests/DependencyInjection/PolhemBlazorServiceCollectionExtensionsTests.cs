using System.ComponentModel;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Web.Blazor.Server.UnitTests.DependencyInjection
{
    /// <summary>
    /// Verifies that <c>AddPolhemBlazor</c> registers the resolved options and factory
    /// as singletons, and that the fluent options API picks up the chosen provider.
    /// </summary>
    public class PolhemBlazorServiceCollectionExtensionsTests
    {
        [Fact]
        [DisplayName("AddPolhemBlazor 不帶 configure 時預設 Local 模式")]
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
        [DisplayName("AddPolhemBlazor + UseLocalProvider 維持 Local 模式")]
        public void AddPolhemBlazor_UseLocalProvider_StaysLocal()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(o => o.UseLocalProvider());
            using var sp = services.BuildServiceProvider();

            var options = sp.GetRequiredService<PolhemBlazorOptions>();
            Assert.Equal(PolhemBlazorProviderMode.Local, options.Mode);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor + UseRemoteProvider 切換為 Remote 模式並保留 endpoint")]
        public void AddPolhemBlazor_UseRemoteProvider_SwitchesToRemote()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor(o => o.UseRemoteProvider("http://example.com/api"));
            using var sp = services.BuildServiceProvider();

            var options = sp.GetRequiredService<PolhemBlazorOptions>();
            Assert.Equal(PolhemBlazorProviderMode.Remote, options.Mode);
            Assert.Equal("http://example.com/api", options.Endpoint);
        }

        [Fact]
        [DisplayName("AddPolhemBlazor 註冊 PolhemApiConnectorFactory")]
        public void AddPolhemBlazor_RegistersConnectorFactory()
        {
            var services = new ServiceCollection();
            services.AddPolhemBlazor();
            using var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<PolhemApiConnectorFactory>();
            Assert.Equal(PolhemBlazorProviderMode.Local, factory.Mode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("UseRemoteProvider 拒絕空 endpoint")]
        public void UseRemoteProvider_EmptyEndpoint_Throws(string? endpoint)
        {
            var options = new PolhemBlazorOptions();
            Assert.ThrowsAny<ArgumentException>(() => options.UseRemoteProvider(endpoint!));
        }

        [Fact]
        [DisplayName("AddPolhemBlazor 對 null services 拋 ArgumentNullException")]
        public void AddPolhemBlazor_NullServices_Throws()
        {
            IServiceCollection? services = null;
            Assert.Throws<ArgumentNullException>(() => services!.AddPolhemBlazor());
        }
    }
}
