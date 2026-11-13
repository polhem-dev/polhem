using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// <see cref="LookupDialog"/> on a host without native windows, given the schema and the connector: the rows are
    /// loaded into an overlay card, OK returns the selected row and Cancel returns nothing.
    /// </summary>
    /// <remarks>
    /// The schema and the connector are passed in, so the services registered in <c>UIServices</c> are not needed. A
    /// test that leaves them out relies on no services being registered in this test assembly.
    /// </remarks>
    public class LookupDialogTests
    {
        private static FormSchema BuildCustomerSchema()
        {
            var schema = new FormSchema("Customer", "Customers");
            var table = schema.Tables!.Add("Customer", "Customers");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid));
            table.Fields.Add(new FormField(SysFields.Id, "Code", FieldDbType.String));
            table.Fields.Add(new FormField(SysFields.Name, "Name", FieldDbType.String));
            return schema;
        }

        private static DataTable BuildRows()
        {
            var table = new DataTable("Customer");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Id, typeof(string));
            table.Columns.Add(SysFields.Name, typeof(string));
            table.Rows.Add(Guid.NewGuid(), "C001", "Acme");
            return table;
        }

        [Fact]
        [DisplayName("OK returns the selected row, and the card is titled with the schema's display name")]
        public void ShowAsync_Committed_ReturnsSelectedRow()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var rows = BuildRows();
            var task = LookupDialog.ShowAsync(host.Host, "Customer", BuildCustomerSchema(), new StubConnector(rows));

            Assert.Equal("Customers", host.Title);
            var panel = host.Panel<LookupPanel>();
            panel.Grid.SelectedItem = panel.Grid.DataTable!.DefaultView[0];
            panel.Commit();

            var selected = InlineContinuationScope.Completed(task);
            Assert.NotNull(selected);
            Assert.Equal("Acme", selected![SysFields.Name]);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Cancel returns null")]
        public void ShowAsync_Cancelled_ReturnsNull()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var task = LookupDialog.ShowAsync(host.Host, "Customer", BuildCustomerSchema(), new StubConnector(BuildRows()));

            host.Panel<LookupPanel>().Cancel();

            Assert.Null(InlineContinuationScope.Completed(task));
        }

        [Fact]
        [DisplayName("Without a schema and with no definition source registered, the lookup is refused")]
        public void ShowAsync_NoSchema_Throws()
        {
            var task = LookupDialog.ShowAsync(new OverlayTestHost().Host, "Customer");

            var error = Assert.IsType<InvalidOperationException>(task.Exception?.InnerException);
            Assert.Contains("Customer", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Without a connector and with no connector factory registered, the lookup is refused")]
        public void ShowAsync_NoConnector_Throws()
        {
            var task = LookupDialog.ShowAsync(new OverlayTestHost().Host, "Customer", BuildCustomerSchema());

            var error = Assert.IsType<InvalidOperationException>(task.Exception?.InnerException);
            Assert.Contains(nameof(Polhem.Api.Contracts.Form.IFormApiConnectorFactory), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A token cancelled before the call throws without showing anything")]
        public void ShowAsync_AlreadyCancelled_Throws()
        {
            var host = new OverlayTestHost();

            var task = LookupDialog.ShowAsync(host.Host, "Customer", BuildCustomerSchema(), new StubConnector(BuildRows()),
                new CancellationToken(canceled: true));

            Assert.True(task.IsCanceled);
            Assert.Empty(host.Overlay.Children);
        }

        private sealed class StubConnector(DataTable table)
            : FormApiConnector(Polhem.Api.Client.PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance), "Customer")
        {
            public override Task<GetLookupResponse> GetLookupAsync(
                string searchText = "",
                Polhem.Definition.Paging.PagingOptions? paging = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new GetLookupResponse { Table = table });
        }
    }
}
