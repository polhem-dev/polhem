using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Base;
using Polhem.Base.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Filters;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// End-to-end tests of what an action name may reach, what an encoded body is decoded into, and what a failure
    /// tells the caller, through <see cref="JsonRpcExecutor"/> with a business object the test controls.
    /// </summary>
    /// <remarks>
    /// The business object carries a type-level <see cref="ApiAccessControlAttribute"/>, the shape the security rule
    /// once recommended, so every member below is covered by an access declaration and only the resolution rule
    /// stands between a caller and it.
    /// </remarks>
    [Collection("SysInfoStatic")]
    public class JsonRpcExecutorHardeningTests
    {
        private static JsonRpcExecutor NewExecutor(ExposedBusinessObject businessObject, ILogger? logger = null)
            => new(new SingleObjectFactory(businessObject), new RejectAllTokens(), new NoKeys())
            {
                AccessToken = Guid.Empty,
                IsLocalCall = false,
                Logger = logger,
            };

        private static JsonRpcRequest Request(string action, object value, PayloadFormat format = PayloadFormat.Plain)
        {
            var parameters = new JsonRpcParams { Value = value };
            if (format != PayloadFormat.Plain)
                ApiPayloadConverter.TransformTo(parameters, format);
            return new JsonRpcRequest { Method = $"Exposed.{action}", Params = parameters, Id = "1" };
        }

        [Fact]
        [DisplayName("An ordinary action on the test business object succeeds, so the refusals below are about resolution")]
        public async Task Execute_OrdinaryAction_Succeeds()
        {
            var bo = new ExposedBusinessObject();

            var response = await NewExecutor(bo).ExecuteAsync(Request(nameof(ExposedBusinessObject.Echo), "hello"));

            Assert.Null(response.Error);
            Assert.Equal("hello", bo.LastCall);
        }

        [Theory]
        [InlineData("set_Label")]
        [InlineData("get_Label")]
        [InlineData(nameof(ExposedBusinessObject.StaticAction))]
        [InlineData(nameof(ExposedBusinessObject.GenericAction))]
        [InlineData(nameof(ExposedBusinessObject.Equals))]
        [InlineData(nameof(ExposedBusinessObject.NoArguments))]
        [DisplayName("An action name does not resolve to accessors, static or generic methods, object overrides, or other signatures")]
        public async Task Execute_UnresolvableMember_IsRefusedWithoutInvoking(string action)
        {
            var bo = new ExposedBusinessObject();

            var response = await NewExecutor(bo).ExecuteAsync(Request(action, "payload"));

            Assert.NotNull(response.Error);
            Assert.Equal((int)JsonRpcErrorCode.InternalError, response.Error!.Code);
            Assert.Equal("original", bo.Label);
            Assert.Equal(string.Empty, bo.LastCall);
            Assert.Equal(0, ExposedBusinessObject.StaticCalls);
        }

        [Fact]
        [DisplayName("An encoded body is decoded into the parameter type the method declares")]
        public async Task Execute_EncodedMatchingBody_IsDecodedAndInvoked()
        {
            var bo = new ExposedBusinessObject();

            var response = await NewExecutor(bo).ExecuteAsync(
                Request(nameof(ExposedBusinessObject.Accept), new PingRequest { TraceId = "t-1" }, PayloadFormat.Encoded));

            Assert.Null(response.Error);
            Assert.Equal("t-1", bo.LastCall);
        }

        [Fact]
        [DisplayName("An encoded body declaring another type is refused and the method is not invoked")]
        public async Task Execute_EncodedBodyOfAnotherType_IsRefused()
        {
            var bo = new ExposedBusinessObject();

            var response = await NewExecutor(bo).ExecuteAsync(
                Request(nameof(ExposedBusinessObject.Accept), new FilterGroup(LogicalOperator.And), PayloadFormat.Encoded));

            Assert.NotNull(response.Error);
            Assert.Equal(string.Empty, bo.LastCall);
        }

        [Fact]
        [DisplayName("A BCL exception reaches the caller as a fixed message, and its real message is logged")]
        public async Task Execute_BclException_ReturnsFixedMessageAndLogsRealOne()
        {
            var logger = new ListLogger();
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;

                var response = await NewExecutor(new ExposedBusinessObject(), logger)
                    .ExecuteAsync(Request(nameof(ExposedBusinessObject.FailInternally), "x"));

                Assert.Equal((int)JsonRpcErrorCode.UserMessage, response.Error!.Code);
                Assert.Equal("The request could not be completed.", response.Error.Message);
                Assert.DoesNotContain("ft_secret", response.Error.Message, StringComparison.Ordinal);

                var entry = Assert.Single(logger.Entries);
                Assert.Equal(LogLevel.Warning, entry.Level);
                Assert.Contains("ft_secret", entry.Exception!.Message, StringComparison.Ordinal);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("A UserMessageException reaches the caller verbatim and is not logged as masked")]
        public async Task Execute_UserMessageException_ReturnsItsMessage()
        {
            var logger = new ListLogger();
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;

                var response = await NewExecutor(new ExposedBusinessObject(), logger)
                    .ExecuteAsync(Request(nameof(ExposedBusinessObject.FailForUser), "x"));

                Assert.Equal((int)JsonRpcErrorCode.UserMessage, response.Error!.Code);
                Assert.Equal("Shown to the user.", response.Error.Message);
                Assert.Empty(logger.Entries);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("A call to an authenticated method without a valid token answers with the Unauthorized code (-32001)")]
        public async Task Execute_AuthenticatedMethodWithoutToken_ReturnsUnauthorizedCode()
        {
            var bo = new ExposedBusinessObject();

            var response = await NewExecutor(bo).ExecuteAsync(Request(nameof(ExposedBusinessObject.SignedInOnly), "x"));

            Assert.Equal((int)JsonRpcErrorCode.Unauthorized, response.Error!.Code);
            Assert.Equal("AccessToken is required or invalid.", response.Error.Message);
            Assert.Equal(string.Empty, bo.LastCall);
        }

        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
        public sealed class ExposedBusinessObject
        {
            public static int StaticCalls { get; private set; }

            public string Label { get; set; } = "original";

            public string LastCall { get; private set; } = string.Empty;

            public string Echo(string value) => LastCall = value;

            public string Accept(PingRequest request) => LastCall = request.TraceId ?? string.Empty;

            public static void StaticAction(string value) => StaticCalls += value.Length;

            public string GenericAction<T>(T value) => LastCall = value?.ToString() ?? string.Empty;

            public string NoArguments() => LastCall = "called";

            [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
            public string SignedInOnly(string value) => LastCall = value;

            public string FailInternally(string value)
            {
                LastCall = value;
                throw new InvalidOperationException($"Column 'x' of table 'ft_secret' is missing ({value}).");
            }

            public string FailForUser(string value)
            {
                LastCall = value;
                throw new UserMessageException("Shown to the user.");
            }

            public override bool Equals(object? obj)
            {
                LastCall = "Equals";
                return ReferenceEquals(this, obj);
            }

            public override int GetHashCode() => 0;
        }

        private sealed class SingleObjectFactory(object businessObject) : IBusinessObjectFactory
        {
            public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall) => businessObject;
        }

        private sealed class RejectAllTokens : IAccessTokenValidator
        {
            public bool Validate(Guid accessToken) => false;
        }

        private sealed class NoKeys : IApiEncryptionKeyProvider
        {
            public byte[] GetKey(Guid accessToken) => [];

            public byte[] GenerateKeyForLogin(Guid accessToken) => [];

            public bool SupportsSessionRebuild => false;
        }

        private sealed class ListLogger : ILogger
        {
            public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, exception));
        }
    }
}
