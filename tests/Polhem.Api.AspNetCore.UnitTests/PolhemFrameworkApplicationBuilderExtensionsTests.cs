using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        [DisplayName("UsePolhemFramework throws ArgumentNullException for a null app")]
        public void UsePolhemFramework_NullApp_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PolhemFrameworkApplicationBuilderExtensions.UsePolhemFramework(null!));
        }

        [Fact]
        [DisplayName("UsePolhemFramework returns the same IApplicationBuilder instance")]
        public void UsePolhemFramework_ValidApp_ReturnsSameApp()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var app = new FakeApplicationBuilder { ApplicationServices = provider };
            var result = app.UsePolhemFramework();

            Assert.Same(app, result);
        }

        [Fact]
        [DisplayName("UsePolhemFramework logs under the Polhem.Api.AspNetCore category")]
        public void UsePolhemFramework_LogsUnderAssemblyCategory()
        {
            // A deployment's logging configuration can set levels for the startup warnings by this
            // category, so it is a published name even though nothing in the framework reads it back.
            var loggerFactory = new CategoryRecordingLoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            var app = new FakeApplicationBuilder { ApplicationServices = services.BuildServiceProvider() };

            app.UsePolhemFramework();

            Assert.Equal(["Polhem.Api.AspNetCore"], loggerFactory.Categories);
        }

        private sealed class CategoryRecordingLoggerFactory : ILoggerFactory
        {
            public List<string> Categories { get; } = [];

            public ILogger CreateLogger(string categoryName)
            {
                Categories.Add(categoryName);
                return NullLogger.Instance;
            }

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }
        }
    }
}
