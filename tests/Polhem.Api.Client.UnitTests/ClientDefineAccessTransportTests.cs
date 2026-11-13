using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.JsonRpc;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Tests for the save calls, the menu read and the customization reads of <see cref="ClientDefineAccess"/>,
    /// over a <see cref="SystemApiConnector"/> whose calls go to a <see cref="FakeApiTransport"/>.
    /// </summary>
    public class ClientDefineAccessTransportTests
    {
        private static (ClientDefineAccess Access, FakeApiTransport Transport) Create(Func<FakeApiCall, object?> answer)
        {
            var transport = new FakeApiTransport(call => FakeApiTransport.Answer(call, answer(call)));
            return (new ClientDefineAccess(new SystemApiConnector(TestClients.Fake(transport))), transport);
        }

        private static (ClientDefineAccess Access, FakeApiTransport Transport) CreateForSave()
            => Create(_ => new SaveDefineResponse());

        private static string MethodOf(string action) => $"{SysProgIds.System}.{action}";

        private static SaveDefineRequest LastSave(FakeApiTransport transport)
        {
            Assert.Equal(MethodOf(SystemActions.SaveDefine), transport.LastCall!.Method);
            return transport.LastCall!.SentValue<SaveDefineRequest>();
        }

        [Fact]
        [DisplayName("SaveSystemSettingsAsync sends SaveDefine for SystemSettings with no keys")]
        public async Task SaveSystemSettingsAsync_SendsSystemSettings()
        {
            var (access, transport) = CreateForSave();

            await access.SaveSystemSettingsAsync(new SystemSettings());

            var sent = LastSave(transport);
            Assert.Equal(DefineType.SystemSettings, sent.DefineType);
            Assert.Null(sent.Keys);
            Assert.NotNull(XmlCodec.Deserialize<SystemSettings>(sent.Xml));
        }

        [Fact]
        [DisplayName("SaveDatabaseSettingsAsync sends SaveDefine for DatabaseSettings")]
        public async Task SaveDatabaseSettingsAsync_SendsDatabaseSettings()
        {
            var (access, transport) = CreateForSave();

            await access.SaveDatabaseSettingsAsync(new DatabaseSettings());

            Assert.Equal(DefineType.DatabaseSettings, LastSave(transport).DefineType);
        }

        [Fact]
        [DisplayName("SaveProgramSettingsAsync sends SaveDefine for ProgramSettings")]
        public async Task SaveProgramSettingsAsync_SendsProgramSettings()
        {
            var (access, transport) = CreateForSave();

            await access.SaveProgramSettingsAsync(new ProgramSettings());

            Assert.Equal(DefineType.ProgramSettings, LastSave(transport).DefineType);
        }

        [Fact]
        [DisplayName("SavePermissionModelsAsync sends SaveDefine for PermissionModels")]
        public async Task SavePermissionModelsAsync_SendsPermissionModels()
        {
            var (access, transport) = CreateForSave();

            await access.SavePermissionModelsAsync(new PermissionModels());

            Assert.Equal(DefineType.PermissionModels, LastSave(transport).DefineType);
        }

        [Fact]
        [DisplayName("SaveDbCategorySettingsAsync sends SaveDefine for DbCategorySettings")]
        public async Task SaveDbCategorySettingsAsync_SendsDbCategorySettings()
        {
            var (access, transport) = CreateForSave();

            await access.SaveDbCategorySettingsAsync(new DbCategorySettings());

            Assert.Equal(DefineType.DbCategorySettings, LastSave(transport).DefineType);
        }

        [Fact]
        [DisplayName("SaveTableSchemaAsync sends SaveDefine for TableSchema keyed by the category id")]
        public async Task SaveTableSchemaAsync_SendsCategoryKey()
        {
            var (access, transport) = CreateForSave();

            await access.SaveTableSchemaAsync("company", new TableSchema { TableName = "ft_order" });

            var sent = LastSave(transport);
            Assert.Equal(DefineType.TableSchema, sent.DefineType);
            Assert.Equal(["company"], sent.Keys!);
            Assert.Equal("ft_order", XmlCodec.Deserialize<TableSchema>(sent.Xml)!.TableName);
        }

        [Fact]
        [DisplayName("SaveFormSchemaAsync sends SaveDefine for FormSchema")]
        public async Task SaveFormSchemaAsync_SendsFormSchema()
        {
            var (access, transport) = CreateForSave();

            await access.SaveFormSchemaAsync(new FormSchema("Order", "Order"));

            var sent = LastSave(transport);
            Assert.Equal(DefineType.FormSchema, sent.DefineType);
            Assert.Equal("Order", XmlCodec.Deserialize<FormSchema>(sent.Xml)!.ProgId);
        }

        [Fact]
        [DisplayName("SaveFormLayoutAsync sends SaveDefine for FormLayout")]
        public async Task SaveFormLayoutAsync_SendsFormLayout()
        {
            var (access, transport) = CreateForSave();

            await access.SaveFormLayoutAsync(new FormLayout { LayoutId = "Order" });

            Assert.Equal(DefineType.FormLayout, LastSave(transport).DefineType);
        }

        [Fact]
        [DisplayName("SaveLanguageAsync sends SaveDefine for Language keyed by the language and namespace")]
        public async Task SaveLanguageAsync_SendsLanguageAndNamespaceKeys()
        {
            var (access, transport) = CreateForSave();

            await access.SaveLanguageAsync(new LanguageResource { Lang = "en-US", Namespace = "Order" });

            var sent = LastSave(transport);
            Assert.Equal(DefineType.Language, sent.DefineType);
            Assert.Equal(["en-US", "Order"], sent.Keys!);
        }

        [Fact]
        [DisplayName("GetMenuSettingsAsync sends GetDefine for MenuSettings and deserializes the returned XML")]
        public async Task GetMenuSettingsAsync_ReturnsMenuSettings()
        {
            var (access, transport) = Create(_ => new GetDefineResponse { Xml = XmlCodec.Serialize(new MenuSettings()) });

            var menu = await access.GetMenuSettingsAsync();

            Assert.NotNull(menu);
            Assert.Equal(MethodOf(SystemActions.GetDefine), transport.LastCall!.Method);
            Assert.Equal(DefineType.MenuSettings, transport.LastCall!.SentValue<GetDefineRequest>().DefineType);
        }

        [Fact]
        [DisplayName("A GetDefine answer with no XML reads as null")]
        public async Task GetCurrencySettingsAsync_EmptyXml_ReturnsNull()
        {
            var (access, _) = Create(_ => new GetDefineResponse { Xml = string.Empty });

            Assert.Null(await access.GetCurrencySettingsAsync());
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayoutAsync returns the tenant layout and sends the customization action")]
        public async Task GetCustomizeFormLayoutAsync_Override_ReturnsLayout()
        {
            var (access, transport) = Create(_ => new GetFormLayoutResponse
            {
                Xml = XmlCodec.Serialize(new FormLayout { LayoutId = "OrderTenant" })
            });

            var layout = await access.GetCustomizeFormLayoutAsync("Order", "OrderTenant");

            Assert.Equal("OrderTenant", layout!.LayoutId);
            Assert.Equal(MethodOf(SystemActions.GetCustomizeFormLayout), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<GetFormLayoutRequest>();
            Assert.Equal("Order", sent.ProgId);
            Assert.Equal("OrderTenant", sent.LayoutId);
        }

        [Fact]
        [DisplayName("GetCustomizeFormLayoutAsync caches a missing override as null and does not fetch it again")]
        public async Task GetCustomizeFormLayoutAsync_NoOverride_CachesNull()
        {
            var (access, transport) = Create(_ => new GetFormLayoutResponse { Xml = string.Empty });

            Assert.Null(await access.GetCustomizeFormLayoutAsync("Order"));
            Assert.Null(await access.GetCustomizeFormLayoutAsync("Order"));

            Assert.Single(transport.Calls);
        }

        [Fact]
        [DisplayName("GetCustomizeLanguageAsync returns the tenant resource and sends the customization action")]
        public async Task GetCustomizeLanguageAsync_Override_ReturnsResource()
        {
            var (access, transport) = Create(_ => new GetLanguageResponse
            {
                Xml = XmlCodec.Serialize(new LanguageResource { Lang = "en-US", Namespace = "Order" })
            });

            var resource = await access.GetCustomizeLanguageAsync("en-US", "Order");

            Assert.Equal("Order", resource!.Namespace);
            Assert.Equal(MethodOf(SystemActions.GetCustomizeLanguage), transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("GetCustomizeLanguageAsync returns null when the tenant has no override")]
        public async Task GetCustomizeLanguageAsync_NoOverride_ReturnsNull()
        {
            var (access, _) = Create(_ => new GetLanguageResponse { Xml = string.Empty });

            Assert.Null(await access.GetCustomizeLanguageAsync("en-US", "Order"));
        }

        [Fact]
        [DisplayName("A failed customization fetch is not cached and the next read retries")]
        public async Task GetCustomizeFormLayoutAsync_FailedFetch_IsEvictedAndRetried()
        {
            int calls = 0;
            var transport = new FakeApiTransport(call =>
            {
                if (++calls == 1)
                    throw new JsonRpcErrorException(-32603, "boom");
                return FakeApiTransport.Answer(call, new GetFormLayoutResponse
                {
                    Xml = XmlCodec.Serialize(new FormLayout { LayoutId = "Order" })
                });
            });
            var access = new ClientDefineAccess(new SystemApiConnector(TestClients.Fake(transport)));

            await Assert.ThrowsAnyAsync<Exception>(() => access.GetCustomizeFormLayoutAsync("Order"));
            var layout = await access.GetCustomizeFormLayoutAsync("Order");

            Assert.Equal("Order", layout!.LayoutId);
            Assert.Equal(2, transport.Calls.Count);
        }

        [Fact]
        [DisplayName("Cancelling a wait for a customization fetch throws OperationCanceledException and keeps the fetch")]
        public async Task GetCustomizeFormLayoutAsync_CancelledWait_ThrowsAndKeepsFetch()
        {
            var connector = new PendingConnector();
            var access = new ClientDefineAccess(connector);
            using var cts = new CancellationTokenSource();

            var waiting = access.GetCustomizeFormLayoutAsync("Order", cancellationToken: cts.Token);
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

            connector.Complete(new FormLayout { LayoutId = "Order" });
            var layout = await access.GetCustomizeFormLayoutAsync("Order");

            Assert.Equal("Order", layout!.LayoutId);
            Assert.Equal(1, connector.CallCount);
        }

        /// <summary>
        /// A connector whose customization fetch stays pending until the test completes it.
        /// </summary>
        private sealed class PendingConnector : SystemApiConnector
        {
            private readonly TaskCompletionSource<FormLayout?> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public PendingConnector() : base(TestClients.Local(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid())) { }

            public int CallCount { get; private set; }

            public void Complete(FormLayout layout) => _pending.SetResult(layout);

            public override Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return _pending.Task;
            }
        }
    }
}
