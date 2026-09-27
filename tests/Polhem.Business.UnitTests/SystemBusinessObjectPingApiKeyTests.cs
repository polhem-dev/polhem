using System.ComponentModel;
using Polhem.Business.System;
using Polhem.Definition.Security;
using Polhem.Tests.Shared;

using Polhem.Definition;
namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// How <see cref="SystemBusinessObject.Ping"/> reports the API key status, and the restriction that the version is returned only
    /// with a valid key (a ping without a key should not expose the framework version to everyone).
    /// </summary>
    public class SystemBusinessObjectPingApiKeyTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public SystemBusinessObjectPingApiKeyTests(SharedDbFixture fx) { _fx = fx; }

        private PingResult PingWith(ApiKeyValidationResult validation)
        {
            var bo = new SystemBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.Empty, SysProgIds.System);
            ((IApiKeyContextAware)bo).ApiKeyValidation = validation;
            return bo.Ping(new PingArgs { TraceId = "T-1", ClientName = "unit" });
        }

        [Fact]
        [DisplayName("Ping without the key gate (an in-process call) returns NotChecked with the version")]
        public void Ping_NotChecked_ReportsStatusAndVersion()
        {
            var result = PingWith(ApiKeyValidationResult.NotChecked);

            Assert.Equal("ok", result.Status);
            Assert.Equal(ApiKeyStatus.NotChecked, result.ApiKeyStatus);
            Assert.False(string.IsNullOrEmpty(result.Version));
        }

        [Fact]
        [DisplayName("Ping in a deployment that has issued no keys returns NotConfigured and still includes the version")]
        public void Ping_NotConfigured_ReportsStatusAndVersion()
        {
            var result = PingWith(new ApiKeyValidationResult(ApiKeyStatus.NotConfigured));

            Assert.Equal("ok", result.Status);
            Assert.Equal(ApiKeyStatus.NotConfigured, result.ApiKeyStatus);
            Assert.False(string.IsNullOrEmpty(result.Version));
        }

        [Fact]
        [DisplayName("Ping with a valid key returns Valid with the version")]
        public void Ping_ValidKey_ReportsStatusAndVersion()
        {
            var result = PingWith(new ApiKeyValidationResult(ApiKeyStatus.Valid, "app", "App"));

            Assert.Equal(ApiKeyStatus.Valid, result.ApiKeyStatus);
            Assert.False(string.IsNullOrEmpty(result.Version));
        }

        [Fact]
        [DisplayName("Ping without a key (strict mode) still returns ok but without the version")]
        public void Ping_NotProvided_ReturnsOkWithoutVersion()
        {
            var result = PingWith(new ApiKeyValidationResult(ApiKeyStatus.NotProvided));

            Assert.Equal("ok", result.Status);
            Assert.Equal(ApiKeyStatus.NotProvided, result.ApiKeyStatus);
            Assert.Null(result.Version);
        }

        [Fact]
        [DisplayName("Ping with an invalid key reports Invalid without the version")]
        public void Ping_InvalidKey_ReportsInvalidWithoutVersion()
        {
            var result = PingWith(new ApiKeyValidationResult(ApiKeyStatus.Invalid, "app", string.Empty));

            Assert.Equal("ok", result.Status);
            Assert.Equal(ApiKeyStatus.Invalid, result.ApiKeyStatus);
            Assert.Null(result.Version);
        }
    }
}
