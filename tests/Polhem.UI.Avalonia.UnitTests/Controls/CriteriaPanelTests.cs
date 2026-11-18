using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    public class CriteriaPanelTests
    {
        private static readonly CriteriaField s_name = new("sys_name", "Name", CriteriaFieldKind.Text, FieldDbType.String);
        private static readonly CriteriaField s_date = new("order_date", "Date", CriteriaFieldKind.Date, FieldDbType.Date);
        private static readonly CriteriaField s_active = new("is_active", "Active", CriteriaFieldKind.Boolean, FieldDbType.Boolean);

        private static CriteriaPanel BoundPanel(QueryCriteria? criteria = null)
        {
            var panel = new CriteriaPanel();
            panel.Bind([s_name, s_date, s_active], criteria ?? new QueryCriteria());
            return panel;
        }

        private static T Single<T>(CriteriaPanel panel) where T : Control
            => Assert.Single(panel.GetLogicalDescendants().OfType<T>());

        private static Border[] RowBlocks(CriteriaPanel panel)
            => panel.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains(CriteriaPanel.RowClass)).ToArray();

        [Fact]
        [DisplayName("Each condition row sits in its own framed block, and removing the row removes its block")]
        public void Rows_EachInOwnBlock()
        {
            var panel = BoundPanel();
            panel.AddRow(s_name);
            panel.AddRow(s_active);
            var blocks = RowBlocks(panel);
            var remove = blocks[0].GetLogicalDescendants().OfType<Button>().Single();

            Assert.Equal(2, blocks.Length);
            remove.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.Single(RowBlocks(panel));
        }

        [Fact]
        [DisplayName("Typing in a text row writes the trimmed text to its condition")]
        public void TextEditor_WritesValue()
        {
            var panel = BoundPanel();
            var row = panel.AddRow(s_name);

            Single<TextBox>(panel).Text = " Lee ";

            Assert.Equal("Lee", row.Value);
        }

        [Fact]
        [DisplayName("Choosing another operator writes it to the row and swaps the value editor")]
        public void OperatorBox_ChangesOperatorAndEditor()
        {
            var panel = BoundPanel();
            var row = panel.AddRow(s_date);
            var operatorBox = panel.GetLogicalDescendants().OfType<ComboBox>().First();

            operatorBox.SelectedIndex = 1;

            Assert.Equal(CriteriaOperator.Equal, row.Operator);
            Assert.Single(panel.GetLogicalDescendants().OfType<CalendarDatePicker>());
        }

        [Fact]
        [DisplayName("Picking both days of a date range writes the range")]
        public void DateRange_WritesBothEnds()
        {
            var panel = BoundPanel();
            var row = panel.AddRow(s_date);
            var pickers = panel.GetLogicalDescendants().OfType<CalendarDatePicker>().ToArray();

            pickers[0].SelectedDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
            pickers[1].SelectedDate = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 17)), ((DateOnly?)row.From, (DateOnly?)row.To));
        }

        [Fact]
        [DisplayName("A yes / no row starts at Any and writes the chosen answer")]
        public void BooleanEditor_WritesAnswer()
        {
            var panel = BoundPanel();
            var row = panel.AddRow(s_active);
            var box = Single<ComboBox>(panel);

            Assert.Equal(0, box.SelectedIndex);
            box.SelectedIndex = 2;

            Assert.Equal(false, row.Value);
        }

        [Fact]
        [DisplayName("Ticking a listed value adds it to the row and unticking removes it")]
        public void ChoiceEditor_TicksValues()
        {
            var status = new CriteriaField("status", "Status", CriteriaFieldKind.Choice, FieldDbType.String)
            {
                ListItems = [new ListItem("A", "Active"), new ListItem("C", "Closed")],
            };
            var panel = new CriteriaPanel();
            panel.Bind([status], new QueryCriteria());
            var row = panel.AddRow(status);
            var boxes = panel.GetLogicalDescendants().OfType<CheckBox>().ToArray();

            boxes[1].IsChecked = true;
            Assert.Equal([new CriteriaValue("C", "Closed")], row.Values);

            boxes[1].IsChecked = false;
            Assert.Empty(row.Values);
        }

        [Fact]
        [DisplayName("A query with an empty required row is not run and names the row")]
        public void RequestSearch_MissingRequired_ShowsMessage()
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_date, kind: CriteriaRowKind.Required);
            var panel = BoundPanel(criteria);
            var searches = 0;
            panel.SearchRequested += (_, _) => searches++;

            panel.RequestSearch();

            Assert.Equal(0, searches);
            Assert.Contains("Date", panel.MessageText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Clearing removes the optional rows and runs the query")]
        public void ClearAndSearch_RemovesOptionalRowsAndSearches()
        {
            var panel = BoundPanel();
            panel.AddRow(s_name).SetValue("Lee");
            var searches = 0;
            panel.SearchRequested += (_, _) => searches++;

            panel.ClearAndSearch();

            Assert.Empty(panel.Criteria.Rows);
            Assert.Empty(panel.GetLogicalDescendants().OfType<TextBox>());
            Assert.Equal(1, searches);
        }

        [Fact]
        [DisplayName("Refresh shows the rows again after the host removed one outside the panel")]
        public void Refresh_AfterExternalRemoval_DropsRow()
        {
            var panel = BoundPanel();
            var row = panel.AddRow(s_name);
            panel.Criteria.Remove(row);

            panel.Refresh();

            Assert.Empty(panel.GetLogicalDescendants().OfType<TextBox>());
        }

        [Fact]
        [DisplayName("The close button raises CloseRequested")]
        public void CloseButton_RaisesCloseRequested()
        {
            var panel = BoundPanel();
            var closed = 0;
            panel.CloseRequested += (_, _) => closed++;
            var close = panel.GetLogicalDescendants().OfType<Button>()
                .First(b => global::Avalonia.Automation.AutomationProperties.GetName(b) == UIText.Get(Polhem.Definition.Language.PolhemUIText.Close));

            close.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, closed);
        }
    }
}
