using System.ComponentModel;
using System.Text.RegularExpressions;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Pins the JSON a successful call answers with, member for member.
    /// </summary>
    /// <remarks>
    /// Clients in other languages read this shape, including the <c>method</c> member that JSON-RPC 2.0 does not
    /// define. The expected text is what the executor the dispatcher replaced wrote for the same request, captured
    /// when the two paths ran side by side; any change to it is a change to the wire.
    /// </remarks>
    public class WireShapeTests : IClassFixture<PolhemTestFixture>
    {
        private static readonly Regex s_serverTime = new("\"serverTime\":\"[^\"]*\"", RegexOptions.None, TimeSpan.FromSeconds(1));

        private readonly PolhemTestFixture _fx;

        public WireShapeTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        [Fact]
        [DisplayName("A plain Ping written by hand, as a JavaScript client sends it, is answered in the pinned shape")]
        public async Task HandWrittenPlainPing_AnswersInPinnedShape()
        {
            const string Request = """{"jsonrpc":"2.0","method":"System.Ping","params":{"format":0,"value":{"clientName":"app","traceId":"t-1"}},"id":"1"}""";
            const string Expected = """{"jsonrpc":"2.0","method":"System.Ping","result":{"format":0,"value":{"status":"ok","serverTime":"*","version":"1.0.0","traceId":"t-1"},"type":""},"id":"1"}""";

            var response = await new TestDispatcher(_fx.Provider).ExecuteJsonAsync(Request);

            Assert.Equal(Expected, s_serverTime.Replace(response.Json, "\"serverTime\":\"*\""));
        }
    }
}
