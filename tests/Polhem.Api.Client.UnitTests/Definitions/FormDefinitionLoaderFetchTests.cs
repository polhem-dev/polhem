using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.JsonRpc;

namespace Polhem.Api.Client.UnitTests.Definitions
{
    /// <summary>
    /// How <see cref="FormDefinitionLoader"/> fetches a form's language resources: at once over a remote client, where
    /// each fetch is a round trip, and one after another in process, where the calls share the caller's services.
    /// </summary>
    public class FormDefinitionLoaderFetchTests
    {
        private const string ProgId = "Employee";

        private sealed class UnusedTransport : IJsonRpcTransport
        {
            public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("The connector answers every call itself.");

            public Task<IReadOnlyList<JsonRpcResponse>> SendBatchAsync(IReadOnlyList<JsonRpcRequest> requests,
                CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("The connector answers every call itself.");
        }

        /// <summary>Answers each language fetch after a short delay and records how many were in flight at once.</summary>
        private sealed class CountingConnector(bool isLocal) : SystemApiConnector(
            PolhemApiClient.CreateWithTransport(_ => new UnusedTransport(), isLocal, TestClients.PayloadOptions))
        {
            private int _inFlight;

            public int MaxInFlight { get; private set; }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null, CancellationToken cancellationToken = default)
            {
                if (defineType == DefineType.FormSchema)
                {
                    var schema = new FormSchema(ProgId, "Employee");
                    var table = schema.Tables!.Add(ProgId, "Employee");
                    table.Fields!.Add(new FormField("gender", "Gender", FieldDbType.String) { LangEnumName = "Common.Gender" });
                    return Task.FromResult((T)(object)schema);
                }
                return CountAsync(() => (T)(object)new LanguageResource());
            }

            public override Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns, CancellationToken cancellationToken = default)
                => CountAsync<LanguageResource?>(() => null);

            private async Task<T> CountAsync<T>(Func<T> result)
            {
                int now = Interlocked.Increment(ref _inFlight);
                lock (this) { MaxInFlight = Math.Max(MaxInFlight, now); }
                await Task.Delay(20).ConfigureAwait(false);
                Interlocked.Decrement(ref _inFlight);
                return result();
            }
        }

        [Fact]
        [DisplayName("Over a remote client, the language resources of a form are fetched at once")]
        public async Task GetLocalizedSchemaAsync_RemoteClient_FetchesConcurrently()
        {
            var connector = new CountingConnector(isLocal: false);

            await new FormDefinitionLoader(new ClientDefineAccess(connector)).GetLocalizedSchemaAsync(ProgId, "zh-TW");

            Assert.True(connector.MaxInFlight > 1, $"At most {connector.MaxInFlight} fetch was in flight.");
        }

        [Fact]
        [DisplayName("Over an in-process client, the language resources of a form are fetched one after another")]
        public async Task GetLocalizedSchemaAsync_InProcessClient_FetchesOneAtATime()
        {
            var connector = new CountingConnector(isLocal: true);

            await new FormDefinitionLoader(new ClientDefineAccess(connector)).GetLocalizedSchemaAsync(ProgId, "zh-TW");

            Assert.Equal(1, connector.MaxInFlight);
        }
    }
}
