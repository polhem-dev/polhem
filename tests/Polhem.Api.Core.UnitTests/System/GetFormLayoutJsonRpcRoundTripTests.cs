using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// An end-to-end round-trip through <see cref="JsonRpcExecutor"/>: <c>System.GetFormLayout</c> is dispatched by
    /// the executor to <see cref="Polhem.Business.System.SystemBusinessObject.GetFormLayout"/>, verifying that:
    /// <list type="bullet">
    /// <item>action routing (the reflection lookup of progId.action) finds the method</item>
    /// <item>ApiInputConverter (GetFormLayoutRequest → GetFormLayoutArgs) keeps ProgId / LayoutId</item>
    /// <item>ApiOutputConverter (GetFormLayoutResult → GetFormLayoutResponse) naming-convention reflection works,
    ///   and the FormLayout object is deep-copied correctly</item>
    /// <item>with an empty LayoutId the server uses ProgId as the layoutId (layout definition files are named {ProgId}.FormLayout.xml)</item>
    /// </list>
    /// </summary>
    public class GetFormLayoutJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetFormLayoutJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        private JsonRpcExecutor NewExecutor(Guid accessToken)
        {
            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            return new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };
        }

        [Fact]
        [DisplayName("System.GetFormLayout dispatches through JsonRpcExecutor and returns the default layout")]
        public async Task GetFormLayout_ThroughJsonRpc_DispatchesAndReturnsLayout()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var executor = NewExecutor(accessToken);

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetFormLayout}",
                Params = new JsonRpcParams
                {
                    Value = new GetFormLayoutRequest { ProgId = "Employee", LayoutId = "" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetFormLayoutResponse>(response.Result!.Value);
            // An empty LayoutId resolves to the ProgId, and tests/Define has Employee.FormLayout.xml.
            Assert.False(string.IsNullOrEmpty(result.Xml));
            var layout = XmlCodec.Deserialize<FormLayout>(result.Xml!);
            Assert.NotNull(layout);
            Assert.Equal("Employee", layout!.ProgId);
            Assert.Equal("Employee", layout.LayoutId);
            Assert.NotNull(layout.Sections);
            Assert.True(layout.Sections!.Count > 0, "The layout should have at least one section.");
        }

        [Fact]
        [DisplayName("System.GetFormLayout returns an RpcError for an empty ProgId")]
        public async Task GetFormLayout_EmptyProgId_ReturnsRpcError()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var executor = NewExecutor(accessToken);

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetFormLayout}",
                Params = new JsonRpcParams
                {
                    Value = new GetFormLayoutRequest { ProgId = "", LayoutId = "default" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("ProgId is required", response.Error!.Message);
        }
    }
}
