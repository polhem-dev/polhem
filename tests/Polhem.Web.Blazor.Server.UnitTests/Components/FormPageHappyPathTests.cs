using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers <see cref="FormPage.OnInitializedAsync"/> with a valid ProgId and a working backend.
    /// A <see cref="FakeApiServer"/> simulates the full
    /// initialization path, covering the schema fetch, layout creation, DataObject creation and ReloadList inside the try block.
    /// </summary>
    public class FormPageHappyPathTests
    {
        /// <summary>
        /// A server that serves the schema and a layout generated from it as the stored definitions, and an empty list.
        /// The page reads the stored layout definition; it never derives one from the schema. This fake stands in for
        /// that stored definition.
        /// </summary>
        private static FakeApiServer Server(FormSchema schema)
            => new FakeApiServer()
                .On<GetListRequest>($"TestProg.{FormActions.GetList}", _ => new GetListResponse { Table = new DataTable("FakeList") })
                .On<GetDefineRequest>($"{SysProgIds.System}.{SystemActions.GetDefine}", request => new GetDefineResponse
                {
                    Xml = request.DefineType switch
                    {
                        DefineType.FormSchema => XmlCodec.Serialize(schema),
                        DefineType.FormLayout => XmlCodec.Serialize(FormLayoutGenerator.Generate(schema, schema.ProgId)),
                        _ => throw new NotSupportedException($"GetDefine for {request.DefineType} is not supported by the fake."),
                    },
                });

        /// <summary>
        /// Gives the page a client over <see cref="Server"/>. The fake serves stored definitions only, so the page
        /// takes the path that renders them as stored; the default loader path is covered by <c>FormPageBunitTests</c>.
        /// </summary>
        private static void Inject(FormPage page, FormSchema schema)
        {
            typeof(FormPage).GetProperty("Client", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, Server(schema).CreateClient());
            typeof(FormPage).GetProperty("Options", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, new PolhemBlazorOptions { UseDefinitionLoader = false }.UseLocalProvider());
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
        [DisplayName("OnInitializedAsync completes initialization without setting _error for a valid ProgId and a working backend")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingBackend_CompletesWithoutError()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            Inject(page, schema);

            await InvokeOnInitializedAsync(page);

            var error = typeof(FormPage).GetField(
                "_error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page) as string;
            Assert.Null(error);
        }

        [Fact]
        [DisplayName("OnInitializedAsync sets _isInitializing to false for a valid ProgId and a working backend")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingBackend_SetsIsInitializingFalse()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            Inject(page, schema);

            await InvokeOnInitializedAsync(page);

            var isInitializing = (bool)typeof(FormPage).GetField(
                "_isInitializing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;
            Assert.False(isInitializing);
        }

        [Fact]
        [DisplayName("OnInitializedAsync sets _dataObject to non-null for a valid ProgId and a working backend")]
        public async Task OnInitializedAsync_ValidProgIdAndWorkingBackend_SetsDataObjectNonNull()
        {
            var schema = BuildSchema();
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            Inject(page, schema);

            await InvokeOnInitializedAsync(page);

            var dataObject = typeof(FormPage).GetField(
                "_dataObject", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page);
            Assert.NotNull(dataObject);
        }
    }
}
