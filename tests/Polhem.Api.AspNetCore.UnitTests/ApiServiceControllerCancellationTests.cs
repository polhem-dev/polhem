using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Polhem.Api.Core.JsonRpc;
using Polhem.Definition;
using Polhem.Tests.Shared;

namespace Polhem.Api.AspNetCore.UnitTests
{
    /// <summary>
    /// The controller hands <see cref="HttpContext.RequestAborted"/> to the executor, so a client that has gone
    /// away does not get its call dispatched.
    /// </summary>
    public class ApiServiceControllerCancellationTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public ApiServiceControllerCancellationTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private sealed class TestController : Controllers.ApiServiceController
        {
            public Task<IActionResult> HandleAsync(JsonRpcRequest request) => HandleRequestAsync(Guid.Empty, request);
        }

        [Fact]
        [DisplayName("HandleRequestAsync answers 499 without running the call when the client has already disconnected")]
        public async Task HandleRequestAsync_RequestAborted_Returns499()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var context = new DefaultHttpContext
            {
                RequestServices = _fx.Provider,
                RequestAborted = cts.Token,
            };
            var controller = new TestController { ControllerContext = new ControllerContext { HttpContext = context } };
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.Ping}",
                Params = new JsonRpcParams(),
                Id = Guid.NewGuid().ToString(),
            };

            var result = await controller.HandleAsync(request);

            // Not a 500 error envelope: nobody is left to read one, and a cancelled call is not a server fault.
            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status499ClientClosedRequest, status.StatusCode);
        }
    }
}
