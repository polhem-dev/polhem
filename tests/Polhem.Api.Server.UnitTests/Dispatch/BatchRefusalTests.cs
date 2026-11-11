using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Server.Dispatch;
using Polhem.JsonRpc;

namespace Polhem.Api.Server.UnitTests.Dispatch
{
    /// <summary>
    /// The framework's server options refuse JSON-RPC batches, so one HTTP request carries one call.
    /// </summary>
    /// <remarks>
    /// A rate limit in front of the server counts HTTP requests. With batches, one request carried many anonymous
    /// sign-ins, each a full password key derivation.
    /// </remarks>
    public class BatchRefusalTests
    {
        private const string TwoCalls =
            """[{"jsonrpc":"2.0","method":"System.Ping","params":{},"id":"1"},{"jsonrpc":"2.0","method":"System.Ping","params":{},"id":"2"}]""";

        [Fact]
        [DisplayName("The framework's server options refuse every batch")]
        public void CreateServerOptions_RefusesBatches()
        {
            Assert.True(PolhemJsonRpc.CreateServerOptions().MaxBatchSize <= 0);
        }

        [Fact]
        [DisplayName("A batch sent to the framework's pipeline is answered with one invalid-request error and no call runs")]
        public async Task Dispatch_Batch_AnsweredWithOneInvalidRequestError()
        {
            var services = TestDispatcher.AddDispatchDefaults(new ServiceCollection()).BuildServiceProvider();

            var response = await new TestDispatcher(services).ExecuteJsonAsync(TwoCalls);

            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, response.Error!.Code);
            Assert.Null(response.Result);
        }
    }
}
