using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.DataObjects;
using Polhem.Web.Blazor.Fluent.Controls.Editors;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// A read-only check box must not reach the field, whatever change its element reports.
    /// </summary>
    public class FluentCheckEditTests : BunitContext, IAsyncLifetime
    {
        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        [Fact]
        [DisplayName("A change reported by a read-only check box does not reach the field")]
        public void Change_ReadOnly_KeepsTheField()
        {
            Services.AddPolhemFluent();
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

            cut.Find("fluent-checkbox").Change(false);

            Assert.Equal(bool.TrueString, dataObject.GetField("active"));
        }
    }
}
