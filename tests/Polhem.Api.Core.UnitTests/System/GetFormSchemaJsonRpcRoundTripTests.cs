using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;
using Polhem.Api.Core.UnitTests.Dispatch;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// An end-to-end round trip through the JSON-RPC dispatcher: dispatches
    /// <c>System.GetFormSchema</c> through the dispatcher to
    /// <see cref="Polhem.Business.System.SystemBusinessObject.GetFormSchema"/> and verifies that:
    /// <list type="bullet">
    /// <item>action routing (the progId.action reflection lookup) finds the method</item>
    /// <item>ApiInputConverter (GetFormSchemaRequest → GetFormSchemaArgs) keeps the ProgId</item>
    /// <item>ApiOutputConverter (GetFormSchemaResult → GetFormSchemaResponse) naming-convention reflection works,
    ///   and the FormSchema arrives intact as XML</item>
    /// <item>the Employee schema seeded by the fixture is read from IDefineAccess</item>
    /// </list>
    /// </summary>
    public class GetFormSchemaJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetFormSchemaJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("System.GetFormSchema dispatches through the JSON-RPC dispatcher and returns the Employee schema seeded by the fixture")]
        public async Task GetFormSchema_ThroughJsonRpc_DispatchesAndReturnsSchema()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new TestDispatcher(_fx.Provider, boFactory)
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetFormSchema}",
                Params = new TestPayload
                {
                    Value = new GetFormSchemaRequest { ProgId = "Employee" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetFormSchemaResponse>(response.Result!.Value);
            // Definitions travel as XML because their nested collections are get-only, which JSON and MessagePack cannot read back.
            Assert.False(string.IsNullOrEmpty(result.Xml));
            var schema = XmlCodec.Deserialize<FormSchema>(result.Xml!);
            Assert.NotNull(schema);
            Assert.Equal("Employee", schema!.ProgId);
            Assert.NotNull(schema.Tables);
            Assert.True(schema.Tables!.Count > 0, "The schema should have at least one master table.");
        }

        [Fact]
        [DisplayName("System.GetFormSchema returns an RpcError for an empty ProgId")]
        public async Task GetFormSchema_EmptyProgId_ReturnsRpcError()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new TestDispatcher(_fx.Provider, boFactory)
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetFormSchema}",
                Params = new TestPayload
                {
                    Value = new GetFormSchemaRequest { ProgId = "" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("ProgId is required", response.Error!.Message);
        }
    }
}
