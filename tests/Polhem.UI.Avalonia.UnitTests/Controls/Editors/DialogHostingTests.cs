using System.ComponentModel;
using System.Data;
using Avalonia.Controls;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Tests for <see cref="DialogHosting"/>, the decision <see cref="LookupDialog"/> and <see cref="RowEditDialog"/>
    /// share between a native window and the overlay host.
    /// </summary>
    /// <remarks>
    /// The platform probes cannot be switched on a desktop test runner, so the decision is tested through its pure
    /// overload. The last test drives <see cref="LookupDialog"/> itself with no owning window, which is the shape a
    /// single-view head presents.
    /// </remarks>
    public class DialogHostingTests
    {
        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        [DisplayName("Only a native window owner on a windowing platform gets a native dialog; everything else uses the overlay")]
        public void UsesOverlay_OwnerAndPlatform_ReturnsExpected(bool ownerIsWindow, bool singleViewPlatform, bool expected)
        {
            Assert.Equal(expected, DialogHosting.UsesOverlay(ownerIsWindow, singleViewPlatform));
        }

        [Fact]
        [DisplayName("GetWindowOwner returns null when there is no top level, so the caller takes the overlay path")]
        public void GetWindowOwner_NoTopLevel_ReturnsNull()
        {
            Assert.Null(DialogHosting.GetWindowOwner(null));
        }

        [Fact]
        [DisplayName("LookupDialog without an owning window goes to the overlay host instead of creating a Window")]
        public async Task LookupDialog_NoOwningWindow_UsesOverlayHost()
        {
            // A detached host has no top level, the same answer an iOS or Android single view gives to "is there a
            // window". The overlay host then fails for want of an overlay layer. Before the shared decision this path
            // constructed a `Window`, which on the mobile backends throws `NotSupportedException`.
            var host = new Border();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => LookupDialog.ShowAsync(host, "Customer", BuildSchema(), new EmptyConnector()));

            Assert.Contains("OverlayLayer", exception.Message, StringComparison.Ordinal);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Customer", "Customer") { CategoryId = "company" };
            var table = schema.Tables!.Add("Customer", "Customer");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row Id", FieldDbType.Guid));
            table.Fields!.Add(new FormField(SysFields.Id, "Customer Id", FieldDbType.String));
            return schema;
        }

        private sealed class EmptyConnector : FormApiConnector
        {
            public EmptyConnector() : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), "Customer") { }

            public override Task<GetLookupResponse> GetLookupAsync(
                string searchText = "",
                Polhem.Definition.Paging.PagingOptions? paging = null)
                => Task.FromResult(new GetLookupResponse { Table = new DataTable("Customer") });
        }
    }
}
