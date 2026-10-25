using System.ComponentModel;
using System.Data;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Contracts.Form;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="LookupDialog"/>.
    /// </summary>
    public class LookupDialogTests : BunitContext
    {
        [Fact]
        [DisplayName("With a schema and a connector the dialog shows the panel titled with the schema's name")]
        public void Render_ExplicitSchemaAndConnector_ShowsPanel()
        {
            var cut = Render<LookupDialog>(p => p
                .Add(c => c.ProgId, "Dept")
                .Add(c => c.Schema, LookupTestData.BuildTargetSchema())
                .Add(c => c.Connector, new FakeLookupConnector()));

            Assert.Equal("Department", cut.Find(".polhem-lookup-dialog__title").TextContent);
            Assert.Equal("true", cut.Find(".polhem-lookup-dialog").GetAttribute("aria-modal"));
            Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count);
        }

        [Fact]
        [DisplayName("A committed row and a click outside the card reach the host")]
        public void Commit_AndBackdropClick_ReachHost()
        {
            DataRow? committed = null;
            var cancelled = false;
            var cut = Render<LookupDialog>(p => p
                .Add(c => c.ProgId, "Dept")
                .Add(c => c.Schema, LookupTestData.BuildTargetSchema())
                .Add(c => c.Connector, new FakeLookupConnector())
                .Add(c => c.OnCommitted, row => committed = row)
                .Add(c => c.OnCancelled, () => cancelled = true));

            cut.FindAll("tr.polhem-dynamic-grid__row")[0].DoubleClick();
            cut.Find(".polhem-lookup-dialog__backdrop").Click();

            Assert.NotNull(committed);
            Assert.True(cancelled);
        }

        [Fact]
        [DisplayName("Registered definition source and connector factory supply the dialog")]
        public void Render_RegisteredSource_SuppliesDialog()
        {
            var source = new FakeLookupSource();
            Services.AddScoped<IFormDefinitionSource>(_ => source);
            Services.AddScoped<IFormApiConnectorFactory>(_ => source);

            var cut = Render<LookupDialog>(p => p.Add(c => c.ProgId, "Dept"));

            Assert.Equal("Department", cut.Find(".polhem-lookup-dialog__title").TextContent);
            Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count);
        }

        [Fact]
        [DisplayName("Without any source the dialog shows why and reports the failure")]
        public void Render_NoClient_ShowsAndReportsFailure()
        {
            Exception? failure = null;

            var cut = Render<LookupDialog>(p => p
                .Add(c => c.ProgId, "Dept")
                .Add(c => c.OnFailed, ex => failure = ex));

            Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains("No source for lookup 'Dept'", cut.Find(".polhem-lookup-dialog__error").TextContent, StringComparison.Ordinal);
        }
    }
}
