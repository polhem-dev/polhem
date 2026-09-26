using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Polhem.Api.AspNetCore.UnitTests
{
    /// <summary>
    /// Tests the <see cref="Controllers.ApiServiceController.IsDevelopment"/> property.
    /// A hand-written <c>FakeServiceProvider</c> injects <see cref="IHostEnvironment"/>, so neither
    /// PolhemTestFixture nor the shared backend DI container is needed.
    /// </summary>
    public class ApiServiceControllerIsDevelopmentTests
    {
        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public required string EnvironmentName { get; set; }
            public string ApplicationName { get; set; } = string.Empty;
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private sealed class FakeServiceProvider : IServiceProvider
        {
            private readonly IHostEnvironment _env;
            public FakeServiceProvider(IHostEnvironment env) => _env = env;
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IHostEnvironment) ? _env : null;
        }

        private sealed class TestableController : Controllers.ApiServiceController
        {
            public bool GetIsDevelopment() => IsDevelopment;
        }

        private static TestableController CreateController(string environmentName)
        {
            var env = new FakeHostEnvironment { EnvironmentName = environmentName };
            var context = new DefaultHttpContext { RequestServices = new FakeServiceProvider(env) };
            return new TestableController { ControllerContext = new ControllerContext { HttpContext = context } };
        }

        [Fact]
        [DisplayName("IsDevelopment returns true in the Development environment")]
        public void IsDevelopment_DevelopmentEnvironment_ReturnsTrue()
        {
            var controller = CreateController(Environments.Development);
            Assert.True(controller.GetIsDevelopment());
        }

        [Fact]
        [DisplayName("IsDevelopment returns false in the Production environment")]
        public void IsDevelopment_ProductionEnvironment_ReturnsFalse()
        {
            var controller = CreateController(Environments.Production);
            Assert.False(controller.GetIsDevelopment());
        }
    }
}
