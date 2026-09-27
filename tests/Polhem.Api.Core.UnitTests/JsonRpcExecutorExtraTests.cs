using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Additional JsonRpcExecutor tests covering the error paths (ParseMethod exceptions, an empty progId, an unknown
    /// action), the ExecuteAsync path and property assignment.
    /// </summary>
    // These tests assert masked messages, and masking depends on the process-wide static `SysInfo.IsDebugMode`.
    // So they are serialized with the test classes that toggle that flag (see `SysInfoStaticCollection`).
    [Collection(SysInfoStaticCollection.Name)]
    public class JsonRpcExecutorExtraTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public JsonRpcExecutorExtraTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private JsonRpcExecutor NewExecutor(Guid accessToken, bool isLocalCall = false)
        {
            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = isLocalCall,
            };
            return executor;
        }

        [Fact]
        [DisplayName("The property setters assign AccessToken and IsLocalCall")]
        public void Properties_AssignableAfterConstruction()
        {
            var token = Guid.NewGuid();
            var executor = NewExecutor(token, isLocalCall: true);

            Assert.Equal(token, executor.AccessToken);
            Assert.True(executor.IsLocalCall);
        }

        [Fact]
        [DisplayName("IsLocalCall defaults to false")]
        public void IsLocalCall_DefaultsToFalse()
        {
            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>());
            Assert.False(executor.IsLocalCall);
        }

        [Fact]
        [DisplayName("ExecuteAsync completes the Ping method")]
        public async Task ExecuteAsync_Ping_Succeeds()
        {
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams { Value = new PingRequest { ClientName = "T", TraceId = "X" } },
                Id = Guid.NewGuid().ToString()
            };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            Assert.IsType<PingResponse>(response.Result!.Value);
        }

        [Fact]
        [DisplayName("Execute returns the FormatException user message when Method has no '.'")]
        public async Task Execute_MethodMissingDot_ReturnsFormatExceptionMessage()
        {
            var request = new JsonRpcRequest
            {
                Method = "InvalidMethodFormat",
                Params = new JsonRpcParams(),
                Id = "1"
            };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal((int)JsonRpcErrorCode.UserMessage, response.Error!.Code);
            Assert.Contains("Invalid method format", response.Error.Message);
        }

        [Fact]
        [DisplayName("Execute returns the FormatException user message when Method is an empty string")]
        public async Task Execute_EmptyMethod_ReturnsFormatExceptionMessage()
        {
            var request = new JsonRpcRequest
            {
                Method = string.Empty,
                Params = new JsonRpcParams(),
                Id = "1"
            };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("Invalid method format", response.Error!.Message);
        }

        [Theory]
        [InlineData("<script>.GetList")]
        [InlineData("Order.Get List")]
        [InlineData("Order.Save.Extra")]
        [InlineData("Order.")]
        [DisplayName("Execute rejects a method whose progId or action has characters outside the allowed set")]
        public async Task Execute_MethodWithInvalidCharacters_ReturnsFormatExceptionMessage(string method)
        {
            var request = new JsonRpcRequest { Method = method, Params = new JsonRpcParams(), Id = "1" };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal("Invalid method format.", response.Error!.Message);
        }

        [Fact]
        [DisplayName("Execute rejects a progId longer than the limit without echoing it back")]
        public async Task Execute_OverlongProgId_ReturnsFormatExceptionWithoutEcho()
        {
            string progId = new('A', JsonRpcExecutor.MaxMethodPartLength + 1);
            var request = new JsonRpcRequest { Method = progId + ".GetList", Params = new JsonRpcParams(), Id = "1" };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal("Invalid method format.", response.Error!.Message);
        }

        [Fact]
        [DisplayName("Execute accepts a progId exactly at the length limit")]
        public async Task Execute_ProgIdAtLimit_PassesShapeCheck()
        {
            string progId = new('A', JsonRpcExecutor.MaxMethodPartLength);
            var request = new JsonRpcRequest { Method = progId + ".NoSuchAction", Params = new JsonRpcParams(), Id = "1" };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.NotEqual("Invalid method format.", response.Error!.Message);
        }

        [Fact]
        [DisplayName("Execute returns the ArgumentException user message when progId is an empty string")]
        public async Task Execute_EmptyProgId_ReturnsArgumentExceptionMessage()
        {
            // `ParseMethod` splits ".Ping" into an empty progId and the action "Ping".
            var request = new JsonRpcRequest
            {
                Method = ".Ping",
                Params = new JsonRpcParams(),
                Id = "1"
            };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("ProgId", response.Error!.Message);
        }

        [Fact]
        [DisplayName("Execute masks MissingMethodException for an unknown Action as Internal server error (outside debug mode)")]
        public async Task Execute_UnknownAction_ReturnsGenericInternalError()
        {
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.DefinitelyNotAMethod",
                Params = new JsonRpcParams(),
                Id = "1"
            };

            // The test fixture itself runs in debug mode (the SystemSettings in tests/Define), and masking only
            // applies outside debug mode. What is verified here is the production behavior, so debug mode is turned
            // off explicitly rather than taken from the environment.
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

                Assert.NotNull(response.Error);
                Assert.Equal((int)JsonRpcErrorCode.InternalError, response.Error!.Code);
                Assert.Equal("Internal server error", response.Error.Message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("Execute passes through the original exception message for an unknown Action in debug mode")]
        public async Task Execute_UnknownAction_DebugMode_PassesThroughMessage()
        {
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.DefinitelyNotAMethod",
                Params = new JsonRpcParams(),
                Id = "1"
            };

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

                Assert.NotNull(response.Error);
                Assert.Equal((int)JsonRpcErrorCode.InternalError, response.Error!.Code);
                Assert.Contains("DefinitelyNotAMethod", response.Error.Message, StringComparison.Ordinal);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("Execute takes the CreateBusinessObject branch for a non-System progId")]
        public async Task Execute_NonSystemProgId_InvokesCreateBusinessObject()
        {
            // Uses the defined Department progId with an unknown action. In debug mode the `MissingMethodException`
            // message passes through and names the business object type the action was looked up on, which shows
            // the form branch of `CreateBusinessObject` built it rather than the system one.
            var request = new JsonRpcRequest
            {
                Method = "Department.DefinitelyNotAMethod",
                Params = new JsonRpcParams(),
                Id = "1"
            };

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

                Assert.NotNull(response.Error);
                Assert.Contains("business object 'FormBusinessObject'", response.Error!.Message, StringComparison.Ordinal);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("The Response returned by Execute echoes Method and Id")]
        public async Task Execute_Response_EchoesMethodAndId()
        {
            var id = Guid.NewGuid().ToString();
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams { Value = new PingRequest { ClientName = "C", TraceId = "T" } },
                Id = id
            };

            var response = await NewExecutor(Guid.Empty, isLocalCall: true).ExecuteAsync(request);

            Assert.Equal(request.Method, response.Method);
            Assert.Equal(id, response.Id);
        }
    }
}
