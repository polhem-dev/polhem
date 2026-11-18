using System.ComponentModel;
using System.Globalization;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Criteria;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's query pane must do on a form page's list: the Query button, the condition rows, the chips
    /// of the conditions in effect and the filter the list reads with. A UI package derives from this class and supplies
    /// how its controls take input; the class names of the pane are common to all of them.
    /// </summary>
    public abstract class QueryPaneBehaviorTests : BunitContext, IAsyncLifetime
    {
        private const string QueryButton = ".polhem-form-page__query";
        private const string Pane = ".polhem-criteria-panel";
        private const string PaneForm = ".polhem-criteria-panel__form";
        private const string Row = ".polhem-criteria-row";
        private const string Message = ".polhem-criteria-panel__message";
        private const string Chip = ".polhem-criteria-chip__open";
        private const string ChipRemove = ".polhem-criteria-chip__remove";
        private const string ChipsClear = ".polhem-criteria-chips__clear";
        private const string Overlay = ".polhem-query-pane--overlay";

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Runs after the backend's services are registered, the first point a test may resolve a service.</summary>
        protected virtual void OnServicesRegistered()
        {
        }

        /// <summary>Gets the form page component.</summary>
        protected abstract Type PageType { get; }

        /// <summary>Gets the query pane component.</summary>
        protected abstract Type PanelType { get; }

        /// <summary>Gets the selector of a list row in the full layout.</summary>
        protected abstract string RowSelector { get; }

        /// <summary>Gets the selector of a card in the compact layout.</summary>
        protected abstract string CardSelector { get; }

        /// <summary>Adds a condition row for the field with <paramref name="caption"/> through "add condition".</summary>
        /// <param name="cut">The rendered pane or page.</param>
        /// <param name="caption">The field's caption.</param>
        protected abstract void AddCondition(IRenderedComponent<ContainerFragment> cut, string caption);

        /// <summary>Types into the first input of a condition row.</summary>
        /// <param name="cut">The rendered pane or page.</param>
        /// <param name="row">The row's index.</param>
        /// <param name="text">The text.</param>
        protected abstract void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text);

        /// <summary>Checks the listed value with <paramref name="text"/> in a choice row.</summary>
        /// <param name="cut">The rendered pane or page.</param>
        /// <param name="row">The row's index.</param>
        /// <param name="text">The value's text.</param>
        protected abstract void CheckChoice(IRenderedComponent<ContainerFragment> cut, int row, string text);

        /// <summary>Picks the period with <paramref name="text"/> from a date range row's period menu.</summary>
        /// <param name="cut">The rendered pane or page.</param>
        /// <param name="row">The row's index.</param>
        /// <param name="text">The period's text.</param>
        protected abstract void ChoosePeriod(IRenderedComponent<ContainerFragment> cut, int row, string text);

        /// <summary>Opens the filter of the list's first column, enters text and applies it.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="text">The text.</param>
        protected abstract void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text);

        /// <summary>Goes to the next page of the list.</summary>
        /// <param name="cut">The rendered page.</param>
        protected abstract void NextPage(IRenderedComponent<ContainerFragment> cut);

        private static string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(null), key);

        private FormPageTestBackend Register()
        {
            var backend = new FormPageTestBackend(queryFields: true);
            backend.Register(Services);
            OnServicesRegistered();
            return backend;
        }

        private IRenderedComponent<ContainerFragment> RenderList(bool compact = false)
        {
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", FormPageTestBackend.EmpProgId);
                builder.AddAttribute(2, "IsCompact", (bool?)compact);
                builder.CloseComponent();
            });
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(compact ? CardSelector : RowSelector).Count));
            return cut;
        }

        private static IRenderedComponent<ContainerFragment> OpenPane(IRenderedComponent<ContainerFragment> cut)
        {
            cut.Find(QueryButton).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Pane)));
            return cut;
        }

        private static void Search(IRenderedComponent<ContainerFragment> cut) => cut.Find(PaneForm).Submit();

        private static List<FilterCondition> Conditions(FilterNode? filter) => filter switch
        {
            FilterCondition condition => [condition],
            FilterGroup group => [.. group.Nodes.SelectMany(Conditions)],
            _ => [],
        };

        [Fact]
        [DisplayName("The list offers Query, which opens the pane beside the list with no conditions")]
        public void Query_Click_OpensPaneWithNoRows()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = RenderList();

            Assert.Equal(Text(PolhemUIText.Query), cut.Find(QueryButton).TextContent.Trim());
            OpenPane(cut);

            Assert.Empty(cut.FindAll(Row));
            Assert.Empty(cut.FindAll(Overlay));
        }

        [Fact]
        [DisplayName("Searching a text condition reads the first page with it, shows a chip and counts it on Query")]
        public void Search_TextCondition_FiltersAndShowsChip()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());
            int reads = backend.GetListCount;

            AddCondition(cut, "Name");
            TypeValue(cut, 0, "Ali");
            Search(cut);

            cut.WaitForAssertion(() => Assert.Equal(reads + 1, backend.GetListCount));
            var condition = Assert.Single(Conditions(backend.LastFilter));
            Assert.Equal("emp_name", condition.FieldName);
            Assert.Equal(ComparisonOperator.Contains, condition.Operator);
            Assert.Contains("Ali", Convert.ToString(condition.Value, CultureInfo.InvariantCulture), StringComparison.Ordinal);
            Assert.Equal(1, backend.ListPaging[^1].Page);
            cut.WaitForAssertion(() => Assert.Equal(
                $"Name {Text(PolhemUIText.CriteriaContains)} Ali", Assert.Single(cut.FindAll(Chip)).TextContent.Trim()));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.QueryWithCount), 1),
                cut.Find(QueryButton).TextContent.Trim());
            // Beside the list, the pane stays open for the next change.
            Assert.Single(cut.FindAll(Pane));
        }

        [Fact]
        [DisplayName("Editing a condition without searching leaves the list's filter as it was when paging")]
        public void Paging_UnsearchedEdit_KeepsSearchedFilter()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            backend.TotalRows = 200;
            var cut = OpenPane(RenderList());
            AddCondition(cut, "Name");
            TypeValue(cut, 0, "Ali");
            Search(cut);
            cut.WaitForAssertion(() => Assert.Single(Conditions(backend.LastFilter)));

            TypeValue(cut, 0, "Bob");
            NextPage(cut);

            cut.WaitForAssertion(() => Assert.Equal(2, backend.ListPaging[^1].Page));
            Assert.Contains("Ali", Convert.ToString(Assert.Single(Conditions(backend.LastFilter)).Value, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Checking listed values sends the field's codes, and the chip names their text")]
        public void Search_ChoiceCondition_SendsCodes()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());

            AddCondition(cut, "Status");
            CheckChoice(cut, 0, "Open");
            Search(cut);

            cut.WaitForAssertion(() => Assert.Single(Conditions(backend.LastFilter)));
            var condition = Assert.Single(Conditions(backend.LastFilter));
            Assert.Equal("status", condition.FieldName);
            Assert.Equal("OPEN", Convert.ToString(condition.Value, CultureInfo.InvariantCulture));
            cut.WaitForAssertion(() => Assert.Contains("Open", Assert.Single(cut.FindAll(Chip)).TextContent, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A date period is sent as the days it covers today, and the chip names the period")]
        public void Search_DatePeriod_SendsResolvedDays()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());

            AddCondition(cut, "Hired");
            ChoosePeriod(cut, 0, Text(PolhemUIText.DateThisMonth));
            Search(cut);

            cut.WaitForAssertion(() => Assert.Equal(2, Conditions(backend.LastFilter).Count));
            var today = DateOnly.FromDateTime(DateTime.Now);
            var first = new DateOnly(today.Year, today.Month, 1);
            var conditions = Conditions(backend.LastFilter);
            Assert.All(conditions, c => Assert.Equal("hire_date", c.FieldName));
            Assert.Equal(ComparisonOperator.GreaterThanOrEqual, conditions[0].Operator);
            Assert.Equal(first, conditions[0].Value);
            Assert.Equal(ComparisonOperator.LessThanOrEqual, conditions[1].Operator);
            Assert.Equal(first.AddMonths(1).AddDays(-1), conditions[1].Value);
            cut.WaitForAssertion(() => Assert.Contains(Text(PolhemUIText.DateThisMonth), Assert.Single(cut.FindAll(Chip)).TextContent,
                StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Removing a chip drops its condition and reads the list again without it")]
        public void RemoveChip_ReadsWithoutCondition()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());
            AddCondition(cut, "Name");
            TypeValue(cut, 0, "Ali");
            Search(cut);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Chip)));
            int reads = backend.GetListCount;

            cut.Find(ChipRemove).Click();

            cut.WaitForAssertion(() => Assert.Equal(reads + 1, backend.GetListCount));
            Assert.Null(backend.LastFilter);
            Assert.Empty(cut.FindAll(Chip));
            Assert.Empty(cut.FindAll(Row));
            Assert.Equal(Text(PolhemUIText.Query), cut.Find(QueryButton).TextContent.Trim());
        }

        [Fact]
        [DisplayName("Clearing the chips removes every condition and reads the whole list")]
        public void ClearChips_ReadsWholeList()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());
            AddCondition(cut, "Name");
            TypeValue(cut, 0, "Ali");
            AddCondition(cut, "Status");
            CheckChoice(cut, 1, "Closed");
            Search(cut);
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(Chip).Count));

            cut.Find(ChipsClear).Click();

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Chip)));
            Assert.Null(backend.LastFilter);
            Assert.Empty(cut.FindAll(Row));
        }

        [Fact]
        [DisplayName("A column filter is a condition: it shows as a chip and as a row of the pane")]
        public void ColumnFilter_ShowsAsChipAndRow()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();

            ApplyFirstColumnFilter(cut, "Ali");

            cut.WaitForAssertion(() => Assert.Equal(
                $"Name {Text(PolhemUIText.CriteriaContains)} Ali", Assert.Single(cut.FindAll(Chip)).TextContent.Trim()));
            Assert.Equal("Ali", Assert.Single(Conditions(backend.LastFilter)).Value as string);
            OpenPane(cut);
            Assert.Single(cut.FindAll(Row));
        }

        [Fact]
        [DisplayName("Setting a column again replaces its condition, and a pane condition on another field adds to it")]
        public void ColumnFilter_ReplacesItsOwnRowAndCombinesWithPane()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            ApplyFirstColumnFilter(cut, "Ali");
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Chip)));

            ApplyFirstColumnFilter(cut, "Bob");
            cut.WaitForAssertion(() => Assert.Equal("Bob", Assert.Single(Conditions(backend.LastFilter)).Value as string));

            OpenPane(cut);
            AddCondition(cut, "Status");
            CheckChoice(cut, 1, "Open");
            Search(cut);

            cut.WaitForAssertion(() => Assert.Equal(2, Conditions(backend.LastFilter).Count));
            Assert.Equal(2, cut.FindAll(Chip).Count);
        }

        [Fact]
        [DisplayName("Removing a column filter's chip reads the list without it")]
        public void ColumnFilter_RemoveChip_ReadsWithout()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = RenderList();
            ApplyFirstColumnFilter(cut, "Ali");
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Chip)));

            cut.Find(ChipRemove).Click();

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Chip)));
            Assert.Null(backend.LastFilter);
        }

        [Fact]
        [DisplayName("Escape closes the pane and keeps what was entered for the next time it opens")]
        public void Escape_ClosesPaneKeepingRows()
        {
            using var culture = new CultureScope("en-US");
            Register();
            var cut = OpenPane(RenderList());
            AddCondition(cut, "Name");

            cut.Find(Pane).KeyDown(new KeyboardEventArgs { Key = "Escape" });

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Pane)));
            OpenPane(cut);
            Assert.Single(cut.FindAll(Row));
        }

        [Fact]
        [DisplayName("In the compact layout the pane covers the page, and a search closes it to show the cards")]
        public void Compact_Search_ClosesOverlay()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList(compact: true));
            Assert.Single(cut.FindAll(Overlay));

            AddCondition(cut, "Name");
            TypeValue(cut, 0, "Ali");
            Search(cut);

            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Pane)));
            Assert.Single(Conditions(backend.LastFilter));
            Assert.Single(cut.FindAll(Chip));
        }

        [Fact]
        [DisplayName("Search with a required row left empty names it and does not ask for the query")]
        public void Search_RequiredEmpty_NamesItAndDoesNotAsk()
        {
            using var culture = new CultureScope("en-US");
            var name = new CriteriaField("emp_name", "Name", CriteriaFieldKind.Text, FieldDbType.String);
            var criteria = new QueryCriteria();
            criteria.Add(name, kind: CriteriaRowKind.Required);
            int searches = 0;
            OnServicesRegistered();
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PanelType);
                builder.AddAttribute(1, "Fields", (IReadOnlyList<CriteriaField>)[name]);
                builder.AddAttribute(2, "Criteria", criteria);
                builder.AddAttribute(3, "SearchRequested", EventCallback.Factory.Create(this, () => searches++));
                builder.CloseComponent();
            });

            Search(cut);

            cut.WaitForAssertion(() => Assert.Contains("Name", cut.Find(Message).TextContent, StringComparison.Ordinal));
            Assert.Equal(0, searches);
            Assert.Empty(cut.FindAll($"{Row} .polhem-criteria-row__remove"));

            TypeValue(cut, 0, "Ali");
            Search(cut);

            cut.WaitForAssertion(() => Assert.Equal(1, searches));
            Assert.Empty(cut.FindAll(Message));
        }
    }
}
