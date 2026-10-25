using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="NumericEdit"/>, aligned with the Avalonia head's <c>NumericEdit</c>: the
    /// display format at rest, full precision while focused, and full-precision write-back.
    /// </summary>
    public class NumericEditTests : BunitContext
    {
        private const string FieldName = "amount";
        private const string InputSelector = "input.polhem-field-editor--numeric";

        private static FormDataObject BuildDataObject(decimal value)
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(FieldName, "Amount", FieldDbType.Decimal);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.MasterRow![FieldName] = value;
            return dataObject;
        }

        private IRenderedComponent<NumericEdit> RenderEditor(FormDataObject dataObject)
            => Render<NumericEdit>(p => p
                .Add(c => c.LayoutField, new LayoutField { FieldName = FieldName, NumberFormat = "N2" })
                .Add(c => c.DataObject, dataObject));

        [Fact]
        [DisplayName("At rest the value is shown in the field's number format and the user's culture")]
        public void Render_AtRest_UsesNumberFormat()
        {
            using var culture = new CultureScope("en-US");

            var cut = RenderEditor(BuildDataObject(1234.567m));

            Assert.Equal("1,234.57", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("Focusing the editor shows the full-precision value and blurring restores the format")]
        public void Focus_ShowsFullPrecision_BlurRestoresFormat()
        {
            using var culture = new CultureScope("en-US");
            var cut = RenderEditor(BuildDataObject(1234.567m));

            cut.Find(InputSelector).Focus();
            Assert.Equal("1234.567", cut.Find(InputSelector).GetAttribute("value"));

            cut.Find(InputSelector).Blur();
            Assert.Equal("1,234.57", cut.Find(InputSelector).GetAttribute("value"));
        }

        [Fact]
        [DisplayName("A committed number is written back at full precision")]
        public void Change_Number_WritesFullPrecision()
        {
            using var culture = new CultureScope("de-DE");
            var dataObject = BuildDataObject(0m);
            var cut = RenderEditor(dataObject);

            cut.Find(InputSelector).Change("1,23456");

            Assert.Equal(1.23456m, (decimal)dataObject.MasterRow![FieldName]);
        }

        [Fact]
        [DisplayName("Input that does not parse keeps the last valid value")]
        public void Change_Invalid_KeepsValue()
        {
            using var culture = new CultureScope("en-US");
            var dataObject = BuildDataObject(7m);
            var cut = RenderEditor(dataObject);

            cut.Find(InputSelector).Change("7.x");

            Assert.Equal(7m, (decimal)dataObject.MasterRow![FieldName]);
        }
    }
}
