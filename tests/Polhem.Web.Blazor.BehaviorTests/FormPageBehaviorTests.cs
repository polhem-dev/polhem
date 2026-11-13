using System.ComponentModel;
using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client.Definitions;
using Polhem.Api.Client;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's form page must do. A UI package derives from this class and supplies how to find what its
    /// controls render differently; the page's own class names are common to all of them.
    /// </summary>
    public abstract class FormPageBehaviorTests : BunitContext, IAsyncLifetime
    {
        /// <summary>Gets the selector of a command button on the toolbar.</summary>
        protected virtual string Toolbar => ".polhem-form-page__toolbar button";

        /// <summary>Gets the selector of a button on a related record's toolbar.</summary>
        protected virtual string RelatedRecordButton => ".polhem-related-record-dialog__toolbar button";

        /// <summary>Gets the selector of a button of the pager.</summary>
        protected virtual string Pager => ".polhem-form-page__pager button";
        private const string Record = ".polhem-form-layout";
        private const string Notice = ".polhem-form-page__notice";
        private const string NameInput = "#polhem-form-emp_name";
        private const string OpensRecord = ".polhem-field-editor--opens-record";

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Runs after the backend's services are registered, the first point a test may resolve a service.</summary>
        protected virtual void OnServicesRegistered()
        {
        }

        /// <summary>Gets the form page component.</summary>
        protected abstract Type PageType { get; }

        /// <summary>Gets the selector of a list row.</summary>
        protected abstract string RowSelector { get; }

        /// <summary>Gets the selector of a sortable list header.</summary>
        protected abstract string SortSelector { get; }

        /// <summary>Double-clicks a list row.</summary>
        /// <param name="row">The row.</param>
        protected abstract void DoubleClickRow(IElement row);

        /// <summary>Double-clicks the editor of a field on the record.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="fieldName">The field.</param>
        protected abstract void DoubleClickField(IRenderedComponent<ContainerFragment> cut, string fieldName);

        /// <summary>Finds what a dialog of the page renders, which may be outside the page's markup.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="selector">The selector.</param>
        /// <returns>The elements.</returns>
        protected abstract IReadOnlyList<IElement> FindInDialogs(IRenderedComponent<ContainerFragment> cut, string selector);

        /// <summary>Chooses the rows per page.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="pageSize">The rows per page.</param>
        protected abstract void ChoosePageSize(IRenderedComponent<ContainerFragment> cut, int pageSize);

        private static string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(null), key);

        private FormPageTestBackend Register(FormPageTestBackend? backend = null)
        {
            backend ??= new FormPageTestBackend();
            backend.Register(Services);
            OnServicesRegistered();
            return backend;
        }

        private IRenderedComponent<ContainerFragment> RenderPage(bool confirmDelete = true, string progId = FormPageTestBackend.EmpProgId)
        {
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", progId);
                builder.AddAttribute(2, "IsCompact", (bool?)false);
                builder.AddAttribute(3, "ConfirmDelete", confirmDelete);
                builder.CloseComponent();
            });
            return cut;
        }

        private IRenderedComponent<ContainerFragment> RenderList(bool confirmDelete = true)
        {
            var cut = RenderPage(confirmDelete);
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            return cut;
        }

        private static IElement? Button(IRenderedComponent<ContainerFragment> cut, string selector, string key)
            => cut.FindAll(selector).SingleOrDefault(b => b.TextContent.Trim() == Text(key));

        private IElement Command(IRenderedComponent<ContainerFragment> cut, string key)
            => Button(cut, Toolbar, key) ?? throw new InvalidOperationException($"No {key} command.");

        private void OpenRow(IRenderedComponent<ContainerFragment> cut, string key)
        {
            cut.FindAll(RowSelector)[0].Click();
            Command(cut, key).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
        }

        [Fact]
        [DisplayName("The list loads with View, Edit and Delete disabled until a row is selected")]
        public void Load_NoSelection_DisablesRowCommands()
        {
            using var culture = new CultureScope("en-US");
            Register();

            var cut = RenderList();

            Assert.True(Command(cut, PolhemUIText.View).HasAttribute("disabled"));
            Assert.True(Command(cut, PolhemUIText.Edit).HasAttribute("disabled"));
            Assert.True(Command(cut, PolhemUIText.Delete).HasAttribute("disabled"));
            Assert.False(Command(cut, PolhemUIText.New).HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Under zh-TW the toolbar shows the translated labels and its commands work")]
        public void Load_ZhTw_TranslatesToolbar()
        {
            using var culture = new CultureScope("zh-TW");
            Register();
            var cut = RenderList();

            cut.FindAll(Toolbar).Single(b => b.TextContent.Trim() == "新增").Click();

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
            Assert.Contains(cut.FindAll(Toolbar), b => b.TextContent.Trim() == "儲存");
        }

        [Fact]
        [DisplayName("Selecting a row enables View, Edit and Delete")]
        public void SelectRow_EnablesRowCommands()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();

            cut.FindAll(RowSelector)[0].Click();

            cut.WaitForAssertion(() => Assert.False(Command(cut, PolhemUIText.View).HasAttribute("disabled")));
            Assert.False(Command(cut, PolhemUIText.Edit).HasAttribute("disabled"));
            Assert.False(Command(cut, PolhemUIText.Delete).HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Double-clicking a row opens it read-only with Back")]
        public void DoubleClickRow_OpensReadOnly()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();

            DoubleClickRow(cut.FindAll(RowSelector)[0]);

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Back));
            Assert.Null(Button(cut, Toolbar, PolhemUIText.Save));
        }

        [Fact]
        [DisplayName("Back returns from a viewed record to the list")]
        public void Back_ReturnsToList()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);

            Command(cut, PolhemUIText.Back).Click();

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            Assert.Empty(cut.FindAll(Record));
        }

        [Fact]
        [DisplayName("Edit opens the row with Save and Cancel, and Cancel returns without saving")]
        public void EditThenCancel_DoesNotSave()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Save));

            Command(cut, PolhemUIText.Cancel).Click();

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            Assert.Equal(0, backend.SaveCount);
        }

        [Fact]
        [DisplayName("Save sends the record, returns to the list and reloads it")]
        public void Save_ReturnsAndReloads()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);
            int listsBefore = backend.GetListCount;

            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            Assert.Equal(1, backend.SaveCount);
            Assert.Equal(listsBefore + 1, backend.GetListCount);
        }

        [Fact]
        [DisplayName("New opens a record in Add mode with Save")]
        public void New_OpensRecord()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();

            Command(cut, PolhemUIText.New).Click();

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Save));
        }

        [Fact]
        [DisplayName("Save with an empty required field names it and sends nothing")]
        public void Save_MissingRequired_ShowsNoticeAndKeepsRecord()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(nameRequired: true));
            var cut = RenderList();
            Command(cut, PolhemUIText.New).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));

            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.Contains("Name", cut.Find(Notice).TextContent, StringComparison.Ordinal));
            Assert.Equal(0, backend.SaveCount);
            Assert.Single(cut.FindAll(Record));
        }

        [Fact]
        [DisplayName("Delete asks first, and going ahead deletes the selected row and reloads the list")]
        public void Delete_Confirmed_Deletes()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            cut.FindAll(RowSelector)[0].Click();

            Command(cut, PolhemUIText.Delete).Click();
            cut.WaitForAssertion(() => Assert.Single(FindInDialogs(cut, ".polhem-confirm-dialog__confirm")));
            Assert.Empty(backend.Deleted);
            FindInDialogs(cut, ".polhem-confirm-dialog__confirm")[0].Click();

            cut.WaitForAssertion(() => Assert.Equal([FormPageTestBackend.AliceId], backend.Deleted));
            cut.WaitForAssertion(() => Assert.Empty(FindInDialogs(cut, ".polhem-confirm-dialog__confirm")));
        }

        [Fact]
        [DisplayName("Cancel on the delete question keeps the record and closes the question")]
        public void Delete_Cancelled_KeepsRecord()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            cut.FindAll(RowSelector)[0].Click();
            Command(cut, PolhemUIText.Delete).Click();
            cut.WaitForAssertion(() => Assert.Single(FindInDialogs(cut, ".polhem-confirm-dialog__cancel")));

            FindInDialogs(cut, ".polhem-confirm-dialog__cancel")[0].Click();

            cut.WaitForAssertion(() => Assert.Empty(FindInDialogs(cut, ".polhem-confirm-dialog__cancel")));
            Assert.Empty(backend.Deleted);
            Assert.Equal(2, cut.FindAll(RowSelector).Count);
        }

        [Fact]
        [DisplayName("With ConfirmDelete off, Delete removes the row without asking")]
        public void Delete_NoConfirm_DeletesAtOnce()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList(confirmDelete: false);
            cut.FindAll(RowSelector)[0].Click();

            Command(cut, PolhemUIText.Delete).Click();

            cut.WaitForAssertion(() => Assert.Single(backend.Deleted));
        }

        [Fact]
        [DisplayName("A record that fails to load is a notice, and the list stays on screen")]
        public void OpenRow_Fails_ShowsNotice()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            backend.GetDataFailure = "Record is locked";
            var cut = RenderList();
            cut.FindAll(RowSelector)[0].Click();

            Command(cut, PolhemUIText.View).Click();

            cut.WaitForAssertion(() => Assert.Contains("Record is locked", cut.Find(Notice).TextContent, StringComparison.Ordinal));
            Assert.Equal(2, cut.FindAll(RowSelector).Count);
        }

        [Fact]
        [DisplayName("A form with no definition shows the error in place of the form")]
        public void Load_NoSchema_ShowsError()
        {
            using var culture = new CultureScope("en-US");
            Register(new FormPageTestBackend { MissingSchema = true });

            var cut = RenderPage();

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".polhem-form-page__error")));
            Assert.Empty(cut.FindAll(Toolbar));
        }

        [Fact]
        [DisplayName("A user who may only read sees View but not New, Edit or Delete")]
        public async Task Capabilities_ReadOnly_HidesCommands()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(permissionBound: true));
            backend.Capabilities[FormPageTestBackend.EmpModelId] = PermissionActions.Read;
            await Services.GetRequiredService<BlazorUserContext>().EnterCompanyAsync("ACME");

            var cut = RenderList();

            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.View));
            Assert.Null(Button(cut, Toolbar, PolhemUIText.New));
            Assert.Null(Button(cut, Toolbar, PolhemUIText.Edit));
            Assert.Null(Button(cut, Toolbar, PolhemUIText.Delete));
        }

        [Fact]
        [DisplayName("Next reads the next page without counting again and clears the selection")]
        public void Pager_Next_ReadsNextPage()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { TotalRows = 120 });
            var cut = RenderList();
            cut.FindAll(RowSelector)[0].Click();

            Button(cut, Pager, PolhemUIText.NextPage)!.Click();

            cut.WaitForAssertion(() => Assert.Equal(2, backend.ListPaging[^1].Page));
            Assert.False(backend.ListPaging[^1].IncludeTotalCount);
            cut.WaitForAssertion(() => Assert.True(Command(cut, PolhemUIText.View).HasAttribute("disabled")));
        }

        [Fact]
        [DisplayName("Changing the rows per page returns to the first page and counts again")]
        public void Pager_PageSize_ReadsFirstPage()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { TotalRows = 500 });
            var cut = RenderList();
            Button(cut, Pager, PolhemUIText.NextPage)!.Click();
            cut.WaitForAssertion(() => Assert.Equal(2, backend.ListPaging[^1].Page));

            ChoosePageSize(cut, 100);

            cut.WaitForAssertion(() => Assert.Equal(100, backend.ListPaging[^1].PageSize));
            Assert.Equal(1, backend.ListPaging[^1].Page);
            Assert.True(backend.ListPaging[^1].IncludeTotalCount);
        }

        [Fact]
        [DisplayName("Clicking a header reads the list in that order")]
        public void Sort_Header_ReadsInOrder()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();

            cut.Find(SortSelector).Click();

            cut.WaitForAssertion(() => Assert.Equal("emp_name", backend.LastSortField));
        }

        [Fact]
        [DisplayName("Double-clicking a relation field of a viewed record opens its record in a dialog, and Close closes it")]
        public void Relation_DoubleClick_OpensDialog()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);

            DoubleClickField(cut, FormPageTestBackend.RelationField);

            cut.WaitForAssertion(() => Assert.Single(FindInDialogs(cut, ".polhem-related-record-dialog__toolbar ~ .polhem-form-layout")));
            FindInDialogs(cut, RelatedRecordButton)[0].Click();
            cut.WaitForAssertion(() => Assert.Empty(FindInDialogs(cut, ".polhem-related-record-dialog__toolbar")));
        }

        [Fact]
        [DisplayName("A relation field of a form the user may not read names the form in a notice and opens nothing")]
        public async Task Relation_NotPermitted_ShowsNotice()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(permissionBound: true));
            backend.Capabilities[FormPageTestBackend.EmpModelId] = PermissionActions.Read;
            await Services.GetRequiredService<BlazorUserContext>().EnterCompanyAsync("ACME");
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);

            DoubleClickField(cut, FormPageTestBackend.RelationField);

            cut.WaitForAssertion(() => Assert.Contains("Department", cut.Find(Notice).TextContent, StringComparison.Ordinal));
            Assert.Empty(FindInDialogs(cut, ".polhem-related-record-dialog__toolbar"));
        }

        [Fact]
        [DisplayName("The list asks for sys_rowid ahead of the form's ListFields, so a listed row can be opened")]
        public void Load_AsksForRowId()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();

            RenderList();

            Assert.StartsWith(SysFields.RowId, backend.ListSelectFields[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A paged list shows its position, and one without paging metadata shows no page buttons")]
        public void Pager_FollowsPagingMetadata()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            backend.TotalRows = 120;
            var cut = RenderList();
            Assert.Contains("120", cut.Find(".polhem-form-page__page-position").TextContent, StringComparison.Ordinal);

            backend.TotalRows = null;
            Button(cut, Pager, PolhemUIText.NextPage)!.Click();

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Pager)));
        }

        [Fact]
        [DisplayName("Save keeps the saved row selected")]
        public void Save_KeepsRowSelected()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);

            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.False(Command(cut, PolhemUIText.View).HasAttribute("disabled")));
        }

        [Fact]
        [DisplayName("A save the server rejects keeps the record and the edited value on screen and names the reason")]
        public void Save_Rejected_KeepsEdits()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            backend.SaveFailure = "An order needs a line.";
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);
            cut.Find(NameInput).Change("Alice Edited");

            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.Contains("An order needs a line.", cut.Find(Notice).TextContent, StringComparison.Ordinal));
            Assert.Equal("Alice Edited", cut.Find(NameInput).GetAttribute("value"));
            Assert.False(Command(cut, PolhemUIText.Save).HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Once the required field is filled, Save sends the record and clears the notice")]
        public void Save_AfterFillingRequired_Saves()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(nameRequired: true));
            var cut = RenderList();
            Command(cut, PolhemUIText.New).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
            Command(cut, PolhemUIText.Save).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Notice)));

            cut.Find(NameInput).Change("Carol");
            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.Equal(1, backend.SaveCount));
            Assert.Empty(cut.FindAll(Notice));
        }

        [Fact]
        [DisplayName("Editing a source field recomputes the computed field on the open record")]
        public void Edit_RecomputesComputedField()
        {
            using var culture = new CultureScope("en-US");
            Register(new FormPageTestBackend(amountExpression: "price * qty"));
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);

            cut.Find("#polhem-form-price").Change("10");
            cut.Find("#polhem-form-qty").Change("3");

            cut.WaitForAssertion(() => Assert.StartsWith("30", cut.Find("#polhem-form-amount").GetAttribute("value"), StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A computed field whose expression cannot run names the field in a notice")]
        public void Edit_BadExpression_ShowsNotice()
        {
            using var culture = new CultureScope("en-US");
            Register(new FormPageTestBackend(amountExpression: "price * 1.05"));
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);
            Assert.Empty(cut.FindAll(Notice));

            cut.Find("#polhem-form-price").Change("10");

            cut.WaitForAssertion(() => Assert.Contains("Amount is no longer calculated", cut.Find(Notice).TextContent, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A new ProgId loads that program's list, and the same ProgId does not reload")]
        public void ProgId_Change_Reloads()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            var page = cut.FindComponent<FormPageBase>();

            page.Render(p => p.Add(c => c.ProgId, FormPageTestBackend.EmpProgId));
            Assert.Equal(1, backend.GetListCount);

            page.Render(p => p.Add(c => c.ProgId, FormPageTestBackend.OtherProgId));

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
            Assert.Contains("Carol", cut.Find(RowSelector).TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Moving to another program closes an open delete question")]
        public void ProgId_Change_ClosesDeleteQuestion()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            cut.FindAll(RowSelector)[0].Click();
            Command(cut, PolhemUIText.Delete).Click();
            cut.WaitForAssertion(() => Assert.Single(FindInDialogs(cut, ".polhem-confirm-dialog__cancel")));

            cut.FindComponent<FormPageBase>().Render(p => p.Add(c => c.ProgId, FormPageTestBackend.OtherProgId));

            cut.WaitForAssertion(() => Assert.Empty(FindInDialogs(cut, ".polhem-confirm-dialog__cancel")));
            Assert.Empty(backend.Deleted);
        }

        [Fact]
        [DisplayName("A blank ProgId shows the error without calling the backend")]
        public void Load_BlankProgId_ShowsError()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();

            var cut = RenderPage(progId: string.Empty);

            cut.WaitForAssertion(() => Assert.Contains("FormPage.ProgId must be set.", cut.Find(".polhem-form-page__error").TextContent, StringComparison.Ordinal));
            Assert.Equal(0, backend.GetListCount);
        }

        [Fact]
        [DisplayName("A missing FormLayout shows a configuration error instead of the form")]
        public void Load_NoLayout_ShowsError()
        {
            using var culture = new CultureScope("en-US");
            Register(new FormPageTestBackend { MissingLayout = true });

            var cut = RenderPage();

            cut.WaitForAssertion(() => Assert.Contains("No FormLayout definition found", cut.Find(".polhem-form-page__error").TextContent, StringComparison.Ordinal));
            Assert.Empty(cut.FindAll(Toolbar));
        }

        [Fact]
        [DisplayName("A user who may create and update but not delete sees View, New and Edit, and Save on the record")]
        public async Task Capabilities_NoDelete_HidesDelete()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(permissionBound: true));
            backend.Capabilities[FormPageTestBackend.EmpModelId] = PermissionActions.Read | PermissionActions.Create | PermissionActions.Update;
            await Services.GetRequiredService<BlazorUserContext>().EnterCompanyAsync("ACME");
            var cut = RenderList();

            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.New));
            Assert.Null(Button(cut, Toolbar, PolhemUIText.Delete));
            OpenRow(cut, PolhemUIText.Edit);
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Save));
        }

        [Fact]
        [DisplayName("Without an entered company the page treats the user as unrestricted")]
        public void Capabilities_NoCompany_ShowsEveryCommand()
        {
            using var culture = new CultureScope("en-US");
            Register(new FormPageTestBackend(permissionBound: true));

            var cut = RenderList();

            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.New));
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Delete));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("A sensitive field shows only when the capability snapshot grants its category")]
        public async Task Capabilities_SensitiveField_FollowsCategory(bool granted)
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(permissionBound: true));
            backend.Capabilities[FormPageTestBackend.EmpModelId] = PermissionActions.Read | PermissionActions.Update;
            if (granted)
                backend.Capabilities[SensitiveCategory.Amount.ToPermissionModelId()] = PermissionActions.Read | PermissionActions.Update;
            await Services.GetRequiredService<BlazorUserContext>().EnterCompanyAsync("ACME");
            var cut = RenderList();

            OpenRow(cut, PolhemUIText.View);

            Assert.Equal(granted ? 1 : 0, cut.FindAll("#polhem-form-salary").Count);
            Assert.Single(cut.FindAll(NameInput));
        }

        private static LanguageResource ZhTw()
        {
            var resource = new LanguageResource { Namespace = FormPageTestBackend.EmpProgId, Lang = "zh-TW" };
            resource.Items.Add(string.Format(CultureInfo.InvariantCulture, FormSchemaLocalizer.FieldCaptionKeyFormat, "emp_name"), "姓名");
            return resource;
        }

        private static string NameLabel(IRenderedComponent<ContainerFragment> cut)
            => cut.Find("label[for='polhem-form-emp_name']").TextContent.Trim();

        [Fact]
        [DisplayName("By default the page localizes its definitions, in the list and on the record")]
        public void Localization_Default_ShowsTranslation()
        {
            using var culture = new CultureScope("zh-TW");
            Register(new FormPageTestBackend { Translation = ZhTw() });
            var cut = RenderList();
            Assert.Contains("姓名", cut.Markup, StringComparison.Ordinal);

            Command(cut, PolhemUIText.New).Click();

            cut.WaitForAssertion(() => Assert.Equal("姓名", NameLabel(cut)));
        }

        [Fact]
        [DisplayName("With the definition loader off the page shows the stored captions and fetches no language resources")]
        public void Localization_LoaderOff_ShowsStored()
        {
            using var culture = new CultureScope("zh-TW");
            var backend = Register(new FormPageTestBackend { Translation = ZhTw(), UseDefinitionLoader = false });
            var cut = RenderList();

            Command(cut, PolhemUIText.New).Click();

            cut.WaitForAssertion(() => Assert.Equal("Name", NameLabel(cut)));
            Assert.Equal(0, backend.LanguageFetchCount);
        }

        [Fact]
        [DisplayName("A DefinitionLoader passed to the page is used instead of the default one")]
        public void Localization_PageLoader_Wins()
        {
            using var culture = new CultureScope("zh-TW");
            var backend = Register(new FormPageTestBackend { UseDefinitionLoader = false });
            var translated = new FormPageTestBackend { Translation = ZhTw() };
            var loader = new FormDefinitionLoader(new ClientDefineAccess(translated.Server.CreateClient().System));
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", FormPageTestBackend.EmpProgId);
                builder.AddAttribute(2, "IsCompact", (bool?)false);
                builder.AddAttribute(3, "DefinitionLoader", loader);
                builder.CloseComponent();
            });
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));

            Command(cut, PolhemUIText.New).Click();

            cut.WaitForAssertion(() => Assert.Equal("姓名", NameLabel(cut)));
            Assert.Equal(0, backend.LanguageFetchCount);
        }

        [Fact]
        [DisplayName("On a viewed record a relation field with a value shows the open hint")]
        public void Relation_Viewed_ShowsHint()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();

            OpenRow(cut, PolhemUIText.View);

            Assert.NotEmpty(cut.FindAll(OpensRecord));
        }

        [Fact]
        [DisplayName("Closing a related record leaves the viewed record on screen without reloading it")]
        public void Relation_Close_KeepsRecord()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);
            DoubleClickField(cut, FormPageTestBackend.RelationField);
            cut.WaitForAssertion(() => Assert.Single(FindInDialogs(cut, RelatedRecordButton)));
            Assert.Equal([FormPageTestBackend.DeptId], backend.DeptLoaded);

            FindInDialogs(cut, RelatedRecordButton)[0].Click();

            cut.WaitForAssertion(() => Assert.Empty(FindInDialogs(cut, RelatedRecordButton)));
            Assert.Single(cut.FindAll(Record));
            Assert.NotNull(Button(cut, Toolbar, PolhemUIText.Back));
        }

        [Fact]
        [DisplayName("A user with Read on the related form's model opens its record")]
        public async Task Relation_Permitted_Opens()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend(permissionBound: true));
            backend.Capabilities[FormPageTestBackend.EmpModelId] = PermissionActions.Read;
            backend.Capabilities[FormPageTestBackend.DeptModelId] = PermissionActions.Read;
            await Services.GetRequiredService<BlazorUserContext>().EnterCompanyAsync("ACME");
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);

            DoubleClickField(cut, FormPageTestBackend.RelationField);

            cut.WaitForAssertion(() => Assert.Equal([FormPageTestBackend.DeptId], backend.DeptLoaded));
        }

        [Fact]
        [DisplayName("While the record is edited a relation field shows no hint and opens nothing")]
        public void Relation_Editing_OffersNothing()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.Edit);
            Assert.Empty(cut.FindAll(OpensRecord));

            DoubleClickField(cut, FormPageTestBackend.RelationField);

            Assert.Empty(backend.DeptLoaded);
            Assert.Empty(FindInDialogs(cut, ".polhem-related-record-dialog__toolbar"));
        }

        [Fact]
        [DisplayName("An empty relation field on a viewed record shows no hint and opens nothing")]
        public void Relation_Empty_OffersNothing()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { EmptyRelation = true });
            var cut = RenderList();
            OpenRow(cut, PolhemUIText.View);
            Assert.Empty(cut.FindAll(OpensRecord));

            DoubleClickField(cut, FormPageTestBackend.RelationField);

            Assert.Empty(backend.DeptLoaded);
        }

        /// <summary>Tells the page the end of the card list came near the viewport, as the browser does.</summary>
        /// <param name="cut">The rendered page.</param>
        protected abstract void ReachCardListEnd(IRenderedComponent<ContainerFragment> cut);

        private const string Card = ".polhem-card";

        private IRenderedComponent<ContainerFragment> RenderCards(FormPageTestBackend? backend = null)
        {
            Register(backend ?? new FormPageTestBackend { TotalRows = 120, DistinctRows = true });
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", FormPageTestBackend.EmpProgId);
                builder.AddAttribute(2, "IsCompact", (bool?)true);
                builder.CloseComponent();
            });
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(Card)));
            return cut;
        }

        [Fact]
        [DisplayName("A compact list shows cards with captions and values, and no page buttons")]
        public void Cards_Compact_ReplaceTheGrid()
        {
            using var culture = new CultureScope("en-US");
            var cut = RenderCards();

            Assert.Equal(50, cut.FindAll(Card).Count);
            Assert.Empty(cut.FindAll(RowSelector));
            Assert.Empty(cut.FindAll(Pager));
            Assert.Equal("Name", cut.Find(".polhem-card__caption").TextContent);
            Assert.Equal("Row 0", cut.Find(".polhem-card__value").TextContent);
        }

        [Fact]
        [DisplayName("Reaching the end reads the next page and keeps the selection, and the last page stops asking")]
        public void Cards_ReachEnd_AppendsNextPage()
        {
            using var culture = new CultureScope("en-US");
            var cut = RenderCards();
            cut.FindAll(Card)[0].Click();

            ReachCardListEnd(cut);
            cut.WaitForAssertion(() => Assert.Equal(100, cut.FindAll(Card).Count));
            Assert.False(Command(cut, PolhemUIText.View).HasAttribute("disabled"));

            ReachCardListEnd(cut);
            cut.WaitForAssertion(() => Assert.Equal(120, cut.FindAll(Card).Count));
            Assert.Empty(cut.FindAll(".polhem-card-list__end"));
        }

        [Fact]
        [DisplayName("The cards stop at their limit with a note")]
        public void Cards_AtLimit_ShowNote()
        {
            using var culture = new CultureScope("en-US");
            var backend = new FormPageTestBackend { TotalRows = 1100, DistinctRows = true };
            var cut = RenderCards(backend);
            Assert.Empty(cut.FindAll(".polhem-card-list__limit"));

            // Waits on the backend, because searching the markup of many cards after every page is slow.
            foreach (var page in Enumerable.Range(2, 19))
            {
                ReachCardListEnd(cut);
                Assert.Equal(page, backend.ListPaging[^1].Page);
            }

            Assert.Equal(1000, cut.FindAll(Card).Count);

            Assert.Contains("1000", cut.Find(".polhem-card-list__limit").TextContent, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll(".polhem-card-list__end"));
        }

        [Fact]
        [DisplayName("Saving starts the cards over from the first page")]
        public void Cards_Save_StartOver()
        {
            using var culture = new CultureScope("en-US");
            var backend = new FormPageTestBackend { TotalRows = 120, DistinctRows = true };
            var cut = RenderCards(backend);
            ReachCardListEnd(cut);
            cut.WaitForAssertion(() => Assert.Equal(100, cut.FindAll(Card).Count));
            cut.FindAll(Card)[0].Click();
            Command(cut, PolhemUIText.Edit).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));

            Command(cut, PolhemUIText.Save).Click();

            cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(Card).Count));
            Assert.Equal(1, backend.ListPaging[^1].Page);
        }

        [Fact]
        [DisplayName("A card selects on a click and opens read-only on a double-click")]
        public void Cards_ClickAndDoubleClick()
        {
            using var culture = new CultureScope("en-US");
            var cut = RenderCards();
            Assert.True(Command(cut, PolhemUIText.View).HasAttribute("disabled"));

            cut.FindAll(Card)[0].Click();
            cut.WaitForAssertion(() => Assert.False(Command(cut, PolhemUIText.View).HasAttribute("disabled")));
            Assert.Single(cut.FindAll(".polhem-card--selected"));

            DoubleClickRow(cut.FindAll(Card)[0]);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Record)));
            Assert.Empty(cut.FindAll(Card));
        }

        [Fact]
        [DisplayName("Selecting another card renders the two cards whose selection changed and no other")]
        public void Cards_Click_RendersOnlyTheCardsWhoseSelectionChanged()
        {
            using var culture = new CultureScope("en-US");
            var cut = RenderCards();
            cut.FindAll(Card)[0].Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".polhem-card--selected")));
            var cards = cut.FindComponents<ListCard>();
            Assert.Equal(50, cards.Count);
            var before = cards.Select(card => card.RenderCount).ToArray();

            cut.FindAll(Card)[1].Click();

            cut.WaitForAssertion(() => Assert.Contains("polhem-card--selected", cut.FindAll(Card)[1].ClassName, StringComparison.Ordinal));
            var rendered = cards.Select((card, index) => (card, index)).Where(c => c.card.RenderCount != before[c.index]).Select(c => c.index);
            Assert.Equal([0, 1], rendered);
        }

        /// <summary>Opens the filter of the first column, enters text and applies it.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="text">The text.</param>
        protected abstract void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text);

        private const string ClearFilters = ".polhem-form-page__clear-filters";

        [Fact]
        [DisplayName("A column filter reads the first page with the filter, and shows that the list is filtered")]
        public void Filter_Apply_ReadsFirstPage()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { TotalRows = 120 });
            var cut = RenderList();
            Button(cut, Pager, PolhemUIText.NextPage)!.Click();
            cut.WaitForAssertion(() => Assert.Equal(2, backend.ListPaging[^1].Page));
            Assert.Empty(cut.FindAll(ClearFilters));

            ApplyFirstColumnFilter(cut, "Ali");

            cut.WaitForAssertion(() => Assert.NotNull(backend.LastFilter));
            var condition = Assert.IsType<Polhem.Definition.Filters.FilterCondition>(backend.LastFilter);
            Assert.Equal("emp_name", condition.FieldName);
            Assert.Equal(Polhem.Definition.Filters.ComparisonOperator.Contains, condition.Operator);
            Assert.Equal("Ali", condition.Value?.ToString());
            Assert.Equal(1, backend.ListPaging[^1].Page);
            Assert.True(backend.ListPaging[^1].IncludeTotalCount);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ClearFilters)));
        }

        [Fact]
        [DisplayName("A filter that leaves no rows can still be cleared")]
        public void Filter_NoRows_CanBeCleared()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { NoRowsWhenFiltered = true });
            var cut = RenderList();

            ApplyFirstColumnFilter(cut, "nobody");
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(RowSelector)));

            cut.Find(ClearFilters).Click();

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            Assert.Null(backend.LastFilter);
            Assert.Empty(cut.FindAll(ClearFilters));
        }

        [Fact]
        [DisplayName("A filter stays when the next page or a sort reads the list")]
        public void Filter_KeptAcrossPaging()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(new FormPageTestBackend { TotalRows = 120 });
            var cut = RenderList();
            ApplyFirstColumnFilter(cut, "Ali");
            cut.WaitForAssertion(() => Assert.NotNull(backend.LastFilter));

            cut.WaitForAssertion(() => Button(cut, Pager, PolhemUIText.NextPage)!.Click());

            cut.WaitForAssertion(() => Assert.Equal(2, backend.ListPaging[^1].Page));
            Assert.NotNull(backend.LastFilter);
        }

        [Fact]
        [DisplayName("Moving to another program drops the filters")]
        public void Filter_ProgIdChange_Drops()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();
            ApplyFirstColumnFilter(cut, "Ali");
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ClearFilters)));

            cut.FindComponent<FormPageBase>().Render(p => p.Add(c => c.ProgId, FormPageTestBackend.OtherProgId));

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(RowSelector)));
            Assert.Empty(cut.FindAll(ClearFilters));
        }
    }
}
