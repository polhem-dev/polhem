using System.Text.Json;
using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.Messages.Form;
using Polhem.JsonRpc;
using Polhem.Definition;
using Polhem.Tests.Shared;
using JsonRpcRequest = Polhem.JsonRpc.JsonRpcRequest;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Cancellation through the client surface: a token handed to a connector, a provider or the client
    /// definition cache must reach the transport and stop the call.
    /// </summary>
    public class ClientCancellationTests
    {
        /// <summary>
        /// An HTTP handler that never answers on its own, so the only way out of a call is cancellation.
        /// </summary>
        private sealed class HangingHandler : HttpMessageHandler
        {
            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool SawCancellation { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Entered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    SawCancellation = true;
                    throw;
                }
                throw new InvalidOperationException("A delay without a timeout only ends by cancellation.");
            }
        }

        /// <summary>
        /// Records the token each call receives and answers with an empty result.
        /// </summary>
        private static FakeApiTransport NewCapturingTransport()
            => new(call => FakeApiTransport.Answer(call, new GetListResponse()));

        /// <summary>
        /// A definition fetch that stays pending until released, and ignores cancellation on purpose: the
        /// fetch is shared, so no single caller's token may stop it.
        /// </summary>
        private sealed class GatedConnector : SystemApiConnector
        {
            private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public GatedConnector() : base(EmptyServiceProvider.Instance, Guid.NewGuid()) { }

            public int GetDefineCallCount { get; private set; }

            public void Release() => _gate.TrySetResult();

            public override async Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null, CancellationToken cancellationToken = default)
            {
                GetDefineCallCount++;
                await _gate.Task.ConfigureAwait(false);
                return Activator.CreateInstance<T>();
            }
        }

        private static JsonRpcRequest NewRequest() => new(
            $"{SysProgIds.System}.{SystemActions.Ping}",
            JsonSerializer.Deserialize<JsonElement>("{}"),
            JsonRpcId.FromString(Guid.NewGuid().ToString()));

        private static FormApiConnector NewFormConnector(FakeApiTransport transport)
        {
            var connector = new FormApiConnector(EmptyServiceProvider.Instance, Guid.NewGuid(), "Employee", new ApiSessionContext());
            typeof(ApiConnector)
                .GetProperty(nameof(ApiConnector.Provider), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(connector, transport);
            return connector;
        }

        [Fact]
        [DisplayName("RemoteApiProvider aborts the HTTP call in flight when its token is cancelled")]
        public async Task RemoteApiProvider_CancelledDuringHttpCall_AbortsRequest()
        {
            var handler = new HangingHandler();
            var provider = new RemoteApiProvider("http://example.invalid/api", Guid.Empty, handler);
            using var cts = new CancellationTokenSource();

            var call = provider.SendAsync(NewRequest(), cts.Token);
            await handler.Entered.Task;
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
            // The handler saw the cancellation itself, so the token reached the socket layer rather than only
            // abandoning the wait on the caller's side.
            Assert.True(handler.SawCancellation);
        }

        [Fact]
        [DisplayName("LocalApiProvider with a cancelled token throws before resolving the dispatcher")]
        public async Task LocalApiProvider_CancelledToken_ThrowsBeforeDispatch()
        {
            // The empty provider has no dispatcher, so getting past the cancellation check would throw
            // InvalidOperationException instead.
            var provider = new LocalApiProvider(EmptyServiceProvider.Instance, Guid.Empty);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SendAsync(NewRequest(), cts.Token));
        }

        [Fact]
        [DisplayName("LocalApiProvider with a cancelled token does not run the call on the in-process backend")]
        public async Task LocalApiProvider_CancelledToken_DoesNotRunCall()
        {
            var provider = new LocalApiProvider(TestProcessBootstrap.LocalServices, Guid.Empty);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SendAsync(NewRequest(), cts.Token));
        }

        [Fact]
        [DisplayName("A connector method passes its cancellation token down to the provider")]
        public async Task ConnectorMethod_Token_ReachesProvider()
        {
            var transport = NewCapturingTransport();
            var connector = NewFormConnector(transport);
            using var cts = new CancellationTokenSource();

            await connector.GetListAsync(cancellationToken: cts.Token);

            Assert.Equal(cts.Token, transport.LastToken);
        }

        [Fact]
        [DisplayName("A connector method with a cancelled token throws without calling the provider")]
        public async Task ConnectorMethod_CancelledToken_DoesNotCallProvider()
        {
            var transport = NewCapturingTransport();
            var connector = NewFormConnector(transport);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connector.GetListAsync(cancellationToken: cts.Token));
            Assert.Empty(transport.Calls);
        }

        [Fact]
        [DisplayName("PingAsync lets a cancellation through unwrapped")]
        public async Task PingAsync_CancelledToken_ThrowsOperationCanceled()
        {
            var connector = new SystemApiConnector(EmptyServiceProvider.Instance, Guid.Empty, new ApiSessionContext());
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Every other failure is wrapped in InvalidOperationException; a cancellation the caller asked for is not.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connector.PingAsync(cts.Token));
        }

        [Fact]
        [DisplayName("ClientDefineAccess: cancelling one caller's wait leaves the shared fetch running for the others")]
        public async Task ClientDefineAccess_CancelledWaiter_DoesNotFailSharedFetch()
        {
            var connector = new GatedConnector();
            var access = new ClientDefineAccess(connector);
            using var cts = new CancellationTokenSource();

            var cancelled = access.GetFormSchemaAsync("Employee", cts.Token);
            var patient = access.GetFormSchemaAsync("Employee");
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            connector.Release();
            var schema = await patient;

            Assert.NotNull(schema);
            // One fetch served both callers, and its result was cached rather than evicted by the cancellation.
            var again = await access.GetFormSchemaAsync("Employee");
            Assert.Same(schema, again);
            Assert.Equal(1, connector.GetDefineCallCount);
        }
    }
}
