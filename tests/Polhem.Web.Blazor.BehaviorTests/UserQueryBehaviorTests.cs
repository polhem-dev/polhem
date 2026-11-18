using System.ComponentModel;
using System.Globalization;
using Bunit;
using Bunit.Rendering;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Language;
using Polhem.Tests.Shared;
using Polhem.UI.Core.Criteria;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's saved queries must do at the top of a form page's query pane. A UI package derives from
    /// this class and supplies how its picker, menu and dialogs take input.
    /// </summary>
    public abstract class UserQueryBehaviorTests : BunitContext, IAsyncLifetime
    {
        /// <summary>The class of the Save action.</summary>
        protected const string SaveAction = "polhem-user-query-bar__save";

        /// <summary>The class of the Save as action.</summary>
        protected const string SaveAsAction = "polhem-user-query-bar__save-as";

        /// <summary>The class of the Rename action.</summary>
        protected const string RenameAction = "polhem-user-query-bar__rename";

        /// <summary>The class of the share action.</summary>
        protected const string ShareAction = "polhem-user-query-bar__share";

        /// <summary>The class of the default action.</summary>
        protected const string DefaultAction = "polhem-user-query-bar__default";

        /// <summary>The class of the Delete action.</summary>
        protected const string DeleteAction = "polhem-user-query-bar__delete";

        private const string QueryButton = ".polhem-form-page__query";
        private const string Pane = ".polhem-criteria-panel";
        private const string Row = ".polhem-criteria-row";
        private const string Chip = ".polhem-criteria-chip__open";
        private const string Modified = ".polhem-user-query-bar__modified";
        private const string PaneMessage = ".polhem-criteria-panel__message";
        private const string PageNotice = ".polhem-form-page__notice";

        private static readonly Guid s_ownId = new("bbbbbbbb-0000-0000-0000-000000000001");
        private static readonly Guid s_otherId = new("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly CriteriaField s_name = new("emp_name", "Name", CriteriaFieldKind.Text, FieldDbType.String);
        private static readonly string[] s_ownActions = [SaveAction, SaveAsAction, RenameAction, ShareAction, DefaultAction, DeleteAction];

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

        /// <summary>Gets the selector of a list row.</summary>
        protected abstract string RowSelector { get; }

        /// <summary>Picks a saved query in the bar's picker.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="rowId">The query's <c>sys_rowid</c>.</param>
        protected abstract void ChooseQuery(IRenderedComponent<ContainerFragment> cut, Guid rowId);

        /// <summary>Returns the bar's menu actions, by class, and whether each can be chosen.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <returns>Whether each action is enabled, keyed by its class.</returns>
        protected abstract IReadOnlyDictionary<string, bool> ActionStates(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Chooses an action of the bar's menu.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="action">The action's class.</param>
        protected abstract void ChooseAction(IRenderedComponent<ContainerFragment> cut, string action);

        /// <summary>Types a name into the open name prompt, checks its option when asked, and accepts it.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="name">The name.</param>
        /// <param name="checkOption">Whether to check the option under the name.</param>
        protected abstract void AnswerNamePrompt(IRenderedComponent<ContainerFragment> cut, string name, bool checkOption);

        /// <summary>Answers the open confirmation with yes.</summary>
        /// <param name="cut">The rendered page.</param>
        protected abstract void Confirm(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Types into the first value input of a condition row.</summary>
        /// <param name="cut">The rendered page.</param>
        /// <param name="row">The row's index.</param>
        /// <param name="text">The text.</param>
        protected abstract void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text);

        private static string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(null), key);

        private static string NameContains(string text)
        {
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue(text);
            return UserQueryCodec.Serialize(criteria);
        }

        private FormPageTestBackend Register(bool withDefault = false, bool withOther = false)
        {
            var backend = new FormPageTestBackend(queryFields: true);
            backend.UserQueries.Add(new(s_ownId, "Ali", IsShared: false, IsOwn: true, NameContains("Ali")));
            if (withOther) backend.UserQueries.Add(new(s_otherId, "Bob", IsShared: true, IsOwn: false, NameContains("Bob")));
            if (withDefault) backend.DefaultUserQueryRowId = s_ownId;
            backend.Register(Services);
            OnServicesRegistered();
            return backend;
        }

        private IRenderedComponent<ContainerFragment> RenderList()
        {
            var cut = Render(builder =>
            {
                builder.OpenComponent(0, PageType);
                builder.AddAttribute(1, "ProgId", FormPageTestBackend.EmpProgId);
                builder.AddAttribute(2, "IsCompact", (bool?)false);
                builder.CloseComponent();
            });
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(RowSelector).Count));
            return cut;
        }

        private static IRenderedComponent<ContainerFragment> OpenPane(IRenderedComponent<ContainerFragment> cut)
        {
            cut.Find(QueryButton).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Pane)));
            return cut;
        }

        private static FilterCondition? OnlyCondition(FilterNode? filter) => filter as FilterCondition;

        [Fact]
        [DisplayName("The list opens with the user's default query applied, before its first page is read")]
        public void Open_DefaultQuery_ReadsFirstPageWithIt()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withDefault: true);

            var cut = RenderList();

            Assert.Equal(1, backend.GetListCount);
            Assert.Equal("Ali", OnlyCondition(backend.LastFilter)?.Value as string);
            Assert.Single(cut.FindAll(Chip));
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.QueryWithCount), 1),
                cut.Find(QueryButton).TextContent.Trim());
        }

        [Fact]
        [DisplayName("Picking a query applies its conditions and reads the list, and editing them marks it changed")]
        public void Pick_AppliesAndMarksChange()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());
            Assert.Null(backend.LastFilter);

            ChooseQuery(cut, s_ownId);

            cut.WaitForAssertion(() => Assert.Equal("Ali", OnlyCondition(backend.LastFilter)?.Value as string));
            Assert.Single(cut.FindAll(Row));
            Assert.Empty(cut.FindAll(Modified));

            TypeValue(cut, 0, "Al");

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Modified)));
        }

        [Fact]
        [DisplayName("Another user's shared query can be applied and saved under a new name, and made the default, not changed")]
        public void OtherUsersQuery_OffersOnlySaveAsAndDefault()
        {
            using var culture = new CultureScope("en-US");
            Register(withOther: true);
            var cut = OpenPane(RenderList());

            ChooseQuery(cut, s_otherId);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row)));

            var states = ActionStates(cut);
            Assert.True(states[SaveAsAction]);
            Assert.True(states[DefaultAction]);
            Assert.False(states[SaveAction]);
            Assert.False(states[RenameAction]);
            Assert.False(states[ShareAction]);
            Assert.False(states[DeleteAction]);
        }

        [Fact]
        [DisplayName("Save as stores the conditions on screen under the new name, shared when asked, and applies the new query")]
        public void SaveAs_StoresNewSharedQuery()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withOther: true);
            var cut = OpenPane(RenderList());
            ChooseQuery(cut, s_otherId);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row)));

            ChooseAction(cut, SaveAsAction);
            AnswerNamePrompt(cut, "  Mine  ", checkOption: true);

            cut.WaitForAssertion(() => Assert.Single(backend.UserQuerySaves));
            var saved = backend.UserQuerySaves[0];
            Assert.Equal("Mine", saved.Name);
            Assert.True(saved.IsShared);
            Assert.Equal(NameContains("Bob"), saved.Criteria);
            cut.WaitForAssertion(() => Assert.All(s_ownActions, action => Assert.True(ActionStates(cut)[action], action)));
        }

        [Fact]
        [DisplayName("Save overwrites the applied query with the conditions on screen and clears the changed marker")]
        public void Save_OverwritesWithConditionsOnScreen()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withDefault: true);
            var cut = OpenPane(RenderList());
            TypeValue(cut, 0, "Alice");
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Modified)));

            ChooseAction(cut, SaveAction);

            cut.WaitForAssertion(() => Assert.Single(backend.UserQuerySaves));
            Assert.Equal(s_ownId, backend.UserQuerySaves[0].RowId);
            Assert.Equal(NameContains("Alice"), backend.UserQuerySaves[0].Criteria);
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Modified)));
        }

        [Fact]
        [DisplayName("Rename stores the new name with what the query stored, not the conditions on screen")]
        public void Rename_KeepsStoredCriteria()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withDefault: true);
            var cut = OpenPane(RenderList());
            TypeValue(cut, 0, "Alice");

            ChooseAction(cut, RenameAction);
            AnswerNamePrompt(cut, "Renamed", checkOption: false);

            cut.WaitForAssertion(() => Assert.Single(backend.UserQuerySaves));
            Assert.Equal("Renamed", backend.UserQuerySaves[0].Name);
            Assert.Equal(NameContains("Ali"), backend.UserQuerySaves[0].Criteria);
        }

        [Fact]
        [DisplayName("Delete asks first, and going ahead deletes the query and keeps the conditions on screen")]
        public void Delete_AsksThenDeletesKeepingConditions()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withDefault: true);
            var cut = OpenPane(RenderList());

            ChooseAction(cut, DeleteAction);
            Assert.Single(backend.UserQueries);
            Confirm(cut);

            cut.WaitForAssertion(() => Assert.Empty(backend.UserQueries));
            Assert.Single(cut.FindAll(Row));
            Assert.Equal(Guid.Empty, backend.DefaultUserQueryRowId);
        }

        [Fact]
        [DisplayName("The default action makes the applied query the default, and again stops it being so")]
        public void ToggleDefault_SetsAndClears()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var cut = OpenPane(RenderList());
            ChooseQuery(cut, s_ownId);
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row)));

            ChooseAction(cut, DefaultAction);
            cut.WaitForAssertion(() => Assert.Equal(s_ownId, backend.DefaultUserQueryRowId));

            ChooseAction(cut, DefaultAction);
            cut.WaitForAssertion(() => Assert.Equal(Guid.Empty, backend.DefaultUserQueryRowId));
        }

        [Fact]
        [DisplayName("A query whose text cannot be read leaves the conditions as they were and says so")]
        public void Pick_UnreadableQuery_KeepsConditions()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            backend.UserQueries.Add(new(s_otherId, "Broken", IsShared: false, IsOwn: true, "<not a query"));
            var cut = OpenPane(RenderList());
            int reads = backend.GetListCount;

            ChooseQuery(cut, s_otherId);

            cut.WaitForAssertion(() => Assert.Equal(Text(PolhemUIText.UserQueryUnreadable), cut.Find(PaneMessage).TextContent.Trim()));
            Assert.Equal(reads, backend.GetListCount);
            Assert.Empty(cut.FindAll(Row));
        }

        [Fact]
        [DisplayName("Conditions on a field the user can no longer query are dropped and named in the pane")]
        public void Pick_QueryWithUnknownField_NamesSkippedField()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register();
            var gone = new CriteriaField("salary", "Salary", CriteriaFieldKind.Number, FieldDbType.Decimal);
            var criteria = new QueryCriteria();
            criteria.Add(s_name).SetValue("Ali");
            criteria.Add(gone).SetRange(100m, null);
            backend.UserQueries.Add(new(s_otherId, "Old", IsShared: false, IsOwn: true, UserQueryCodec.Serialize(criteria)));
            var cut = OpenPane(RenderList());

            ChooseQuery(cut, s_otherId);

            cut.WaitForAssertion(() => Assert.Contains("salary", cut.Find(PaneMessage).TextContent, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Ali", OnlyCondition(backend.LastFilter)?.Value as string);
            Assert.Single(cut.FindAll(Row));
        }

        [Fact]
        [DisplayName("Saved queries that cannot be read are a notice, and the list opens without them")]
        public void Open_QueriesFail_NoticeAndListOpens()
        {
            using var culture = new CultureScope("en-US");
            var backend = Register(withDefault: true);
            backend.UserQueryFailure = "Queries are down.";

            var cut = RenderList();

            Assert.Contains("Queries are down.", cut.Find(PageNotice).TextContent, StringComparison.Ordinal);
            Assert.Null(backend.LastFilter);
            Assert.NotNull(cut.Find(QueryButton));
        }
    }
}
