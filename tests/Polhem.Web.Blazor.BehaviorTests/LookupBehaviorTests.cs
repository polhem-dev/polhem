using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Definition.Layouts;
using Polhem.Api.Contracts.Form;
using Polhem.UI.Core.Definitions;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.Web.Blazor.DataObjects;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's lookup editor and picker must do. A UI package derives from this class and supplies how
    /// to find and operate its controls. A picker may render outside the editor's markup, so the driver finds it itself.
    /// </summary>
    public abstract class LookupBehaviorTests : BunitContext, IAsyncLifetime
    {
        private readonly FakeLookupSource _source = new();

        /// <summary>
        /// Initializes a new instance of <see cref="LookupBehaviorTests"/>.
        /// </summary>
        protected LookupBehaviorTests()
        {
            Services.AddScoped<IFormDefinitionSource>(_ => _source);
            Services.AddScoped<IFormApiConnectorFactory>(_ => _source);
        }

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Gets the lookup editor component.</summary>
        protected abstract Type EditorType { get; }

        /// <summary>Reads the text the editor shows.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>The text.</returns>
        protected abstract string ReadText(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Determines whether the user can type in the input.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>Whether it accepts typing.</returns>
        protected abstract bool AcceptsTyping(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Determines whether the lookup button shows.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>Whether it shows.</returns>
        protected abstract bool ShowsButton(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks the lookup button.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void ClickButton(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Presses a key on the input.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="key">The key.</param>
        protected abstract void PressKey(IRenderedComponent<ContainerFragment> cut, string key);

        /// <summary>Double-clicks the editor.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void DoubleClickEditor(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Determines whether the picker is open.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>Whether it is open.</returns>
        protected abstract bool IsPickerOpen(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Counts the picker's rows.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>The row count.</returns>
        protected abstract int PickerRowCount(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks a picker row.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="row">The 0-based row.</param>
        protected abstract void ClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Double-clicks a picker row.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="row">The 0-based row.</param>
        protected abstract void DoubleClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row);

        /// <summary>Determines whether the picker's OK can be clicked.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>Whether it is enabled.</returns>
        protected abstract bool CanConfirmPicker(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks the picker's OK.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void ConfirmPicker(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Clicks the picker's Cancel.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void CancelPicker(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Enters a search and submits it.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="text">The search text.</param>
        protected abstract void SearchPicker(IRenderedComponent<ContainerFragment> cut, string text);

        private IRenderedComponent<ContainerFragment> RenderEditor(FormDataObject dataObject, string fieldName = LookupTestData.LookupField,
            SingleFormMode mode = SingleFormMode.Edit, Action<RelationTarget>? openRelation = null, Action? buttonClick = null)
        {
            var type = EditorType;
            RenderFragment editor = b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(FieldEditorBase.FieldName), fieldName);
                if (buttonClick is not null)
                    b.AddAttribute(2, nameof(ButtonEditorBase.ButtonClick), EventCallback.Factory.Create(this, buttonClick));
                b.CloseComponent();
            };
            return Render(b =>
            {
                b.OpenComponent<FormScope>(0);
                b.AddAttribute(1, nameof(FormScope.DataObject), dataObject);
                b.AddAttribute(2, nameof(FormScope.FormMode), mode);
                if (openRelation is not null)
                    b.AddAttribute(3, nameof(FormScope.OpenRelation), EventCallback.Factory.Create(this, openRelation));
                b.AddAttribute(4, nameof(FormScope.ChildContent), editor);
                b.CloseComponent();
            });
        }

        private static FormDataObject BuildEmployeeWithSelection()
        {
            var dataObject = LookupTestData.BuildEmployee();
            dataObject.SetField(LookupTestData.LookupField, LookupTestData.EngineeringId.ToString());
            dataObject.SetField("ref_dept_id", "D01");
            dataObject.SetField("ref_dept_name", "Engineering");
            return dataObject;
        }

        [Fact]
        [DisplayName("A lookup editor shows its display fields and refuses typing")]
        public void Render_ShowsDisplayFieldsWithoutTyping()
        {
            var cut = RenderEditor(BuildEmployeeWithSelection());

            Assert.Equal("D01 - Engineering", ReadText(cut));
            Assert.False(AcceptsTyping(cut));
        }

        [Fact]
        [DisplayName("The button opens the picker over the target's rows, and OK waits for a selection")]
        public void Button_OpensThePicker()
        {
            var cut = RenderEditor(LookupTestData.BuildEmployee());
            Assert.False(IsPickerOpen(cut));

            ClickButton(cut);

            cut.WaitForAssertion(() => Assert.Equal(2, PickerRowCount(cut)));
            Assert.False(CanConfirmPicker(cut));
        }

        [Fact]
        [DisplayName("OK writes the row id and the mapped fields, closes the picker and shows the new value")]
        public void Confirm_WritesTheSelection()
        {
            var dataObject = LookupTestData.BuildEmployee();
            var cut = RenderEditor(dataObject);
            ClickButton(cut);
            cut.WaitForAssertion(() => Assert.Equal(2, PickerRowCount(cut)));

            ClickPickerRow(cut, 1);
            ConfirmPicker(cut);

            cut.WaitForAssertion(() => Assert.False(IsPickerOpen(cut)));
            Assert.Equal("Sales", dataObject.GetField("ref_dept_name"));
            Assert.Equal(LookupTestData.SalesId, Guid.Parse(dataObject.GetField(LookupTestData.LookupField)));
            cut.WaitForAssertion(() => Assert.Equal("D02 - Sales", ReadText(cut)));
        }

        [Fact]
        [DisplayName("A double-click on a row commits it")]
        public void DoubleClick_CommitsTheRow()
        {
            var dataObject = LookupTestData.BuildEmployee();
            var cut = RenderEditor(dataObject);
            ClickButton(cut);
            cut.WaitForAssertion(() => Assert.Equal(2, PickerRowCount(cut)));

            DoubleClickPickerRow(cut, 0);

            cut.WaitForAssertion(() => Assert.False(IsPickerOpen(cut)));
            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));
        }

        [Fact]
        [DisplayName("Cancel closes the picker and writes nothing")]
        public void Cancel_WritesNothing()
        {
            var dataObject = BuildEmployeeWithSelection();
            var cut = RenderEditor(dataObject);
            ClickButton(cut);
            cut.WaitForAssertion(() => Assert.Equal(2, PickerRowCount(cut)));

            ClickPickerRow(cut, 1);
            CancelPicker(cut);

            cut.WaitForAssertion(() => Assert.False(IsPickerOpen(cut)));
            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));
        }

        [Fact]
        [DisplayName("A search reads the matching rows from the server")]
        public void Search_ReadsMatchingRows()
        {
            var cut = RenderEditor(LookupTestData.BuildEmployee());
            ClickButton(cut);
            cut.WaitForAssertion(() => Assert.Equal(2, PickerRowCount(cut)));

            SearchPicker(cut, "sal");

            cut.WaitForAssertion(() => Assert.Equal(1, PickerRowCount(cut)));
            Assert.Equal("sal", _source.Connector.Searches[^1]);
        }

        [Theory]
        [InlineData("Delete")]
        [InlineData("Backspace")]
        [DisplayName("Delete or Backspace clears the selection and the mapped fields")]
        public void ClearKey_ClearsTheSelection(string key)
        {
            var dataObject = BuildEmployeeWithSelection();
            var cut = RenderEditor(dataObject);

            PressKey(cut, key);

            Assert.Equal(string.Empty, dataObject.GetField("ref_dept_name"));
            cut.WaitForAssertion(() => Assert.Equal(string.Empty, ReadText(cut)));
        }

        [Fact]
        [DisplayName("A read-only form offers no button, and a clear key changes nothing")]
        public void View_OffersNoEditing()
        {
            var dataObject = BuildEmployeeWithSelection();
            var cut = RenderEditor(dataObject, mode: SingleFormMode.View);

            Assert.False(ShowsButton(cut));
            PressKey(cut, "Delete");

            Assert.Equal("Engineering", dataObject.GetField("ref_dept_name"));
        }

        [Fact]
        [DisplayName("On a read-only form a double-click opens the record the field points at")]
        public void View_DoubleClick_OpensTheRecord()
        {
            var opened = new List<RelationTarget>();
            var cut = RenderEditor(BuildEmployeeWithSelection(), mode: SingleFormMode.View, openRelation: opened.Add);

            DoubleClickEditor(cut);

            var target = Assert.Single(opened);
            Assert.Equal("Dept", target.ProgId);
            Assert.Equal(LookupTestData.EngineeringId, target.RowId);
        }

        [Fact]
        [DisplayName("Without relation metadata the button only raises ButtonClick and the text can be typed")]
        public void PlainField_RaisesButtonClick()
        {
            var clicks = 0;
            var dataObject = LookupTestData.BuildEmployee();
            var cut = RenderEditor(dataObject, "ref_dept_name", buttonClick: () => clicks++);

            ClickButton(cut);

            Assert.Equal(1, clicks);
            Assert.True(AcceptsTyping(cut));
            Assert.False(IsPickerOpen(cut));
        }

        private IRenderedComponent<ContainerFragment> OpenPicker()
        {
            var cut = RenderEditor(LookupTestData.BuildEmployee());
            ClickButton(cut);
            cut.WaitForAssertion(() => Assert.True(IsPickerOpen(cut)));
            return cut;
        }

        /// <summary>Reads what the picker's message area shows, which may be outside the editor's markup.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="selector">The selector.</param>
        /// <returns>The texts.</returns>
        protected abstract IReadOnlyList<string> PickerText(IRenderedComponent<ContainerFragment> cut, string selector);

        [Fact]
        [DisplayName("A response with more candidates shows the notice, and a narrower search clears it")]
        public void Picker_HasMore_ShowsNotice()
        {
            using var culture = new Polhem.Tests.Shared.CultureScope("en-US");
            _source.Connector.Paging = new Polhem.Definition.Paging.PagingInfo { PageSize = 100, HasMore = true };
            var cut = OpenPicker();
            cut.WaitForAssertion(() => Assert.Contains("first 100 rows", Assert.Single(PickerText(cut, ".polhem-lookup-panel__notice")), StringComparison.Ordinal));

            _source.Connector.Paging = new Polhem.Definition.Paging.PagingInfo { PageSize = 100, HasMore = false };
            SearchPicker(cut, "sal");

            cut.WaitForAssertion(() => Assert.Empty(PickerText(cut, ".polhem-lookup-panel__notice")));
        }

        [Fact]
        [DisplayName("A failed load shows on the picker instead of breaking it")]
        public void Picker_LoadFails_ShowsError()
        {
            _source.Connector.Failure = new InvalidOperationException("Lookup is down");

            var cut = OpenPicker();

            cut.WaitForAssertion(() => Assert.Contains("Lookup is down", Assert.Single(PickerText(cut, ".polhem-lookup-panel__error")), StringComparison.Ordinal));
        }
    }
}
