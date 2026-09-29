using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
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
    /// Covers <see cref="FormPage.OnInitializedAsync"/> with a valid ProgId and a working Factory.
    /// <see cref="FakeSystemConnector"/> and <see cref="FakeFormConnector"/> simulate the full
    /// initialization path, covering the schema fetch, layout creation, DataObject creation and ReloadList inside the try block.
    /// </summary>
    public class FormPageHappyPathTests
    {
        private sealed class FakeFormConnector : FormApiConnector
        {
            public FakeFormConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.Empty, "TestProg") { }

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "",
                FilterNode? filter = null,
                SortFieldCollection? sortFields = null,
                PagingOptions? paging = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new GetListResponse { Table = new DataTable("FakeList") });
        }

        private sealed class FakeSystemConnector : SystemApiConnector
        {
            private readonly FormSchema _schema;

            public FakeSystemConnector(FormSchema schema) : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.Empty)
            {
                _schema = schema;
            }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null, CancellationToken cancellationToken = default)
            {
                if (typeof(T) == typeof(FormSchema))
                    return Task.FromResult((T)(object)_schema);
                // The page reads the stored layout definition; it never derives one from the schema.
                // This fake stands in for that stored definition.
                if (typeof(T) == typeof(FormLayout))
                    return Task.FromResult((T)(object)FormLayoutGenerator.Generate(_schema, _schema.ProgId));

                throw new NotSupportedException($"GetDefineAsync<{typeof(T).Name}> is not supported by the fake.");
            }
        }

        private sealed class FakeFactory : PolhemApiConnectorFactory
        {
            private readonly FakeSystemConnector _systemConnector;

            // The fake system connector serves stored definitions only, so the page takes the path that
            // renders them as stored. The default loader path is covered by `FormPageBunitTests`.
            public FakeFactory(FormSchema schema)
                : base(new PolhemBlazorOptions { UseDefinitionLoader = false }.UseLocalProvider(),
                    new Polhem.Api.Client.ApiSessionContext(), Polhem.Tests.Shared.EmptyServiceProvider.Instance)
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
        [DisplayName("OnInitializedAsync completes initialization without setting _error for a valid ProgId and a working Factory")]
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
        [DisplayName("OnInitializedAsync sets _isInitializing to false for a valid ProgId and a working Factory")]
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
        [DisplayName("OnInitializedAsync sets _dataObject to non-null for a valid ProgId and a working Factory")]
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
