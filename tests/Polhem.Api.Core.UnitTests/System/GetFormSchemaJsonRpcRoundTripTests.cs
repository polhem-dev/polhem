using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
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
    /// 走 <see cref="JsonRpcExecutor"/> 的 end-to-end round-trip：將
    /// <c>System.GetFormSchema</c> 透過 executor 派發到
    /// <see cref="Polhem.Business.System.SystemBusinessObject.GetFormSchema"/>，驗證：
    /// <list type="bullet">
    /// <item>action 路由（progId.action 反射查表）正確找到方法</item>
    /// <item>ApiInputConverter（GetFormSchemaRequest → GetFormSchemaArgs）保留 ProgId</item>
    /// <item>ApiOutputConverter（GetFormSchemaResult → GetFormSchemaResponse）命名慣例反射有作用，
    ///   FormSchema 物件 deep-copy 正確</item>
    /// <item>從 IDefineAccess 取出 fixture seed 的 Employee schema</item>
    /// </list>
    /// </summary>
    public class GetFormSchemaJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetFormSchemaJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("System.GetFormSchema 經 JsonRpcExecutor 應派發成功並回傳 fixture seed 的 Employee schema")]
        public void GetFormSchema_ThroughJsonRpc_DispatchesAndReturnsSchema()
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
                Method = $"{SysProgIds.System}.{SystemActions.GetFormSchema}",
                Params = new JsonRpcParams
                {
                    Value = new GetFormSchemaRequest { ProgId = "Employee" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = executor.Execute(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetFormSchemaResponse>(response.Result!.Value);
            // 定義一律以 XML 上線——巢狀集合是 get-only，JSON / MessagePack 收不回來
            Assert.False(string.IsNullOrEmpty(result.Xml));
            var schema = XmlCodec.Deserialize<FormSchema>(result.Xml!);
            Assert.NotNull(schema);
            Assert.Equal("Employee", schema!.ProgId);
            Assert.NotNull(schema.Tables);
            Assert.True(schema.Tables!.Count > 0, "Schema 應至少有一個 master table");
        }

        [Fact]
        [DisplayName("System.GetFormSchema 對空 ProgId 應回 RpcError")]
        public void GetFormSchema_EmptyProgId_ReturnsRpcError()
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
                Method = $"{SysProgIds.System}.{SystemActions.GetFormSchema}",
                Params = new JsonRpcParams
                {
                    Value = new GetFormSchemaRequest { ProgId = "" },
                },
                Id = Guid.NewGuid().ToString(),
            };

            var response = executor.Execute(request);

            Assert.NotNull(response.Error);
            Assert.Contains("ProgId is required", response.Error!.Message);
        }
    }
}
