using System.ComponentModel;
using System.Data;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Bunit;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Render tests: they run BuildRenderTree of DynamicGrid.razor to cover the lines of the .razor template.
    /// </summary>
    public class DynamicGridRenderTests : BunitContext
    {
        [Fact]
        [DisplayName("DynamicGrid renders the empty-state div with the default text when Layout is null")]
        public void DynamicGrid_NullLayout_RendersEmptyDiv()
        {
            using var culture = new CultureScope("en-US");
            var cut = Render<DynamicGrid>();
            var emptyDiv = cut.Find("div.polhem-dynamic-grid--empty");
            Assert.Contains("No data.", emptyDiv.TextContent);
        }

        [Fact]
        [DisplayName("DynamicGrid renders the empty-state text in the UI culture, and a host's EmptyText wins")]
        public void DynamicGrid_NullLayoutUnderZhTw_RendersLocalizedEmptyText()
        {
            using var culture = new CultureScope("zh-TW");
            var localized = Render<DynamicGrid>();
            var overridden = Render<DynamicGrid>(p => p.Add(g => g.EmptyText, "Nothing here"));

            Assert.Contains("沒有資料。", localized.Find("div.polhem-dynamic-grid--empty").TextContent);
            Assert.Contains("Nothing here", overridden.Find("div.polhem-dynamic-grid--empty").TextContent);
        }

        [Fact]
        [DisplayName("DynamicGrid renders a table when given a Layout and Rows with data")]
        public void DynamicGrid_WithLayoutAndRows_RendersTable()
        {
            var layout = new LayoutGrid();
            layout.Columns!.Add(new LayoutColumn { FieldName = "name", Caption = "Name", Visible = true });

            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            var row = table.NewRow();
            row["name"] = "Alice";
            table.Rows.Add(row);

            var cut = Render<DynamicGrid>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.Rows, table));

            Assert.NotNull(cut.Find("table.polhem-dynamic-grid"));
        }
    }
}
