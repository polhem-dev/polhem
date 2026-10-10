using System.ComponentModel;
using System.Data;
using System.Reflection;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Covers the four action handlers of <see cref="FormPage"/> when <c>_dataObject</c> is not null,
    /// and <c>ReloadListAsync</c>.
    /// The action handlers trigger an exception with a <see cref="FormDataObject"/> that has no connector.
    /// <c>RunGuardedAsync</c> catches it and sets <c>_error</c>.
    /// <c>ReloadListAsync</c> runs its full path against a <see cref="FakeApiServer"/>.
    /// </summary>
    public class FormPageNonNullDataObjectTests
    {
        private static FakeApiServer ListServer()
            => new FakeApiServer().On<GetListRequest>($"TestProg.{FormActions.GetList}",
                _ => new GetListResponse { Table = new DataTable("Test") });

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
        [DisplayName("ReloadListAsync sets _listRows to the table the server returns")]
        public async Task ReloadListAsync_WithFakeServer_SetsListRows()
        {
            var page = new FormPage();
            typeof(FormPage).GetProperty("ProgId", BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(page, "TestProg");
            typeof(FormPage).GetProperty("Client", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(page, ListServer().CreateClient());

            var method = typeof(FormPage).GetMethod(
                "ReloadListAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method!.Invoke(page, null)!;
            await task;

            Assert.Equal("Test", Assert.IsType<DataTable>(s_listRowsField.GetValue(page)).TableName);
        }
    }
}
