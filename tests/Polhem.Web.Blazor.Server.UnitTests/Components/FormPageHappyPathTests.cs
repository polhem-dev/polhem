using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// 補強 <see cref="FormPage.OnInitializedAsync"/> 有效 ProgId 且 Factory 正常運作時的覆蓋率。
    /// 以 <see cref="FakeSystemConnector"/> 和 <see cref="FakeFormConnector"/> 模擬完整的
    /// 初始化路徑，涵蓋 try 區塊內 schema 取得、layout 建立、DataObject 建立及 ReloadList 等行為。
    /// </summary>
    public class FormPageHappyPathTests
    {
        private sealed class FakeFormConnector : FormApiConnector
        {
            public FakeFormConnector() : base(Guid.Empty, "TestProg") { }

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "",
                FilterNode? filter = null,
                SortFieldCollection? sortFields = null,
                PagingOptions? paging = null)
                => Task.FromResult(new GetListResponse { Table = new DataTable("FakeList") });
        }

        private sealed class FakeSystemConnector : SystemApiConnector
        {
            private readonly FormSchema _schema;

            public FakeSystemConnector(FormSchema schema) : base(Guid.Empty)
            {
                _schema = schema;
            }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null)
            {
                if (typeof(T) == typeof(FormSchema))
                    return Task.FromResult((T)(object)_schema);
                // The page reads the stored layout definition; it never derives one from the schema.
                // This fake stands in for that stored definition.
                if (typeof(T) == typeof(FormLayout))
                    return Task.FromResult((T)(object)FormLayoutGenerator.Generate(_schema, _schema.ProgId));

                throw new NotSupportedException($"GetDefineAsync<{typeof(T).Name}> 未在 Fake 中支援。");
            }
        }

        private sealed class FakeFactory : PolhemApiConnectorFactory
        {
            private readonly FakeSystemConnector _systemConnector;

            public FakeFactory(FormSchema schema)
                : base(new PolhemBlazorOptions().UseLocalProvider())
            {
                _systemConnector = new FakeSystemConnector(schema);
            }

            public override FormApiConnector CreateFormConnector(Guid accessToken, string progId)
                => new FakeFormConnector();

            public override SystemApiConnector CreateSystemConnector(Guid accessToken)
                => _systemConnector;
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("TestProg", "TestProg");
            var table = schema.Tables!.Add("TestProg", "TestProg");
            table.Fields!.Add("id", "ID", FieldDbType.String);
            table.Fields.Add("name", "名稱", FieldDbType.String);
            return schema;
        }

        private static async Task InvokeOnInitializedAsync(FormPage page)
        {
            var method = typeof(FormPage).GetMethod(
                "OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;
        }

        [Fact]
        [DisplayName("OnInitializedAsync 有效 ProgId 且 Factory 正常時應初始化完成不設定 _error")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingFactory_CompletesWithoutError()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            typeof(FormPage).GetProperty("Factory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, new FakeFactory(schema));

            await InvokeOnInitializedAsync(page);

            var error = typeof(FormPage).GetField(
                "_error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page) as string;
            Assert.Null(error);
        }

        [Fact]
        [DisplayName("OnInitializedAsync 有效 ProgId 且 Factory 正常時 _isInitializing 應設為 false")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingFactory_SetsIsInitializingFalse()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            typeof(FormPage).GetProperty("Factory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, new FakeFactory(schema));

            await InvokeOnInitializedAsync(page);

            var isInitializing = (bool)typeof(FormPage).GetField(
                "_isInitializing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;
            Assert.False(isInitializing);
        }

        [Fact]
        [DisplayName("OnInitializedAsync 有效 ProgId 且 Factory 正常時 _dataObject 應設為非 null")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingFactory_SetsDataObjectNonNull()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            typeof(FormPage).GetProperty("Factory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, new FakeFactory(schema));

            await InvokeOnInitializedAsync(page);

            var dataObject = typeof(FormPage).GetField(
                "_dataObject", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page);
            Assert.NotNull(dataObject);
        }
    }
}
