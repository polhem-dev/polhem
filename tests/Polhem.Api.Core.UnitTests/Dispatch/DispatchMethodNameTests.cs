using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// How the framework's JSON-RPC pipeline answers the method name: its shape, an unknown action, a progId with no
    /// form, and the members it echoes.
    /// </summary>
    // These tests assert masked messages, and masking depends on the process-wide static `SysInfo.IsDebugMode`.
    // So they are serialized with the test classes that toggle that flag (see `SysInfoStaticCollection`).
    [Collection(SysInfoStaticCollection.Name)]
    public class DispatchMethodNameTests : IClassFixture<PolhemTestFixture>
    {
        /// <summary>The longest progId or action the method name accepts.</summary>
        private const int MaxMethodPartLength = 64;

        private readonly PolhemTestFixture _fx;

        public DispatchMethodNameTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private TestDispatcher NewDispatcher(Guid accessToken) => new(_fx.Provider) { AccessToken = accessToken };

        private Task<TestRpcResponse> SendAsync(string method, object? value = null)
            => NewDispatcher(Guid.Empty).ExecuteAsync(new TestRpcRequest
            {
                Method = method,
                Params = new TestPayload { Value = value },
                Id = "1",
            });

        [Fact]
        [DisplayName("A Ping call completes")]
        public async Task Ping_Succeeds()
        {
            var response = await SendAsync($"{SysProgIds.System}.Ping", new PingRequest { ClientName = "T", TraceId = "X" });

            Assert.Null(response.Error);
            Assert.IsType<PingResponse>(response.Result!.Value);
        }

        [Theory]
        [InlineData("InvalidMethodFormat")]
        [InlineData("")]
        [InlineData(".Ping")]
        [InlineData("<script>.GetList")]
        [InlineData("Order.Get List")]
        [InlineData("Order.Save.Extra")]
        [InlineData("Order.")]
        [DisplayName("A method name of the wrong shape is answered with MethodNotFound without being echoed back")]
        public async Task MalformedMethod_ReturnsMethodNotFound(string method)
        {
            var response = await SendAsync(method);

            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal((int)JsonRpcErrorCode.MethodNotFound, response.Error!.Code);
            if (method.Length > 0)
            {
                Assert.DoesNotContain(method, response.Error.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        [DisplayName("A progId longer than the limit is answered with MethodNotFound without being echoed back")]
        public async Task OverlongProgId_ReturnsMethodNotFoundWithoutEcho()
        {
            string progId = new('A', MaxMethodPartLength + 1);

            var response = await SendAsync(progId + ".GetList");

            Assert.Equal((int)JsonRpcErrorCode.MethodNotFound, response.Error!.Code);
            Assert.DoesNotContain(progId, response.Error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("An unknown action is answered with MethodNotFound and a fixed message, in and outside debug mode")]
        public async Task UnknownAction_ReturnsMethodNotFound(bool debugMode)
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = debugMode;
                var response = await SendAsync($"{SysProgIds.System}.DefinitelyNotAMethod");

                Assert.NotNull(response.Error);
                Assert.Equal((int)JsonRpcErrorCode.MethodNotFound, response.Error!.Code);
                Assert.DoesNotContain("DefinitelyNotAMethod", response.Error.Message, StringComparison.Ordinal);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("A progId with no FormSchema file answers without the server's path, even in debug mode where the message passes through")]
        public async Task ProgIdWithoutFormSchema_DebugMode_MessageCarriesNoPath()
        {
            string progId = "NoSuchForm" + Guid.NewGuid().ToString("N")[..8];
            var request = new TestRpcRequest
            {
                Method = progId + ".GetList",
                Params = new TestPayload { Value = new Polhem.Api.Core.Messages.Form.GetListRequest() },
                Id = "1"
            };
            string definePath = _fx.GetRequiredService<PathOptions>().DefinePath;

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var response = await NewDispatcher(TestSessionFactory.CreateAccessToken(_fx)).ExecuteAsync(request);

                Assert.NotNull(response.Error);
                Assert.Equal($"FormSchema '{progId}' not found.", response.Error!.Message);
                Assert.DoesNotContain(definePath, response.Error.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(Path.DirectorySeparatorChar + "FormSchema" + Path.DirectorySeparatorChar,
                    response.Error.Message, StringComparison.Ordinal);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("The answer echoes the id and carries no method member")]
        public async Task Response_EchoesIdWithoutMethod()
        {
            var id = Guid.NewGuid().ToString();
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new TestPayload { Value = new PingRequest { ClientName = "C", TraceId = "T" } },
                Id = id
            };

            var response = await NewDispatcher(Guid.Empty).ExecuteAsync(request);

            Assert.Null(response.Method);
            Assert.Equal(id, response.Id);
        }
    }
}
