using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the four action handlers of <see cref="FormPage"/> when <c>_dataObject</c> is not null,
    /// and <c>ReloadListAsync</c>.
    /// The action handlers trigger an exception with a <see cref="FormDataObject"/> that has no connector.
    /// <c>RunGuardedAsync</c> catches it and sets <c>_error</c>.
    /// <c>ReloadListAsync</c> runs its full path driven by the injected <see cref="FakeFactory"/>.
    /// </summary>
    public class FormPageNonNullDataObjectTests
    {
        private sealed class FakeFormConnector : FormApiConnector
        {
            public FakeFormConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.Empty, "TestProg") { }

            public override Task<GetListResponse> GetListAsync(
                string selectFields = "",
                FilterNode? filter = null,
                SortFieldCollection? sortFields = null,
                PagingOptions? paging = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new GetListResponse { Table = new DataTable("Test") });
        }

        private sealed class FakeFactory : PolhemApiConnectorFactory
        {
            public FakeFactory()
                : base(new PolhemBlazorOptions().UseLocalProvider(), new Polhem.Api.Client.ApiSessionContext(), Polhem.Tests.Shared.EmptyServiceProvider.Instance) { }

            public override FormApiConnector CreateFormConnector(Guid accessToken, string progId)
                => new FakeFormConnector();

            public override SystemApiConnector CreateSystemConnector(Guid accessToken)
                => throw new InvalidOperationException("SystemApiConnector not needed in this test.");
        }

        private static readonly FieldInfo s_dataObjectField =
            typeof(FormPage).GetField("_dataObject", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_errorField =
            typeof(FormPage).GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo s_listRowsField =
            typeof(FormPage).GetField("_listRows", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static FormDataObject CreateDataObjectWithoutConnector()
        {
            var schema = new FormSchema("Test", "Test");
            return new FormDataObject(schema);
        }

        private static async Task InvokeMethodAsync(FormPage page, string methodName, object[]? args = null)
        {
            var method = typeof(FormPage).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, args)!;
            await task;
        }

        private static FormPage CreatePageWithDataObject()
        {
            var page = new FormPage();
            s_dataObjectField.SetValue(page, CreateDataObjectWithoutConnector());
            return page;
        }

        [Fact]
        [DisplayName("OnRowSelectedAsync with a non-null _dataObject and no connector catches the exception through RunGuardedAsync and sets _error")]
        public async Task OnRowSelectedAsync_NonNullDataObjectNoConnector_SetsError()
        {
            var page = CreatePageWithDataObject();
            await InvokeMethodAsync(page, "OnRowSelectedAsync", new object[] { Guid.NewGuid() });
            Assert.StartsWith("LoadAsync requires a FormApiConnector", s_errorField.GetValue(page) as string, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("OnNewAsync with a non-null _dataObject and no connector catches the exception through RunGuardedAsync and sets _error")]
        public async Task OnNewAsync_NonNullDataObjectNoConnector_SetsError()
        {
            var page = CreatePageWithDataObject();
            await InvokeMethodAsync(page, "OnNewAsync", null);
            Assert.StartsWith("NewAsync requires a FormApiConnector", s_errorField.GetValue(page) as string, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("OnSaveAsync with a non-null _dataObject and no connector catches the exception through RunGuardedAsync and sets _error")]
        public async Task OnSaveAsync_NonNullDataObjectNoConnector_SetsError()
        {
            var page = CreatePageWithDataObject();
            await InvokeMethodAsync(page, "OnSaveAsync", null);
            Assert.StartsWith("SaveAsync requires a FormApiConnector", s_errorField.GetValue(page) as string, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("OnDeleteAsync with a non-null _dataObject and no connector catches the exception through RunGuardedAsync and sets _error")]
        public async Task OnDeleteAsync_NonNullDataObjectNoConnector_SetsError()
        {
            var page = CreatePageWithDataObject();
            await InvokeMethodAsync(page, "OnDeleteAsync", null);
            Assert.StartsWith("DeleteAsync requires a FormApiConnector", s_errorField.GetValue(page) as string, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("ReloadListAsync with FakeFactory sets _listRows to a non-null DataTable")]
        public async Task ReloadListAsync_WithFakeFactory_SetsListRows()
        {
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            typeof(FormPage).GetProperty("Factory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, new FakeFactory());

            var method = typeof(FormPage).GetMethod(
                "ReloadListAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;

            Assert.Equal("Test", Assert.IsType<DataTable>(s_listRowsField.GetValue(page)).TableName);
        }
    }
}
