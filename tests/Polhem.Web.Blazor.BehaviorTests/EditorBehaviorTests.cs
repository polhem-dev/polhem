using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.Web.Blazor.DataObjects;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's field editors must do. A UI package derives from this class and supplies how to find and
    /// operate its controls.
    /// </summary>
    public abstract class EditorBehaviorTests : BunitContext, IAsyncLifetime
    {
        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        // xUnit only calls the synchronous Dispose otherwise, and a UI framework's services can be async-only.
        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        private const string FieldName = "value";

        /// <summary>Gets the editor component of <paramref name="controlType"/>.</summary>
        /// <param name="controlType">The control type.</param>
        /// <returns>The component type.</returns>
        protected abstract Type EditorType(ControlType controlType);

        /// <summary>Reads the value the editor shows to the user.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="controlType">The control type.</param>
        /// <returns>The shown text.</returns>
        protected abstract string ReadText(IRenderedComponent<ContainerFragment> cut, ControlType controlType);

        /// <summary>Enters <paramref name="text"/> and finishes the value, as a user leaving the input does.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="controlType">The control type.</param>
        /// <param name="text">The text, or the invariant value for an editor without free text.</param>
        protected abstract void Enter(IRenderedComponent<ContainerFragment> cut, ControlType controlType, string text);

        /// <summary>Determines whether the user can change the value.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="controlType">The control type.</param>
        /// <returns>Whether the editor accepts input.</returns>
        protected abstract bool AcceptsInput(IRenderedComponent<ContainerFragment> cut, ControlType controlType);

        /// <summary>Gives the numeric editor the focus.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void FocusNumeric(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Takes the focus from the numeric editor.</summary>
        /// <param name="cut">The rendered editor.</param>
        protected abstract void BlurNumeric(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Reads the state of the check box.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <returns>Whether it is checked.</returns>
        protected abstract bool IsChecked(IRenderedComponent<ContainerFragment> cut);

        /// <summary>Sets the state of the check box as a user does.</summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="value">The new state.</param>
        protected abstract void SetChecked(IRenderedComponent<ContainerFragment> cut, bool value);

        /// <summary>
        /// Determines whether the editor has the read-only look: only an underline, as the Avalonia controls draw a
        /// read-only field.
        /// </summary>
        /// <param name="cut">The rendered editor.</param>
        /// <param name="controlType">The control type.</param>
        /// <returns>Whether every input of the editor has the read-only look.</returns>
        protected abstract bool HasReadOnlyLook(IRenderedComponent<ContainerFragment> cut, ControlType controlType);

        private static FormDataObject BuildDataObject(FieldDbType dbType, string? value, Action<FormField>? configure = null)
        {
            var schema = new FormSchema("Probe", "Probe");
            var field = schema.Tables!.Add("Probe", "Probe").Fields!.Add(FieldName, "Value", dbType);
            configure?.Invoke(field);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            if (value is not null) dataObject.SetField(FieldName, value);
            return dataObject;
        }

        private IRenderedComponent<ContainerFragment> RenderEditor(ControlType controlType, FormDataObject dataObject,
            SingleFormMode mode = SingleFormMode.Edit, string numberFormat = "")
        {
            var layoutField = new LayoutField { FieldName = FieldName, ControlType = controlType, NumberFormat = numberFormat };
            var type = EditorType(controlType);
            RenderFragment editor = b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(FieldEditorBase.LayoutField), layoutField);
                b.CloseComponent();
            };
            return Render(b =>
            {
                b.OpenComponent<FormScope>(0);
                b.AddAttribute(1, nameof(FormScope.DataObject), dataObject);
                b.AddAttribute(2, nameof(FormScope.FormMode), mode);
                b.AddAttribute(3, nameof(FormScope.ChildContent), editor);
                b.CloseComponent();
            });
        }

        [Theory]
        [InlineData(ControlType.TextEdit)]
        [InlineData(ControlType.MemoEdit)]
        [DisplayName("A text editor writes what the user finished and shows it")]
        public void Text_Commit_WritesTheField(ControlType controlType)
        {
            var dataObject = BuildDataObject(FieldDbType.String, "old");
            var cut = RenderEditor(controlType, dataObject);

            Enter(cut, controlType, "new");

            Assert.Equal("new", dataObject.GetField(FieldName));
            Assert.Equal("new", ReadText(cut, controlType));
        }

        [Fact]
        [DisplayName("A value written by other code reaches the editor")]
        public void Text_ExternalWrite_RefreshesTheEditor()
        {
            var dataObject = BuildDataObject(FieldDbType.String, "old");
            var cut = RenderEditor(ControlType.TextEdit, dataObject);

            cut.InvokeAsync(() => dataObject.SetField(FieldName, "from code"));

            cut.WaitForAssertion(() => Assert.Equal("from code", ReadText(cut, ControlType.TextEdit)));
        }

        [Theory]
        [InlineData(ControlType.TextEdit, FieldDbType.String)]
        [InlineData(ControlType.MemoEdit, FieldDbType.String)]
        [InlineData(ControlType.NumericEdit, FieldDbType.Decimal)]
        [InlineData(ControlType.DateEdit, FieldDbType.Date)]
        [InlineData(ControlType.TimeEdit, FieldDbType.String)]
        [InlineData(ControlType.DateTimeEdit, FieldDbType.DateTime)]
        [InlineData(ControlType.DropDownEdit, FieldDbType.String)]
        [InlineData(ControlType.CheckEdit, FieldDbType.Boolean)]
        [DisplayName("Every editor refuses input in View mode and accepts it in Edit mode")]
        public void Mode_FollowsTheForm(ControlType controlType, FieldDbType dbType)
        {
            Assert.True(AcceptsInput(RenderEditor(controlType, BuildDataObject(dbType, null)), controlType));
            Assert.False(AcceptsInput(RenderEditor(controlType, BuildDataObject(dbType, null), SingleFormMode.View), controlType));
        }

        [Fact]
        [DisplayName("A numeric editor shows the number format at rest and full precision while focused")]
        public void Numeric_Focus_SwitchesFormat()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(FieldDbType.Decimal, "1234.567");
            var cut = RenderEditor(ControlType.NumericEdit, dataObject, numberFormat: "N2");
            Assert.Equal("1,234.57", ReadText(cut, ControlType.NumericEdit));

            FocusNumeric(cut);
            Assert.Equal("1234.567", ReadText(cut, ControlType.NumericEdit));

            BlurNumeric(cut);
            Assert.Equal("1,234.57", ReadText(cut, ControlType.NumericEdit));
        }

        [Fact]
        [DisplayName("A numeric editor keeps the stored value at full precision when the input is not a number")]
        public void Numeric_InvalidInput_KeepsTheValue()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(FieldDbType.Decimal, "1234.567");
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(ControlType.NumericEdit, dataObject, numberFormat: "N2");

            Enter(cut, ControlType.NumericEdit, "abc");

            Assert.Equal(before, dataObject.GetField(FieldName));
            Assert.Equal("1,234.57", ReadText(cut, ControlType.NumericEdit));
        }

        [Theory]
        [InlineData("8:30", "08:30")]
        [InlineData("not a time", "07:15")]
        [InlineData("", "")]
        [DisplayName("A time editor normalises a time, keeps the value for other text, and unsets on an empty box")]
        public void Time_Commit_FollowsTheRules(string typed, string expected)
        {
            var dataObject = BuildDataObject(FieldDbType.String, "07:15");
            var cut = RenderEditor(ControlType.TimeEdit, dataObject);

            Enter(cut, ControlType.TimeEdit, typed);

            Assert.Equal(expected, dataObject.GetField(FieldName));
            Assert.Equal(expected, ReadText(cut, ControlType.TimeEdit));
        }

        [Fact]
        [DisplayName("A date editor writes a date and unsets the field when cleared")]
        public void Date_CommitThenClear_WritesThenUnsets()
        {
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-03-09");
            var cut = RenderEditor(ControlType.DateEdit, dataObject);

            Enter(cut, ControlType.DateEdit, "2026-04-01");
            Assert.StartsWith("2026-04-01", dataObject.GetField(FieldName), StringComparison.Ordinal);

            Enter(cut, ControlType.DateEdit, string.Empty);
            Assert.Equal(string.Empty, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A date editor keeps the stored date when the input is not a date")]
        public void Date_InvalidInput_KeepsTheValue()
        {
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-03-09");
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(ControlType.DateEdit, dataObject);

            Enter(cut, ControlType.DateEdit, "not a date");

            Assert.Equal(before, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A date-and-time editor left as shown keeps the stored fractions of a second")]
        public void DateTime_UnchangedInput_KeepsTheValue()
        {
            var dataObject = BuildDataObject(FieldDbType.DateTime, "2026-03-09T08:30:15.1234567");
            var before = dataObject.GetField(FieldName);
            var cut = RenderEditor(ControlType.DateTimeEdit, dataObject);

            Enter(cut, ControlType.DateTimeEdit, ReadText(cut, ControlType.DateTimeEdit));

            Assert.Equal(before, dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A drop-down writes the chosen item's value and shows nothing for a value that matches no item")]
        public void DropDown_Choice_WritesTheValue()
        {
            var dataObject = BuildDataObject(FieldDbType.String, "gone", f => f.ListItems!.Add("a", "Alpha"));
            var cut = RenderEditor(ControlType.DropDownEdit, dataObject);
            Assert.Equal(string.Empty, ReadText(cut, ControlType.DropDownEdit));

            Enter(cut, ControlType.DropDownEdit, "a");

            Assert.Equal("a", dataObject.GetField(FieldName));
        }

        [Fact]
        [DisplayName("A check box follows the stored value and writes the chosen state")]
        public void Check_Commit_RoundTrips()
        {
            var dataObject = BuildDataObject(FieldDbType.Boolean, "True");
            var cut = RenderEditor(ControlType.CheckEdit, dataObject);
            Assert.True(IsChecked(cut));

            SetChecked(cut, false);

            Assert.False(IsChecked(cut));
        }

        [Theory]
        [InlineData(ControlType.TextEdit, FieldDbType.String)]
        [InlineData(ControlType.MemoEdit, FieldDbType.String)]
        [InlineData(ControlType.NumericEdit, FieldDbType.Decimal)]
        [InlineData(ControlType.ButtonEdit, FieldDbType.String)]
        [InlineData(ControlType.DateEdit, FieldDbType.Date)]
        [InlineData(ControlType.TimeEdit, FieldDbType.String)]
        [InlineData(ControlType.DateTimeEdit, FieldDbType.DateTime)]
        [InlineData(ControlType.DropDownEdit, FieldDbType.String)]
        [DisplayName("A read-only editor has the read-only look and an editable one does not")]
        public void ReadOnlyLook_FollowsTheFormMode(ControlType controlType, FieldDbType dbType)
        {
            var editable = RenderEditor(controlType, BuildDataObject(dbType, null, ConfigureListItems));
            var readOnly = RenderEditor(controlType, BuildDataObject(dbType, null, ConfigureListItems), SingleFormMode.View);

            Assert.False(HasReadOnlyLook(editable, controlType));
            Assert.True(HasReadOnlyLook(readOnly, controlType));
        }

        private static void ConfigureListItems(FormField field) => field.ListItems!.Add("a", "A");

        [Fact]
        [DisplayName("A read-only date shows in the user's culture")]
        public void Date_ReadOnly_ShowsCultureText()
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-10-25");

            var cut = RenderEditor(ControlType.DateEdit, dataObject, SingleFormMode.View);

            Assert.Equal(new DateTime(2026, 10, 25).ToString("d", System.Globalization.CultureInfo.CurrentCulture), ReadText(cut, ControlType.DateEdit));
        }

        [Fact]
        [DisplayName("An editable date shows in the user's culture, like a read-only one")]
        public void Date_Editable_ShowsCultureText()
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-10-25");

            var cut = RenderEditor(ControlType.DateEdit, dataObject);

            Assert.Equal(new DateTime(2026, 10, 25).ToString("d", System.Globalization.CultureInfo.CurrentCulture), ReadText(cut, ControlType.DateEdit));
        }

        [Theory]
        [InlineData("26.10.2026")]
        [InlineData("2026-10-26")]
        [DisplayName("A date editor takes the user's culture form and the ISO form")]
        public void Date_CultureOrIsoInput_WritesIsoDate(string typed)
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject(FieldDbType.Date, "2026-10-25");
            var cut = RenderEditor(ControlType.DateEdit, dataObject);

            Enter(cut, ControlType.DateEdit, typed);

            Assert.StartsWith("2026-10-26", dataObject.GetField(FieldName), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A year-month editor writes the value as yyyy-MM")]
        public void YearMonth_Commit_WritesYearMonth()
        {
            var dataObject = BuildDataObject(FieldDbType.String, null);
            var cut = RenderEditor(ControlType.YearMonthEdit, dataObject);

            Enter(cut, ControlType.YearMonthEdit, "2026-03");

            Assert.Equal("2026-03", dataObject.GetField(FieldName));
        }
    }
}
