using System.ComponentModel;
using System.Data;
using Bunit;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="LookupPanel"/>, aligned with the Avalonia head's <c>LookupPanel</c>.
    /// </summary>
    public class LookupPanelTests : BunitContext
    {
        private IRenderedComponent<LookupPanel> RenderPanel(FakeLookupConnector connector, Action<DataRow>? committed = null, Action? cancelled = null)
            => Render<LookupPanel>(p => p
                .Add(c => c.Schema, LookupTestData.BuildTargetSchema())
                .Add(c => c.Connector, connector)
                .Add(c => c.OnCommitted, row => committed?.Invoke(row))
                .Add(c => c.OnCancelled, () => cancelled?.Invoke()));

        [Fact]
        [DisplayName("The panel loads the lookup rows when it receives a connector")]
        public void Render_LoadsRows()
        {
            var connector = new FakeLookupConnector();

            var cut = RenderPanel(connector);

            Assert.Equal([string.Empty], connector.Searches);
            Assert.Equal(2, cut.FindAll("tr.polhem-dynamic-grid__row").Count);
        }

        [Fact]
        [DisplayName("Submitting the search reloads the rows with the search text")]
        public void Search_SubmitsSearchText()
        {
            var connector = new FakeLookupConnector();
            var cut = RenderPanel(connector);

            cut.Find("input.polhem-lookup-panel__search-input").Change("sal");
            cut.Find("form.polhem-lookup-panel__search").Submit();

            Assert.Equal("sal", connector.Searches[^1]);
            Assert.Single(cut.FindAll("tr.polhem-dynamic-grid__row"));
        }

        [Fact]
        [DisplayName("OK stays disabled until a row is selected, then commits that row")]
        public void Ok_CommitsSelectedRow()
        {
            DataRow? committed = null;
            var cut = RenderPanel(new FakeLookupConnector(), row => committed = row);
            Assert.True(cut.Find("button.polhem-lookup-panel__ok").HasAttribute("disabled"));

            cut.FindAll("tr.polhem-dynamic-grid__row")[1].Click();
            Assert.Contains("polhem-dynamic-grid__row--selected", cut.FindAll("tr.polhem-dynamic-grid__row")[1].ClassList);
            cut.Find("button.polhem-lookup-panel__ok").Click();

            Assert.NotNull(committed);
            Assert.Equal(LookupTestData.SalesId, (Guid)committed!["sys_rowid"]);
        }

        [Fact]
        [DisplayName("A double-click commits the row at once")]
        public void DoubleClick_CommitsRow()
        {
            DataRow? committed = null;
            var cut = RenderPanel(new FakeLookupConnector(), row => committed = row);

            cut.FindAll("tr.polhem-dynamic-grid__row")[0].DoubleClick();

            Assert.Equal(LookupTestData.EngineeringId, (Guid)committed!["sys_rowid"]);
        }

        [Fact]
        [DisplayName("Cancel raises OnCancelled")]
        public void Cancel_RaisesCancelled()
        {
            var cancelled = false;
            var cut = RenderPanel(new FakeLookupConnector(), cancelled: () => cancelled = true);

            cut.Find("button.polhem-lookup-panel__cancel").Click();

            Assert.True(cancelled);
        }

        [Fact]
        [DisplayName("A failed load is shown on the panel instead of breaking it")]
        public void Render_LoadFailure_ShowsError()
        {
            var connector = new FakeLookupConnector { Failure = new InvalidOperationException("backend down") };

            var cut = RenderPanel(connector);

            Assert.Equal("backend down", cut.Find(".polhem-lookup-panel__error").TextContent);
        }
    }
}
