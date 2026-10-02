using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.UnitTests.Form;
using Polhem.Business;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.JsonRpc.Server;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Calls made with a signed-in session give the same answer through the JSON-RPC executor and through the
    /// dispatcher: reading definitions, and a form list whose result is a <see cref="DataTable"/>, in plain and in
    /// MessagePack-encoded form. Each request is serialized to JSON first, as a client sends it.
    /// </summary>
    public class DispatcherSessionParityTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public DispatcherSessionParityTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private async Task<(string Old, string New)> RunBothAsync(JsonRpcRequest request, IServiceProvider services)
        {
            var accessToken = TestSessionFactory.CreateAccessToken(_fx);
            var boFactory = new BusinessObjectFactory(
                services,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());
            var json = request.ToJson();

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = accessToken,
                IsLocalCall = true,
            };
            var oldJson = (await executor.ExecuteAsync(JsonCodec.Deserialize<JsonRpcRequest>(json)!)).ToJson();

            var dispatcherServices = new TestOverrideServiceProvider(services, (typeof(IBusinessObjectFactory), boFactory));
            var transport = new JsonRpcTransportInfo(
                JsonRpcTransportKind.InProcess,
                dispatcherServices,
                items: new Dictionary<string, object?> { [PolhemJsonRpc.AccessTokenItem] = accessToken });
            var result = await new JsonRpcDispatcher(PolhemJsonRpc.CreateServerOptions())
                .DispatchMessageAsync(Encoding.UTF8.GetBytes(json), transport);
            var newJson = Encoding.UTF8.GetString(result.Serialize()!);

            Assert.Contains("\"result\"", newJson, StringComparison.Ordinal);
            return (oldJson, newJson);
        }

        private static JsonRpcRequest Request(string method, object value, PayloadFormat format = PayloadFormat.Plain, string codec = "")
        {
            var payload = new JsonRpcParams { Codec = codec, Value = value };
            ApiPayloadConverter.TransformTo(payload, format);
            return new JsonRpcRequest { Method = method, Params = payload, Id = "session-parity" };
        }

        [Fact]
        [DisplayName("System.GetFormSchema gets the same answer from the executor and the dispatcher")]
        public async Task GetFormSchema_SameAnswer()
        {
            var (oldJson, newJson) = await RunBothAsync(
                Request($"{SysProgIds.System}.{SystemActions.GetFormSchema}", new GetFormSchemaRequest { ProgId = "Employee" }),
                _fx.Provider);

            ParityAssert.SameJson(oldJson, newJson);
        }

        [Fact]
        [DisplayName("System.GetFormLayout gets the same answer from the executor and the dispatcher")]
        public async Task GetFormLayout_SameAnswer()
        {
            var (oldJson, newJson) = await RunBothAsync(
                Request($"{SysProgIds.System}.{SystemActions.GetFormLayout}", new GetFormLayoutRequest { ProgId = "Employee", LayoutId = "" }),
                _fx.Provider);

            ParityAssert.SameJson(oldJson, newJson);
        }

        [Theory]
        [DisplayName("Employee.GetList, whose result holds a DataTable, gets the same answer in plain and encoded form")]
        [InlineData(PayloadFormat.Plain, "")]
        [InlineData(PayloadFormat.Encoded, PayloadCodecNames.MessagePack)]
        [InlineData(PayloadFormat.Encoded, PayloadCodecNames.Json)]
        public async Task GetList_SameAnswer(PayloadFormat format, string codec)
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add("E001", "員工甲");
            var stubFactory = new GetListJsonRpcRoundTripTests.StubFormRepositoryFactory(
                new GetListJsonRpcRoundTripTests.StubDataFormRepository(table));
            var services = new TestOverrideServiceProvider(_fx.Provider, (typeof(IRepositoryFactory), stubFactory));

            var (oldJson, newJson) = await RunBothAsync(
                Request($"Employee.{FormActions.GetList}", new GetListRequest { SelectFields = "sys_id,sys_name" }, format, codec),
                services);

            ParityAssert.SameJson(oldJson, newJson);
            using var answer = JsonDocument.Parse(newJson);
            Assert.Equal((int)format, answer.RootElement.GetProperty("result").GetProperty("format").GetInt32());
        }
    }
}
