using System.ComponentModel;
using Bunit;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="RowEditPanel"/>: the buffered edit session it runs over one row.
    /// </summary>
    public class RowEditPanelTests : BunitContext
    {
        private IRenderedComponent<RowEditPanel> RenderPanel(FormDataObject dataObject, Action? committed = null, Action? cancelled = null)
        {
            var schema = OrderTestData.BuildSchema();
            return Render<RowEditPanel>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.Layout, OrderTestData.LinesLayout(schema))
                .Add(c => c.Row, OrderTestData.Line(dataObject, 0))
                .Add(c => c.EditCommitted, () => committed?.Invoke())
                .Add(c => c.EditCancelled, () => cancelled?.Invoke()));
        }

        private static FormDataObject BuildAcceptedOrder()
        {
            var dataObject = OrderTestData.BuildOrder();
            dataObject.DataSet.AcceptChanges();
            return dataObject;
        }

        [Fact]
        [DisplayName("Edits stay proposed until OK, which commits them and reports EditCommitted")]
        public void Ok_CommitsSession()
        {
            var dataObject = BuildAcceptedOrder();
            var published = new List<string>();
            dataObject.FieldValueChanged += (_, e) => published.Add(e.FieldName);
            var committed = false;
            var cut = RenderPanel(dataObject, committed: () => committed = true);

            cut.Find("#polhem-row-edit-item").Change("Washer");
            Assert.Empty(published);
            cut.Find(".polhem-row-edit-panel__ok").Click();

            Assert.True(committed);
            Assert.Contains("item", published);
            Assert.Equal("Washer", dataObject.GetField(OrderTestData.Line(dataObject, 0), "item"));
        }

        [Fact]
        [DisplayName("Cancel restores the row and reports EditCancelled")]
        public void Cancel_RestoresRow()
        {
            var dataObject = BuildAcceptedOrder();
            var cancelled = false;
            var cut = RenderPanel(dataObject, cancelled: () => cancelled = true);

            cut.Find("#polhem-row-edit-item").Change("Washer");
            cut.Find(".polhem-row-edit-panel__cancel").Click();

            Assert.True(cancelled);
            Assert.Equal("Bolt", dataObject.GetField(OrderTestData.Line(dataObject, 0), "item"));
        }

        [Fact]
        [DisplayName("A panel removed mid-session cancels the edit")]
        public async Task Dispose_CancelsSession()
        {
            var dataObject = BuildAcceptedOrder();
            var cut = RenderPanel(dataObject);
            cut.Find("#polhem-row-edit-item").Change("Washer");

            await DisposeComponentsAsync();

            Assert.Equal("Bolt", dataObject.GetField(OrderTestData.Line(dataObject, 0), "item"));
            Assert.False(OrderTestData.Line(dataObject, 0).HasVersion(System.Data.DataRowVersion.Proposed));
        }

        [Fact]
        [DisplayName("An editor in the session refreshes when something else writes the row, such as a recompute")]
        public void SessionWrite_RefreshesEditor()
        {
            var dataObject = BuildAcceptedOrder();
            var cut = RenderPanel(dataObject);

            cut.InvokeAsync(() => dataObject.SetField(OrderTestData.Line(dataObject, 0), "amount", "42"));

            Assert.StartsWith("42", cut.Find("#polhem-row-edit-amount").GetAttribute("value"), StringComparison.Ordinal);
        }
    }
}
