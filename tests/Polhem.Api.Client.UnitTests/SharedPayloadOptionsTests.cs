using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// An in-process client that holds the backend's own payload options must leave them to the backend.
    /// </summary>
    public class SharedPayloadOptionsTests
    {
        [Fact]
        [DisplayName("A local client given the backend's registered payload options shares them")]
        public void CreateLocal_BackendsOwnOptions_Shares()
        {
            var options = PolhemPayload.CreateOptions();
            using var services = new ServiceCollection().AddSingleton(options).BuildServiceProvider();

            Assert.True(PolhemApiClient.CreateLocal(services, options).SharesServerPayloadOptions);
        }

        [Fact]
        [DisplayName("A local client with options of its own, or with none, does not share the backend's")]
        public void CreateLocal_OwnOptions_DoesNotShare()
        {
            using var services = new ServiceCollection().AddSingleton(PolhemPayload.CreateOptions()).BuildServiceProvider();

            Assert.False(PolhemApiClient.CreateLocal(services, PolhemPayload.CreateOptions()).SharesServerPayloadOptions);
            Assert.False(PolhemApiClient.CreateLocal(services).SharesServerPayloadOptions);
        }
    }
}
