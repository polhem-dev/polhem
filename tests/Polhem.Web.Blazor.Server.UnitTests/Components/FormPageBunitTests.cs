using System.ComponentModel;
using System.Data;
using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Renders <see cref="FormPage"/> with bUnit and drives it through its markup: the list loads on initialization,
    /// clicking a list row loads the record, the toolbar buttons call New / Save / Delete on the connector, and a
    /// failure replaces the page with the error message.
    /// </summary>
    /// <remarks>
    /// The services are what <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/> registers,
    /// including the localizer the toolbar reads its text from, with the connector factory replaced by a fake whose
    /// system connector serves a schema, a layout and optionally a zh-TW translation of the schema, and whose form
    /// connector records every call. The factory keeps the default definition loader, so the page assembles its
    /// definitions the way it does for a real host. No backend runs.
    /// <para>
    /// The toolbar text follows the UI culture. Each test pins <c>en-US</c> so the run does not depend on the
    /// machine's culture, and finds a button by the text the registered localizer gives for its key, so the lookup
    /// holds in any culture.
    /// </para>
    /// </remarks>
    public class FormPageBunitTests : BunitContext
    {
        private const string TestProgId = "BunitProg";
        private static readonly Guid s_aliceRowId = new("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid s_bobRowId = new("aaaaaaaa-0000-0000-0000-000000000002");

        private static FormSchema BuildSchema(bool nameRequired = false)
        {
            var schema = new FormSchema(TestProgId, TestProgId) { ListFields = "emp_name" };
            var master = schema.Tables!.Add(TestProgId, TestProgId);
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("emp_name", "Name", FieldDbType.String).Required = nameRequired;
            return schema;
        }

        private static LanguageResource BuildZhTwTranslation()
        {
            var resource = new LanguageResource { Namespace = TestProgId, Lang = "zh-TW" };
            resource.Items.Add(string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.FieldCaptionKeyFormat, "emp_name"), "姓名");
            return resource;
        }

        private static DataSet RecordFor(FormSchema schema, Guid rowId, string name, bool accept = true)
        {
            var dataSet = FormValueBinding.BuildEmptyDataSet(schema);
            var master = dataSet.Tables[TestProgId]!;
            var row = master.NewRow();
            row[SysFields.RowId] = rowId;
            row["emp_name"] = name;
            master.Rows.Add(row);
            if (accept) { dataSet.AcceptChanges(); }
            return dataSet;
        }

        /// <summary>Records every CRUD call and answers from an in-memory list.</summary>
        private sealed class RecordingFormConnector : FormApiConnector
        {
            private readonly FormSchema _schema;

            public RecordingFormConnector(FormSchema schema)
                : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.Empty, TestProgId)
            {
                _schema = schema;
            }

            public int GetListCount { get; private set; }
            public List<Guid> Loaded { get; } = [];
            public int NewCount { get; private set; }
            public List<DataSet> Saved { get; } = [];
            public List<Guid> Deleted { get; } = [];
            public Exception? GetDataFailure { get; set; }
            public string NewName { get; set; } = "New employee";

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "", FilterNode? filter = null, SortFieldCollection? sortFields = null,
                PagingOptions? paging = null, CancellationToken cancellationToken = default)
            {
                GetListCount++;
                var table = new DataTable(TestProgId);
                table.Columns.Add(SysFields.RowId, typeof(Guid));
                table.Columns.Add("emp_name", typeof(string));
                table.Rows.Add(s_aliceRowId, "Alice");
                table.Rows.Add(s_bobRowId, "Bob");
                return Task.FromResult(new GetListResponse { Table = table });
            }

            public override Task<GetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
            {
                Loaded.Add(rowId);
                if (GetDataFailure != null) { throw GetDataFailure; }
                string name = rowId == s_aliceRowId ? "Alice" : "Bob";
                return Task.FromResult(new GetDataResponse { DataSet = RecordFor(_schema, rowId, name) });
            }

            public override Task<GetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
            {
                NewCount++;
                // As the server returns it: the new master row is still an added row.
                return Task.FromResult(new GetNewDataResponse { DataSet = RecordFor(_schema, Guid.NewGuid(), NewName, accept: false) });
            }

            public override Task<SaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
            {
                Saved.Add(dataSet);
                return Task.FromResult(new SaveResponse { DataSet = dataSet });
            }

            public override Task<DeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
            {
                Deleted.Add(rowId);
                return Task.FromResult(new DeleteResponse { RowsAffected = 1 });
            }
        }

        /// <summary>
        /// Serves the stored definitions: the schema, its layout, and the zh-TW translation when one is given. No
        /// tenant customization exists, and a language without a translation is a missing file, as on a server.
        /// </summary>
        private sealed class DefinitionConnector : SystemApiConnector
        {
            private readonly FormSchema _schema;
            private readonly bool _hasLayout;
            private readonly LanguageResource? _zhTw;

            public DefinitionConnector(FormSchema schema, bool hasLayout, LanguageResource? zhTw)
                : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.Empty)
            {
                _schema = schema;
                _hasLayout = hasLayout;
                _zhTw = zhTw;
            }

            public int LanguageFetchCount { get; private set; }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null, CancellationToken cancellationToken = default)
            {
                if (typeof(T) == typeof(FormSchema)) { return Task.FromResult((T)(object)_schema); }
                if (typeof(T) == typeof(FormLayout))
                {
                    return Task.FromResult(_hasLayout ? (T)(object)FormLayoutGenerator.Generate(_schema, TestProgId) : default!);
                }
                if (typeof(T) == typeof(LanguageResource))
                {
                    LanguageFetchCount++;
                    if (_zhTw != null && keys is [var lang, var ns] && lang == _zhTw.Lang && ns == _zhTw.Namespace)
                        return Task.FromResult((T)(object)_zhTw);
                    throw new FileNotFoundException("No language resource.", string.Join("/", keys ?? []));
                }
                throw new NotSupportedException($"GetDefineAsync<{typeof(T).Name}> is not supported by the fake.");
            }

            public override Task<FormLayout?> GetCustomizeFormLayoutAsync(string progId, string layoutId = "",
                CancellationToken cancellationToken = default)
                => Task.FromResult<FormLayout?>(null);

            public override Task<LanguageResource?> GetCustomizeLanguageAsync(string lang, string ns,
                CancellationToken cancellationToken = default)
                => Task.FromResult<LanguageResource?>(null);
        }

        private sealed class FakeFactory : PolhemApiConnectorFactory
        {
            private readonly DefinitionConnector _system;
            private readonly RecordingFormConnector _form;

            public FakeFactory(DefinitionConnector system, RecordingFormConnector form, PolhemBlazorOptions options)
                : base(options, new ApiSessionContext(), Polhem.Tests.Shared.EmptyServiceProvider.Instance)
            {
                _system = system;
                _form = form;
            }

            public override SystemApiConnector CreateSystemConnector(Guid accessToken) => _system;

            public override FormApiConnector CreateFormConnector(Guid accessToken, string progId) => _form;
        }

        private DefinitionConnector? _definitions;

        private RecordingFormConnector RegisterFactory(bool hasLayout = true, bool nameRequired = false,
            LanguageResource? zhTw = null, bool useDefinitionLoader = true)
        {
            var schema = BuildSchema(nameRequired);
            var form = new RecordingFormConnector(schema);
            _definitions = new DefinitionConnector(schema, hasLayout, zhTw);
            var options = new PolhemBlazorOptions().UseLocalProvider();
            options.UseDefinitionLoader = useDefinitionLoader;
            Services.AddPolhemBlazor();
            // Registered after `AddPolhemBlazor`, so this fake is the factory the page resolves.
            Services.AddSingleton<PolhemApiConnectorFactory>(new FakeFactory(_definitions, form, options));
            return form;
        }

        private static string FieldLabel(IRenderedComponent<FormPage> cut)
            => cut.Find("label[for='polhem-form-emp_name']").TextContent.Trim();

        private IRenderedComponent<FormPage> RenderPage(string progId = TestProgId)
            => Render<FormPage>(p => p.Add(c => c.ProgId, progId));

        private AngleSharp.Dom.IElement ToolbarButton(IRenderedComponent<FormPage> cut, string textKey)
        {
            string text = PolhemUIText.Get(Services.GetRequiredService<IStringLocalizer<PolhemUIText>>(), textKey);
            return cut.FindAll("div.polhem-form-page__toolbar button").Single(b => b.TextContent == text);
        }

        private bool IsDisabled(IRenderedComponent<FormPage> cut, string textKey)
            => ToolbarButton(cut, textKey).HasAttribute("disabled");

        private void Click(IRenderedComponent<FormPage> cut, string textKey)
            => ToolbarButton(cut, textKey).Click();

        [Fact]
        [DisplayName("On load the page lists the rows from GetList, and Save and Delete stay disabled until a record is open")]
        public void Initialize_ListsRowsAndDisablesSaveAndDeleteWithoutRecord()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();

            var cut = RenderPage();

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count));
            Assert.Equal(1, form.GetListCount);
            Assert.Contains("Alice", cut.Find("table.polhem-dynamic-grid").TextContent, StringComparison.Ordinal);
            Assert.False(IsDisabled(cut, PolhemUIText.New));
            Assert.True(IsDisabled(cut, PolhemUIText.Save));
            Assert.True(IsDisabled(cut, PolhemUIText.Delete));
            Assert.Empty(cut.FindAll("div.polhem-form-page__error"));
        }

        [Fact]
        [DisplayName("Clicking a list row loads that record and enables Save and Delete")]
        public void RowClick_LoadsRecordAndEnablesSaveAndDelete()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count));

            cut.FindAll("tr.polhem-dynamic-grid__row")[1].Click();

            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));
            Assert.Equal([s_bobRowId], form.Loaded);
            Assert.False(IsDisabled(cut, PolhemUIText.Delete));
        }

        [Fact]
        [DisplayName("New asks the connector for a new record and enables Save")]
        public void New_RequestsNewDataAndEnablesSave()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.New);

            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));
            Assert.Equal(1, form.NewCount);
        }

        [Fact]
        [DisplayName("Under zh-TW the toolbar shows the translated labels and its buttons still work")]
        public void Toolbar_UnderZhTw_ShowsTranslatedLabelsAndWorks()
        {
            using var culture = new CultureScope("zh-TW");
            var form = RegisterFactory();
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));

            Assert.NotEqual(PolhemUIText.New, ToolbarButton(cut, PolhemUIText.New).TextContent);
            Click(cut, PolhemUIText.New);

            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));
            Assert.Equal(1, form.NewCount);
        }

        [Fact]
        [DisplayName("Save sends the open record to the connector and reloads the list")]
        public void Save_SavesOpenRecordAndReloadsList()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count));
            cut.FindAll("tr.polhem-dynamic-grid__row")[0].Click();
            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.Save);

            cut.WaitForAssertion(() => Assert.Equal(2, form.GetListCount));
            var saved = Assert.Single(form.Saved);
            Assert.Equal(s_aliceRowId, saved.Tables[TestProgId]!.Rows[0][SysFields.RowId]);
            Assert.Empty(cut.FindAll("div.polhem-form-page__error"));
        }

        [Fact]
        [DisplayName("Delete removes the open record through the connector, reloads the list and closes the record")]
        public void Delete_DeletesOpenRecordReloadsListAndClosesRecord()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count));
            cut.FindAll("tr.polhem-dynamic-grid__row")[0].Click();
            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Delete)));

            Click(cut, PolhemUIText.Delete);

            cut.WaitForAssertion(() => Assert.Equal(2, form.GetListCount));
            Assert.Equal([s_aliceRowId], form.Deleted);
            Assert.True(IsDisabled(cut, PolhemUIText.Save));
            Assert.True(IsDisabled(cut, PolhemUIText.Delete));
        }

        [Fact]
        [DisplayName("A failing action replaces the page with its error message")]
        public void Action_Fails_ShowsErrorMessage()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();
            form.GetDataFailure = new InvalidOperationException("Record is locked by another user.");
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count));

            cut.FindAll("tr.polhem-dynamic-grid__row")[0].Click();

            cut.WaitForAssertion(() => Assert.Equal(
                "Record is locked by another user.", cut.Find("div.polhem-form-page__error").TextContent));
            Assert.Empty(cut.FindAll("div.polhem-form-page__toolbar"));
        }

        [Fact]
        [DisplayName("A missing FormLayout definition shows a configuration error instead of the form")]
        public void Initialize_NoFormLayout_ShowsConfigurationError()
        {
            using var culture = new CultureScope("en-US");
            RegisterFactory(hasLayout: false);

            var cut = RenderPage();

            // The message comes from the definition loader the page assembles through by default.
            cut.WaitForAssertion(() => Assert.Contains(
                $"No FormLayout definition found for layout '{TestProgId}'", cut.Find("div.polhem-form-page__error").TextContent,
                StringComparison.Ordinal));
            Assert.Empty(cut.FindAll("div.polhem-form-page__toolbar"));
        }

        [Fact]
        [DisplayName("A blank ProgId shows the ProgId error without calling the backend")]
        public void Initialize_BlankProgId_ShowsProgIdError()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory();

            var cut = RenderPage(progId: string.Empty);

            Assert.Equal("FormPage.ProgId must be set.", cut.Find("div.polhem-form-page__error").TextContent);
            Assert.Equal(0, form.GetListCount);
        }

        [Fact]
        [DisplayName("By default the page localizes its definitions, so under zh-TW the field caption is the translation")]
        public void Initialize_DefaultLoader_UnderZhTw_ShowsTranslatedCaption()
        {
            using var culture = new CultureScope("zh-TW");
            RegisterFactory(zhTw: BuildZhTwTranslation());
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.New);

            cut.WaitForAssertion(() => Assert.Equal("姓名", FieldLabel(cut)));
            Assert.Contains("姓名", cut.Find("table.polhem-dynamic-grid").TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With UseDefinitionLoader off the page renders the stored captions and fetches no language resources")]
        public void Initialize_LoaderOff_UnderZhTw_ShowsStoredCaption()
        {
            using var culture = new CultureScope("zh-TW");
            RegisterFactory(zhTw: BuildZhTwTranslation(), useDefinitionLoader: false);
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.New);

            cut.WaitForAssertion(() => Assert.Equal("Name", FieldLabel(cut)));
            Assert.Equal(0, _definitions!.LanguageFetchCount);
        }

        [Fact]
        [DisplayName("A DefinitionLoader passed to the page is used instead of the factory's")]
        public void Initialize_PageLoader_OverridesFactoryDefault()
        {
            using var culture = new CultureScope("zh-TW");
            RegisterFactory(useDefinitionLoader: false);
            var translated = new DefinitionConnector(BuildSchema(), hasLayout: true, BuildZhTwTranslation());
            var loader = new Polhem.Api.Client.Definitions.FormDefinitionLoader(new ClientDefineAccess(translated));
            var cut = Render<FormPage>(p => p.Add(c => c.ProgId, TestProgId).Add(c => c.DefinitionLoader, loader));
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.New);

            cut.WaitForAssertion(() => Assert.Equal("姓名", FieldLabel(cut)));
            Assert.Equal(0, _definitions!.LanguageFetchCount);
        }

        [Fact]
        [DisplayName("Save with an empty required field names it above the toolbar, keeps the form and sends nothing")]
        public void Save_RequiredFieldEmpty_ShowsNoticeAndDoesNotSave()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory(nameRequired: true);
            form.NewName = " ";
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));
            Click(cut, PolhemUIText.New);
            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));

            Click(cut, PolhemUIText.Save);

            cut.WaitForAssertion(() => Assert.Equal(
                "Fill in the required fields: Name", cut.Find("div.polhem-form-page__notice").TextContent));
            Assert.Empty(form.Saved);
            Assert.Single(cut.FindAll("div.polhem-form-page__toolbar"));
            Assert.Empty(cut.FindAll("div.polhem-form-page__error"));
        }

        [Fact]
        [DisplayName("Once the required field is filled, Save sends the record and clears the notice")]
        public void Save_AfterFillingRequiredField_SavesAndClearsNotice()
        {
            using var culture = new CultureScope("en-US");
            var form = RegisterFactory(nameRequired: true);
            form.NewName = string.Empty;
            var cut = RenderPage();
            cut.WaitForAssertion(() => Assert.True(IsDisabled(cut, PolhemUIText.Save)));
            Click(cut, PolhemUIText.New);
            cut.WaitForAssertion(() => Assert.False(IsDisabled(cut, PolhemUIText.Save)));
            Click(cut, PolhemUIText.Save);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("div.polhem-form-page__notice")));

            cut.Find("#polhem-form-emp_name").Change("Carol");
            Click(cut, PolhemUIText.Save);

            cut.WaitForAssertion(() => Assert.Single(form.Saved));
            Assert.Empty(cut.FindAll("div.polhem-form-page__notice"));
        }
    }
}
