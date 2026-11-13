using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Controls;
using Polhem.Web.Blazor.Controls.Editors;
using Polhem.UI.Core.DataObjects;
using Polhem.Definition.Numbers;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's record layout and edit form must do. Their class names are common to all of them.
    /// </summary>
    public abstract class FormLayoutBehaviorTests : BunitContext, IAsyncLifetime
    {
        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync().ConfigureAwait(true);

        /// <summary>Gets the record layout component, which derives from <see cref="FormLayoutPresenterBase"/>.</summary>
        protected abstract Type PresenterType { get; }

        /// <summary>Gets the edit form component, which derives from <see cref="RowEditPanelBase"/>.</summary>
        protected abstract Type RowEditPanelType { get; }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Emp", "Employee");
            var fields = schema.Tables!.Add("Emp", "Employee").Fields!;
            fields.Add("emp_name", "Name", FieldDbType.String);
            fields.Add("emp_note", "Note", FieldDbType.String);
            return schema;
        }

        private IRenderedComponent<ContainerFragment> RenderPresenter(FormLayout? layout, FormDataObject? dataObject = null, bool compact = false,
            RoundingContext? roundingContext = null)
        {
            var type = PresenterType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(FormLayoutPresenterBase.Layout), layout);
                b.AddAttribute(2, nameof(FormLayoutPresenterBase.DataObject), dataObject);
                b.AddAttribute(3, nameof(FormLayoutPresenterBase.IsCompact), compact);
                if (roundingContext is not null)
                    b.AddAttribute(4, nameof(FormLayoutPresenterBase.RoundingContext), roundingContext);
                b.CloseComponent();
            });
        }

        [Fact]
        [DisplayName("Without a layout the presenter renders its empty state")]
        public void Presenter_NoLayout_RendersEmpty()
        {
            var cut = RenderPresenter(null);

            Assert.Single(cut.FindAll(".polhem-form-layout--empty"));
        }

        [Fact]
        [DisplayName("A section's caption follows its ShowCaption")]
        public void Presenter_ShowCaption_DecidesCaption()
        {
            var layout = FormLayoutGenerator.Generate(BuildSchema(), "default");
            layout.Sections![0].Caption = "Main";
            layout.Sections[0].ShowCaption = true;
            Assert.Single(RenderPresenter(layout).FindAll(".polhem-form-layout__section-caption"));

            layout.Sections[0].ShowCaption = false;
            Assert.Empty(RenderPresenter(layout).FindAll(".polhem-form-layout__section-caption"));
        }

        [Fact]
        [DisplayName("Compact lays the fields out in one column and clamps wider spans")]
        public void Presenter_Compact_RendersOneColumn()
        {
            var layout = FormLayoutGenerator.Generate(BuildSchema(), "default");
            layout.ColumnCount = 3;
            layout.Sections![0].Fields![0].ColumnSpan = 2;

            var cut = RenderPresenter(layout, compact: true);

            Assert.Single(cut.FindAll(".polhem-form-layout--compact"));
            Assert.Contains("repeat(1,", cut.Find(".polhem-form-layout__grid").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("grid-column:span 1", cut.FindAll(".polhem-form-layout__field")[0].GetAttribute("style"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Outside compact the layout's column count and spans apply")]
        public void Presenter_NotCompact_KeepsColumns()
        {
            var layout = FormLayoutGenerator.Generate(BuildSchema(), "default");
            layout.ColumnCount = 3;
            layout.Sections![0].Fields![0].ColumnSpan = 2;

            var cut = RenderPresenter(layout);

            Assert.Empty(cut.FindAll(".polhem-form-layout--compact"));
            Assert.Contains("repeat(3,", cut.Find(".polhem-form-layout__grid").GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("grid-column:span 2", cut.FindAll(".polhem-form-layout__field")[0].GetAttribute("style"), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A detail grid follows the sections, and edits through the edit form when compact")]
        public void Presenter_Details_RenderGrid()
        {
            var schema = OrderTestData.BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            var dataObject = OrderTestData.BuildOrder(schema);

            var cut = RenderPresenter(layout, dataObject, compact: true);

            Assert.Equal("Lines", cut.Find(".polhem-form-layout__detail-caption").TextContent.Trim());
            Assert.Equal(2, cut.FindAll("tr.polhem-grid__row").Count);
            Assert.Single(cut.FindAll(".polhem-grid__button--edit"));
        }

        private IRenderedComponent<ContainerFragment> RenderPanel(FormDataObject dataObject, Action? committed = null, Action? cancelled = null)
        {
            var schema = OrderTestData.BuildSchema();
            var type = RowEditPanelType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(RowEditPanelBase.DataObject), dataObject);
                b.AddAttribute(2, nameof(RowEditPanelBase.Layout), OrderTestData.LinesLayout(schema));
                b.AddAttribute(3, nameof(RowEditPanelBase.Row), OrderTestData.Line(dataObject, 0));
                b.AddAttribute(4, nameof(RowEditPanelBase.EditCommitted), EventCallback.Factory.Create(this, () => committed?.Invoke()));
                b.AddAttribute(5, nameof(RowEditPanelBase.EditCancelled), EventCallback.Factory.Create(this, () => cancelled?.Invoke()));
                b.CloseComponent();
            });
        }

        private static FormDataObject BuildAcceptedOrder()
        {
            var dataObject = OrderTestData.BuildOrder();
            dataObject.DataSet.AcceptChanges();
            return dataObject;
        }

        [Fact]
        [DisplayName("Edits stay proposed until OK, which commits them and reports EditCommitted")]
        public void Panel_Ok_Commits()
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
        public void Panel_Cancel_Restores()
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
        public async Task Panel_Removed_Cancels()
        {
            var dataObject = BuildAcceptedOrder();
            var cut = RenderPanel(dataObject);
            cut.Find("#polhem-row-edit-item").Change("Washer");

            await DisposeComponentsAsync();

            Assert.Equal("Bolt", dataObject.GetField(OrderTestData.Line(dataObject, 0), "item"));
            Assert.False(OrderTestData.Line(dataObject, 0).HasVersion(System.Data.DataRowVersion.Proposed));
        }

        [Theory]
        [InlineData(true, "N3")]
        [InlineData(false, "")]
        [DisplayName("An amount in the edit form takes its decimals from the currency master the grid passes")]
        public void Panel_Amount_UsesCurrencySettings(bool withCurrencies, string expectedFormat)
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            var lines = schema.Tables.Add("OrderLine", "Lines").Fields!;
            lines.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid) { Visible = false });
            lines.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid) { Visible = false });
            lines.Add(new FormField("total", "Total", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            var line = dataObject.DataSet.Tables["OrderLine"]!.NewRow();
            line["total"] = 252m;
            dataObject.DataSet.Tables["OrderLine"]!.Rows.Add(line);
            dataObject.DataSet.AcceptChanges();
            var currencies = withCurrencies
                ? new CurrencySettings { new CurrencyItem("BHD", 0.001m) }
                : null;

            var type = RowEditPanelType;
            var cut = Render(b =>
            {
                b.OpenComponent(0, type);
                b.AddAttribute(1, nameof(RowEditPanelBase.DataObject), dataObject);
                b.AddAttribute(2, nameof(RowEditPanelBase.Layout), FormLayoutGenerator.Generate(schema, "default").Details!.Single());
                b.AddAttribute(3, nameof(RowEditPanelBase.Row), line);
                b.AddAttribute(4, nameof(RowEditPanelBase.CurrencySettings), currencies);
                b.AddAttribute(5, nameof(RowEditPanelBase.DefaultCurrencyCode), "BHD");
                b.CloseComponent();
            });

            var expected = expectedFormat.Length == 0 ? "252" : 252m.ToString(expectedFormat, System.Globalization.CultureInfo.CurrentCulture);
            Assert.Equal(expected, cut.Find("#polhem-row-edit-total").GetAttribute("value"));
        }

        [Fact]
        [DisplayName("An editor in the session refreshes when something else writes the row, such as a recompute")]
        public void Panel_OtherWrite_Refreshes()
        {
            var dataObject = BuildAcceptedOrder();
            var cut = RenderPanel(dataObject);

            cut.InvokeAsync(() => dataObject.SetField(OrderTestData.Line(dataObject, 0), "amount", "42"));

            cut.WaitForAssertion(() => Assert.StartsWith("42", cut.Find("#polhem-row-edit-amount").GetAttribute("value"), StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A section and a detail without a caption show none, and the detail grid gets the rounding context")]
        public void Presenter_NoCaptions_PassesTheRoundingContext()
        {
            var schema = OrderTestData.BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");
            layout.Sections![0].Caption = string.Empty;
            layout.Sections[0].ShowCaption = true;
            layout.Details![0].Caption = string.Empty;
            var currencies = new CurrencySettings();
            var units = new UnitSettings();
            var rounding = new RoundingContext
            {
                CurrencySettings = currencies,
                UnitSettings = units,
                Company = new Polhem.Definition.Identity.CompanyInfo { DefaultCurrency = "TWD" },
            };

            var cut = RenderPresenter(layout, OrderTestData.BuildOrder(schema), roundingContext: rounding);

            Assert.Empty(cut.FindAll(".polhem-form-layout__section-caption"));
            Assert.Empty(cut.FindAll(".polhem-form-layout__detail-caption"));
            var grid = cut.FindComponent<GridControlBase>().Instance;
            Assert.Same(currencies, grid.CurrencySettings);
            Assert.Same(units, grid.UnitSettings);
            Assert.Equal("TWD", grid.DefaultCurrencyCode);
        }

        [Fact]
        [DisplayName("A rounding context without a company gives the detail grid no default currency")]
        public void Presenter_RoundingWithoutCompany_NoDefaultCurrency()
        {
            var schema = OrderTestData.BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "default");

            var cut = RenderPresenter(layout, OrderTestData.BuildOrder(schema), roundingContext: new RoundingContext());

            Assert.Equal(string.Empty, cut.FindComponent<GridControlBase>().Instance.DefaultCurrencyCode);
        }
    }
}
