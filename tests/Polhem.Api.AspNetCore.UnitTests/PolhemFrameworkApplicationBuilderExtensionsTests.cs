using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Api.AspNetCore.UnitTests
{
    public class PolhemFrameworkApplicationBuilderExtensionsTests
    {
        private sealed class FakeApplicationBuilder : IApplicationBuilder
        {
            public IServiceProvider ApplicationServices { get; set; } = null!;
            public IFeatureCollection ServerFeatures => null!;
            public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
            public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => this;
            public IApplicationBuilder New() => this;
            public RequestDelegate Build() => _ => Task.CompletedTask;
        }

        [Fact]
        [DisplayName("UsePolhemFramework 傳入 null 應拋出 ArgumentNullException")]
        public void UsePolhemFramework_NullApp_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PolhemFrameworkApplicationBuilderExtensions.UsePolhemFramework(null!));
        }

        [Fact]
        [DisplayName("UsePolhemFramework 應回傳相同 IApplicationBuilder 實例（Phase 7 後為 no-op）")]
        public void UsePolhemFramework_ValidApp_ReturnsSameApp()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var app = new FakeApplicationBuilder { ApplicationServices = provider };
            var result = app.UsePolhemFramework();

            Assert.Same(app, result);
        }
    }
}
