using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
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

            var cells = cut.FindAll("td.polhem-dynamic-grid__cell");
            Assert.Equal("Alice", Assert.Single(cells).TextContent);
            Assert.Contains("Name", cut.Find("th.polhem-dynamic-grid__header").TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("DynamicGrid shows a drop-down column's list item text when given the schema table")]
        public void DynamicGrid_DropDownColumnWithFormTable_RendersListItemText()
        {
            var formTable = new FormTable("AuditRule", "Audit Rule");
            var mode = formTable.Fields!.Add("change_mode", "Change Log", FieldDbType.Integer);
            mode.ListItems!.Add("0", "Inherit");
            mode.ListItems.Add("1", "On");
            var layout = new LayoutGrid();
            layout.Columns!.Add(new LayoutColumn("change_mode", "Change Log", ControlType.DropDownEdit));
            var table = new DataTable();
            table.Columns.Add("change_mode", typeof(int));
            table.Rows.Add(1);

            var withTable = Render<DynamicGrid>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.Rows, table)
                .Add(c => c.FormTable, formTable));
            var withoutTable = Render<DynamicGrid>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.Rows, table));

            Assert.Equal("On", withTable.Find("td.polhem-dynamic-grid__cell").TextContent);
            Assert.Equal("1", withoutTable.Find("td.polhem-dynamic-grid__cell").TextContent);
        }

        [Fact]
        [DisplayName("DynamicGrid shows the time of a DateTimeEdit column even at midnight")]
        public void DynamicGrid_DateTimeColumnAtMidnight_RendersTime()
        {
            using var culture = new CultureScope("en-US");
            var midnight = new DateTime(2026, 9, 28, 0, 0, 0);
            var layout = new LayoutGrid();
            layout.Columns!.Add(new LayoutColumn("created_at", "Created At", ControlType.DateTimeEdit));
            var table = new DataTable();
            table.Columns.Add("created_at", typeof(DateTime));
            table.Rows.Add(midnight);

            var cut = Render<DynamicGrid>(p => p
                .Add(c => c.Layout, layout)
                .Add(c => c.Rows, table));

            Assert.Equal(midnight.ToString("G", System.Globalization.CultureInfo.CurrentCulture),
                cut.Find("td.polhem-dynamic-grid__cell").TextContent);
        }
    }
}
