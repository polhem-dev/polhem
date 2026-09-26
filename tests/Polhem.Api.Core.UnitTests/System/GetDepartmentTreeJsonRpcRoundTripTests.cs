using System.ComponentModel;
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
    /// An end-to-end round-trip through <see cref="JsonRpcExecutor"/>: <c>System.GetDepartmentTree</c> is dispatched by
    /// the executor to <see cref="Polhem.Business.System.SystemBusinessObject.GetDepartmentTree"/>, verifying action
    /// routing, ApiInputConverter (Request→Args) and ApiOutputConverter (Result→Response).
    /// Without EnterCompany it returns a null tree (no DB access, so the test focuses on the dispatch path).
    /// </summary>
    public class GetDepartmentTreeJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;
        public GetDepartmentTreeJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("System.GetDepartmentTree dispatches through JsonRpcExecutor and returns a null tree before a company is entered")]
        public void GetDepartmentTree_ThroughJsonRpc_NoCompany_ReturnsNullTree()
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);

            var boFactory = new BusinessObjectFactory(
                _fx.Provider,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.GetDepartmentTree}",
                Params = new JsonRpcParams { Value = new GetDepartmentTreeRequest() },
                Id = Guid.NewGuid().ToString(),
            };

            var response = executor.Execute(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetDepartmentTreeResponse>(response.Result!.Value);
            Assert.Null(result.Tree); // Without EnterCompany the CompanyId is empty, so no service is queried and the tree is null.
        }
    }
}
