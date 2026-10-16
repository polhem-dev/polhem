using System.ComponentModel;
using Polhem.Api.Server.Dispatch;
using Polhem.Tests.Shared;

namespace Polhem.Api.Server.UnitTests.Dispatch
{
    /// <summary>
    /// The guards of <see cref="InProcessTransportFactory"/>, the transport an in-process client reaches the dispatcher
    /// through. Dispatching through it end to end is covered by the in-process client tests in
    /// <c>Polhem.Api.Client.UnitTests</c>.
    /// </summary>
    public class InProcessTransportFactoryTests
    {
        [Fact]
        [DisplayName("Create throws ArgumentNullException for a null service provider")]
        public void Create_NullServices_ThrowsArgumentNullException()
        {
            var factory = new InProcessTransportFactory();

            Assert.Throws<ArgumentNullException>(() => factory.Create(null!, Guid.Empty));
        }

        [Fact]
        [DisplayName("Create throws InvalidOperationException naming JsonRpcDispatcher when the provider does not register it")]
        public void Create_DispatcherNotRegistered_ThrowsInvalidOperationException()
        {
            var factory = new InProcessTransportFactory();

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(EmptyServiceProvider.Instance, Guid.Empty));

            Assert.Contains("JsonRpcDispatcher", ex.Message, StringComparison.Ordinal);
        }
    }
}
