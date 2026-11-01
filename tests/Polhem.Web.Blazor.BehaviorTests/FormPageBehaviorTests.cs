using System.ComponentModel;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's form page must do. A UI package derives from this class and supplies how to find what its
    /// controls render differently; the page's own class names are common to all of them.
    /// </summary>
    public abstract class FormPageBehaviorTests : BunitContext, IAsyncLifetime
    {
        private const string Toolbar = ".polhem-form-page__toolbar button";
        private const string Pager = ".polhem-form-page__pager button";
        private const string Record = ".polhem-form-layout";
        private const string Notice = ".polhem-form-page__notice";

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

        private static string Text(string key) => PolhemUIText.Get(Polhem.Web.Blazor.Components.PolhemBlazorText.GetLocalizer(null), key);

        private FormPageTestBackend Register(FormPageTestBackend? backend = null)
        {
            backend ??= new FormPageTestBackend();
            backend.Register(Services);
            OnServicesRegistered();
            return backend;
        }

        private IRenderedComponent<ContainerFragment> RenderPage(bool confirmDelete = true)
        {
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", FormPageTestBackend.EmpProgId);
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

        private static IElement Command(IRenderedComponent<ContainerFragment> cut, string key)
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
            FindInDialogs(cut, ".polhem-related-record-dialog__toolbar button")[0].Click();
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
    }
}
