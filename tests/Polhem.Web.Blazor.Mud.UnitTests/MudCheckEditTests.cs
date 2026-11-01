using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.DataObjects;
using Polhem.Web.Blazor.Mud.Controls.Editors;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// A read-only MudCheckBox blocks the click in the browser, but its input still reports a change event.
    /// </summary>
    public class MudCheckEditTests : BunitContext, IAsyncLifetime
    {
        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        [Fact]
        [DisplayName("A change reported by a read-only check box does not reach the field")]
        public void Change_ReadOnly_KeepsTheField()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            var schema = new FormSchema("P", "P");
            schema.Tables!.Add("P", "P").Fields!.Add("active", "Active", FieldDbType.Boolean);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField("active", "True");
            var cut = Render<CheckEdit>(p => p
                .Add(c => c.FieldName, "active")
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.LayoutField, new LayoutField { FieldName = "active", ReadOnly = true }));

            cut.Find("input").Change(false);

            Assert.Equal(bool.TrueString, dataObject.GetField("active"));
        }
    }
}
