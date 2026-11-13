using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Layouts;
using Polhem.Definition.Numbers;
using Polhem.Definition.Settings;
using Polhem.UI.Core.DataObjects;
using Polhem.UI.Core.Editing;
using Polhem.Web.Blazor.Controls;
using Polhem.Web.Blazor.Controls.Editors;

namespace Polhem.Web.Blazor.UnitTests.Controls
{
    /// <summary>
    /// The layout rules <see cref="FormLayoutPresenterBase"/> gives every UI package's presenter: the column count, the
    /// detail edit mode, the required cue, the editor parameters and the grid styles.
    /// </summary>
    public class FormLayoutPresenterBaseTests : BunitContext
    {
        private ProbePresenter Presenter(Action<ComponentParameterCollectionBuilder<ProbePresenter>>? configure = null)
            => Render<ProbePresenter>(p => configure?.Invoke(p)).Instance;

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Emp", "Employee");
            var fields = schema.Tables!.Add("Emp", "Employee").Fields!;
            fields.Add("emp_name", "Name", FieldDbType.String).Required = true;
            fields.Add("salary", "Salary", FieldDbType.Decimal);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Theory]
        [InlineData(false, 3, 3)]
        [InlineData(false, 0, 1)]
        [InlineData(true, 3, 1)]
        [DisplayName("The column count follows the layout, is at least one, and is one in the compact layout")]
        public void ColumnCount_FollowsLayoutAndCompact(bool compact, int columns, int expected)
        {
            var presenter = Presenter(p => p.Add(c => c.Layout, new FormLayout { ColumnCount = columns }).Add(c => c.IsCompact, compact));

            Assert.Equal(expected, presenter.ColumnCount);
            Assert.Contains($"repeat({expected},", presenter.GridStyle, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Without a layout the presenter has one column, no sections and no details")]
        public void NoLayout_HasNothingToShow()
        {
            var presenter = Presenter();

            Assert.Equal(1, presenter.ColumnCount);
            Assert.Empty(presenter.Sections);
            Assert.Empty(presenter.Details);
        }

        [Theory]
        [InlineData(false, GridEditMode.InCell, GridEditMode.InCell)]
        [InlineData(false, GridEditMode.EditForm, GridEditMode.EditForm)]
        [InlineData(true, GridEditMode.InCell, GridEditMode.EditForm)]
        [DisplayName("The compact layout always edits details in the edit form")]
        public void DetailEditMode_CompactUsesEditForm(bool compact, GridEditMode mode, GridEditMode expected)
        {
            var presenter = Presenter(p => p.Add(c => c.IsCompact, compact).Add(c => c.DetailEditMode, mode));

            Assert.Equal(expected, presenter.DetailMode);
        }

        [Fact]
        [DisplayName("A section lists only its visible fields")]
        public void Fields_SkipHiddenFields()
        {
            var section = new LayoutSection();
            section.Fields!.Add(new LayoutField { FieldName = "a" });
            section.Fields.Add(new LayoutField { FieldName = "b", Visible = false });

            Assert.Equal(["a"], ProbePresenter.Fields(section).Select(f => f.FieldName));
        }

        [Fact]
        [DisplayName("The required cue comes from the layout field or the schema, and a read-only field shows none")]
        public void RequiredCue_FollowsLayoutAndSchema()
        {
            var bound = Presenter(p => p.Add(c => c.DataObject, BuildDataObject()));
            var unbound = Presenter();

            Assert.True(bound.Required(new LayoutField { FieldName = "emp_name" }));
            Assert.False(bound.Required(new LayoutField { FieldName = "salary" }));
            Assert.True(unbound.Required(new LayoutField { FieldName = "salary", Required = true }));
            Assert.False(unbound.Required(new LayoutField { FieldName = "emp_name" }));
            Assert.False(bound.Required(new LayoutField { FieldName = "emp_name", ReadOnly = true }));
        }

        [Fact]
        [DisplayName("A numeric field's editor gets the rounding context's settings and its company's currency")]
        public void EditorParameters_Numeric_CarryTheRoundingContext()
        {
            var currencies = new CurrencySettings();
            var presenter = Presenter(p => p
                .Add(c => c.IdPrefix, "p")
                .Add(c => c.RoundingContext, new RoundingContext { CurrencySettings = currencies, Company = new CompanyInfo { DefaultCurrency = "TWD" } }));

            var parameters = presenter.EditorParameters(new LayoutField { FieldName = "salary", ControlType = ControlType.NumericEdit });

            Assert.Equal("p-salary", parameters[nameof(FieldEditorBase.Id)]);
            Assert.Same(currencies, parameters[nameof(NumericEditorBase.CurrencySettings)]);
            Assert.Equal("TWD", parameters[nameof(NumericEditorBase.DefaultCurrencyCode)]);
        }

        [Fact]
        [DisplayName("A numeric field without a company gets no default currency, and without a rounding context no settings")]
        public void EditorParameters_Numeric_WithoutCompanyOrContext()
        {
            var field = new LayoutField { FieldName = "salary", ControlType = ControlType.NumericEdit };

            var withoutCompany = Presenter(p => p.Add(c => c.RoundingContext, new RoundingContext())).EditorParameters(field);
            var withoutContext = Presenter().EditorParameters(field);

            Assert.Equal(string.Empty, withoutCompany[nameof(NumericEditorBase.DefaultCurrencyCode)]);
            Assert.False(withoutContext.ContainsKey(nameof(NumericEditorBase.CurrencySettings)));
        }

        [Theory]
        [InlineData(0, 1, 2, "grid-row:span 1;grid-column:span 1")]
        [InlineData(2, 5, 2, "grid-row:span 2;grid-column:span 2")]
        [InlineData(1, 0, 3, "grid-row:span 1;grid-column:span 1")]
        [DisplayName("A field spans at least one row and between one and all of the columns")]
        public void FieldStyle_ClampsTheSpans(int rowSpan, int columnSpan, int columns, string expected)
        {
            var field = new LayoutField { FieldName = "a", RowSpan = rowSpan, ColumnSpan = columnSpan };

            Assert.Equal(expected, ProbePresenter.FieldStyle(field, columns));
        }

        // Makes the protected rules callable; renders nothing.
        private sealed class ProbePresenter : FormLayoutPresenterBase
        {
            public int ColumnCount => EffectiveColumnCount;

            public GridEditMode DetailMode => EffectiveDetailEditMode;

            public string GridStyle => BuildGridStyle();

            public IEnumerable<LayoutSection> Sections => EnumerateSections();

            public IEnumerable<LayoutGrid> Details => EnumerateDetails();

            public static IEnumerable<LayoutField> Fields(LayoutSection section) => EnumerateFields(section);

            public static string FieldStyle(LayoutField field, int columns) => BuildFieldStyle(field, columns);

            public bool Required(LayoutField field) => ShowsRequiredCue(field);

            public Dictionary<string, object?> EditorParameters(LayoutField field) => BuildEditorParameters(field);

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
            }
        }
    }
}
