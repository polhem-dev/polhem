using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The XML view's completion of free-text values and its attribute help, which come from the document: the values
    /// its property grid offers (<see cref="ObjectTreeDocumentViewModelBase.XmlValueSuggestions"/>) and the properties'
    /// descriptions (<see cref="ObjectTreeDocumentViewModelBase.XmlAttributeHelp"/>).
    /// </summary>
    public sealed class XmlValueCompletionTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-xmlvalues-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;

        public XmlValueCompletionTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            var customer = master.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            customer.RelationProgId = "Customer";
            customer.RelationFieldMappings.Add("sys_id", "ref_customer_id");
            master.Fields!.Add("ref_customer_id", "Customer Code", FieldDbType.String);
            var detail = schema.Tables!.Add("OrderLine", "Order Line");
            detail.Fields!.Add("quantity", "Quantity", FieldDbType.Decimal);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            _document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
            _document.ShowXmlCommand.Execute(null);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private XmlCompletionSources Sources =>
            new(a => a.TypeName, "default", _document.XmlValueSuggestions, _document.XmlAttributeHelp);

        // The suggestions with the caret right after the opening quote of the first `attribute="` after `anchor`.
        private XmlSuggestions ValuesAfter(string anchor, string attribute)
        {
            var text = _document.XmlText;
            var start = text.IndexOf(anchor, StringComparison.Ordinal);
            Assert.True(start >= 0, anchor);
            var offset = text.IndexOf(attribute + "=\"", start, StringComparison.Ordinal) + attribute.Length + 2;
            return XmlCompletion.At(text, offset, _document.XmlRootType, Sources);
        }

        private static string[] Labels(XmlSuggestions suggestions) => suggestions.Items.Select(i => i.Label).ToArray();

        [Fact]
        [DisplayName("A free-text attribute offers the values the property grid offers for it")]
        public void Values_RootCategoryId_OffersCategoryIds()
        {
            var labels = Labels(ValuesAfter("<FormSchema", "CategoryId"));

            Assert.Contains("company", labels);
            Assert.Contains("common", labels);
        }

        [Fact]
        [DisplayName("A field mapping's destination offers the fields of the table its field belongs to, found without a tree selection")]
        public void Values_MappingDestination_OffersFieldsOfOwningTable()
        {
            var labels = Labels(ValuesAfter("<FieldMapping", "DestinationField"));

            Assert.Contains("ref_customer_id", labels);
            Assert.Contains("sys_id", labels);
            Assert.DoesNotContain("quantity", labels);
        }

        [Fact]
        [DisplayName("An attribute with values in the solution is followed by its values once picked")]
        public void Attributes_FreeTextWithValues_OffersValues()
        {
            var text = _document.XmlText;
            var offset = text.IndexOf("<FormSchema ", StringComparison.Ordinal) + "<FormSchema ".Length;

            var attributes = XmlCompletion.At(text, offset, _document.XmlRootType, Sources);

            // The grid offers nothing for LookupFields, which has a picker of its own instead.
            Assert.False(Assert.Single(attributes.Items, i => i.Label == "LookupFields").OffersValues);
            Assert.True(Assert.Single(attributes.Items, i => i.Label == "CurrencyField").OffersValues);
        }

        [Fact]
        [DisplayName("An attribute's help is its property description, also for an element not read into the document yet")]
        public void Help_FieldAttribute_IsPropertyDescription()
        {
            var text = _document.XmlText;
            var fields = text.IndexOf("<FormField ", StringComparison.Ordinal);
            var typed = text.Insert(fields, "<FormField ");

            var attributes = XmlCompletion.At(typed, fields + "<FormField ".Length, _document.XmlRootType, Sources);

            var expected = typeof(FormField).GetProperty(nameof(FormField.RelationProgId))!
                .GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>().Single().Description;
            var shown = PropertyLabels.Translate(new PropertyGridText(
                PropertyGridTextKind.Description, typeof(FormField), nameof(FormField.RelationProgId), expected)) ?? expected;
            var detail = Assert.Single(attributes.Items, i => i.Label == "RelationProgId").Detail;
            Assert.Equal(shown, detail.Split('\n')[1]);
        }

        [Fact]
        [DisplayName("An element at a place the document does not have yet offers no solution values and does not fail")]
        public void Values_ElementNotInDocument_OffersNone()
        {
            const string Typed = "<FormTable><Fields><FormField CurrencyField=\"";
            var text = _document.XmlText;
            var end = text.IndexOf("</Tables>", StringComparison.Ordinal);
            var typed = text.Insert(end, Typed);

            var values = XmlCompletion.At(typed, end + Typed.Length, _document.XmlRootType, Sources);

            Assert.Empty(values.Items);
        }
    }
}
