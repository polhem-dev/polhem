using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Definition;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Routing tests for the methods of <see cref="LogApiConnector"/>.
    /// </summary>
    /// <remarks>
    /// These methods are thin wrappers, and the real risk is **a copy-paste routing error**: several share
    /// <c>LogListResponse</c> and several share <c>LogAggregateResponse</c>, so writing <c>GetDbAnomalyLog</c> as
    /// <c>GetApiAnomalyLog</c> returns data that looks perfectly reasonable, and neither the type system nor the
    /// round-trip tests notice. These tests pin down the <c>Method</c> sent (<c>progId.action</c>) for each one,
    /// and that the request object is passed on unchanged.
    /// </remarks>
    public class LogApiConnectorRoutingTests
    {
        private sealed class CapturingProvider : IJsonRpcProvider
        {
            public JsonRpcRequest? LastRequest { get; private set; }
            public object? ResultValue { get; set; }

            public Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request)
            {
                LastRequest = request;
                var result = new JsonRpcResult { Value = ResultValue };

                // The connector asks for Encrypted by default but degrades to Encoded when not logged in (no
                // transport key). Replying in the same format lets the whole restore path run.
                ApiPayloadConverter.TransformTo(result, PayloadFormat.Encoded);
                return Task.FromResult(new JsonRpcResponse(request) { Result = result });
            }
        }

        private static (LogApiConnector Connector, CapturingProvider Provider) Create(object resultValue)
        {
            var connector = new LogApiConnector(Guid.NewGuid());
            var provider = new CapturingProvider { ResultValue = resultValue };
            typeof(ApiConnector)
                .GetProperty(nameof(ApiConnector.Provider), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(connector, provider);
            return (connector, provider);
        }

        /// <summary>
        /// One row per method: the method name and the expected action.
        /// </summary>
        public static TheoryData<string, string> RoutedActions => new()
        {
            { nameof(LogApiConnector.GetChangeLogAsync),          LogActions.GetChangeLog },
            { nameof(LogApiConnector.GetChangeDetailAsync),       LogActions.GetChangeDetail },
            { nameof(LogApiConnector.GetLoginLogAsync),           LogActions.GetLoginLog },
            { nameof(LogApiConnector.GetAccessLogAsync),          LogActions.GetAccessLog },
            { nameof(LogApiConnector.GetApiAnomalyLogAsync),      LogActions.GetApiAnomalyLog },
            { nameof(LogApiConnector.GetDbAnomalyLogAsync),       LogActions.GetDbAnomalyLog },
            { nameof(LogApiConnector.GetApiAnomalySummaryAsync),  LogActions.GetApiAnomalySummary },
            { nameof(LogApiConnector.GetDbAnomalySummaryAsync),   LogActions.GetDbAnomalySummary },
            { nameof(LogApiConnector.GetTopApiMethodsAsync),      LogActions.GetTopApiMethods },
        };

        [Theory]
        [MemberData(nameof(RoutedActions))]
        [DisplayName("Each method sends its own action with the AuditLog progId")]
        public async Task Method_RoutesToItsOwnAction(string methodName, string expectedAction)
        {
            var (provider, expectedRequestType) = await InvokeAsync(methodName);

            Assert.NotNull(provider.LastRequest);
            Assert.Equal($"{SysProgIds.AuditLog}.{expectedAction}", provider.LastRequest!.Method);

            // Also pin down the request type sent. Comparing only the action string would still pass a
            // copy-paste error with the right action but the wrong request type (several methods share one
            // response type, so the difference would not show).
            Assert.StartsWith(expectedRequestType.FullName!, provider.LastRequest.Params.TypeName, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The routed actions are all distinct (a duplicate is a direct sign of a copy-paste routing error)")]
        public void RoutedActions_AreAllDistinct()
        {
            var actions = RoutedActions.Select(row => (string)row[1]).ToList();

            Assert.Equal(actions.Count, actions.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        [DisplayName("GetChangeDetailAsync wraps sysRowId into the matching request field")]
        public async Task GetChangeDetailAsync_WrapsSysRowId()
        {
            var (connector, provider) = Create(new GetChangeDetailResponse());
            var sysRowId = Guid.NewGuid();

            await connector.GetChangeDetailAsync(sysRowId);

            // The payload was converted to Encoded before sending (`Value` is serialized into bytes), so restore
            // it before checking. This also shows the request really went onto the wire intact, not just its
            // type name.
            var payload = provider.LastRequest!.Params;
            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);
            var sent = Assert.IsType<GetChangeDetailRequest>(payload.Value);
            Assert.Equal(sysRowId, sent.SysRowId);
        }

        /// <summary>
        /// Invokes the method by name and returns its provider. The response value is a new instance of the
        /// method's declared return type.
        /// </summary>
        private static async Task<(CapturingProvider Provider, Type RequestType)> InvokeAsync(string methodName)
        {
            var method = typeof(LogApiConnector).GetMethod(methodName)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            var responseType = method.ReturnType.GetGenericArguments()[0];
            var (connector, provider) = Create(Activator.CreateInstance(responseType)!);

            var parameter = method.GetParameters()[0];
            var isGuid = parameter.ParameterType == typeof(Guid);
            var argument = isGuid
                ? (object)Guid.NewGuid()
                : Activator.CreateInstance(parameter.ParameterType)!;

            await (Task)method.Invoke(connector, [argument])!;

            // The overload that takes a `Guid` builds the request itself, so the expected type is derived from the
            // method name.
            var requestType = isGuid ? typeof(GetChangeDetailRequest) : parameter.ParameterType;
            return (provider, requestType);
        }
    }
}
