using System.ComponentModel;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The XML view's completion: <see cref="XmlCaretContext"/> finds where the caret is, <see cref="DefineXmlSchema"/>
    /// says what the definition type allows there, and <see cref="XmlCompletion"/> turns the two into suggestions.
    /// </summary>
    public sealed class XmlCompletionTests
    {
        private const string Caret = "|";

        private const string Form = """
            <FormSchema ProgId="Order">
              <Tables>
                <FormTable TableName="Order">
                  <Fields>
                    <FormField FieldName="total" |
            """;

        // Splits a text marked with a caret into the text and the caret's offset.
        private static (string Text, int Offset) Marked(string marked) =>
            (marked.Replace(Caret, string.Empty, StringComparison.Ordinal), marked.IndexOf(Caret, StringComparison.Ordinal));

        private static XmlCaretContext ContextOf(string marked)
        {
            var (text, offset) = Marked(marked);
            return XmlCaretContext.At(text, offset);
        }

        private static XmlSuggestions SuggestionsAt(string marked, Type? rootType = null)
        {
            var (text, offset) = Marked(marked);
            return XmlCompletion.At(text, offset, rootType ?? typeof(FormSchema), new XmlCompletionSources(a => a.TypeName, "default"));
        }

        private static string[] Labels(XmlSuggestions suggestions) => suggestions.Items.Select(i => i.Label).ToArray();

        [Fact]
        [DisplayName("After a start tag's name and a space, the caret is where an attribute of that element goes")]
        public void At_AfterSpaceInStartTag_IsAttributeName()
        {
            var context = ContextOf(Form);

            Assert.Equal(XmlCaretContextKind.AttributeName, context.Kind);
            Assert.Equal(["FormSchema", "Tables", "FormTable", "Fields", "FormField"], context.Path);
            Assert.Equal(["FieldName"], context.PresentAttributes);
        }

        [Fact]
        [DisplayName("Inside an attribute's quotes, the caret is in that attribute's value with what was typed of it")]
        public void At_InsideQuotes_IsAttributeValue()
        {
            var context = ContextOf("""<FormSchema><Tables><FormTable><Fields><FormField DbType="Dec|""");

            Assert.Equal(XmlCaretContextKind.AttributeValue, context.Kind);
            Assert.Equal("DbType", context.AttributeName);
            Assert.Equal("Dec", context.Prefix);
        }

        [Fact]
        [DisplayName("Closed elements, self-closing tags and comments do not stay open")]
        public void At_AfterClosedElements_PathHasOnlyOpenOnes()
        {
            var context = ContextOf("""
                <FormSchema>
                  <!-- <Rules> -->
                  <Tables>
                    <FormTable TableName="A"><Fields><FormField FieldName="x" /></Fields></FormTable>
                  </Tables>
                  <|
                """);

            Assert.Equal(XmlCaretContextKind.ElementName, context.Kind);
            Assert.Equal(["FormSchema"], context.Path);
        }

        [Fact]
        [DisplayName("A caret in a comment gets no suggestion")]
        public void At_InComment_IsContent()
        {
            Assert.Equal(XmlCaretContextKind.Content, ContextOf("<FormSchema><!-- <Tab|").Kind);
        }

        [Fact]
        [DisplayName("A tag closed right before the caret is reported, so its end tag can follow")]
        public void EndTagAfterClose_StartTagJustClosed_ReturnsEndTag()
        {
            var (text, offset) = Marked("<FormSchema><Tables>|");

            Assert.Equal("</Tables>", XmlCompletion.EndTagAfterClose(text, offset));
        }

        [Fact]
        [DisplayName("No end tag is added after a self-closing tag or when the end tag is already there")]
        public void EndTagAfterClose_SelfClosingOrPresent_ReturnsNull()
        {
            var (selfClosing, at) = Marked("<FormSchema><Tables/>|");
            var (present, there) = Marked("<FormSchema><Tables>|</Tables>");

            Assert.Null(XmlCompletion.EndTagAfterClose(selfClosing, at));
            Assert.Null(XmlCompletion.EndTagAfterClose(present, there));
        }

        [Fact]
        [DisplayName("After `<` inside FormSchema the suggestions are its child elements and its end tag")]
        public void At_ElementName_SuggestsChildElementsAndEndTag()
        {
            var suggestions = SuggestionsAt("<FormSchema>\n  <|");

            Assert.Equal(["Tables", "Rules", "/FormSchema"], Labels(suggestions));
        }

        [Fact]
        [DisplayName("Inside a collection the suggestion is its item element")]
        public void At_ElementNameInCollection_SuggestsItemElement()
        {
            const string Marked = "<FormSchema><Tables><FormTable><Fields>\n<Form|";
            var suggestions = SuggestionsAt(Marked);

            Assert.Contains("FormField", Labels(suggestions));
            Assert.Equal(Marked.IndexOf("Form|", StringComparison.Ordinal), suggestions.ReplaceFrom);
        }

        [Fact]
        [DisplayName("Attribute suggestions leave out the ones the tag already has and put the caret inside the quotes")]
        public void At_AttributeName_SuggestsMissingAttributes()
        {
            var suggestions = SuggestionsAt(Form);

            Assert.DoesNotContain("FieldName", Labels(suggestions));
            var dbType = Assert.Single(suggestions.Items, i => i.Label == "DbType");
            Assert.Equal("DbType=\"\"", dbType.InsertText);
            Assert.Equal(1, dbType.CaretOffset);
            Assert.True(dbType.OffersValues);
            Assert.False(Assert.Single(suggestions.Items, i => i.Label == "Caption").OffersValues);
        }

        [Fact]
        [DisplayName("An attribute inherited from a base type is suggested too")]
        public void At_AttributeName_IncludesInheritedAttributes()
        {
            var suggestions = SuggestionsAt("<FormSchema><Tables>\n<FormTable |");

            Assert.Contains("TableName", Labels(suggestions));
            Assert.Contains("DisplayName", Labels(suggestions));
        }

        [Fact]
        [DisplayName("An enum attribute's value suggestions are its members, the default marked, replacing what was typed")]
        public void At_EnumValue_SuggestsMembers()
        {
            var marked = """<FormSchema><Tables><FormTable><Fields><FormField ControlType="Te|""";
            var suggestions = SuggestionsAt(marked);

            var labels = Labels(suggestions);
            Assert.Contains("TextEdit", labels);
            Assert.Contains("DropDownEdit", labels);
            Assert.Equal("default", Assert.Single(suggestions.Items, i => i.Label == "Auto").Detail);
            Assert.Equal(marked.IndexOf(Caret, StringComparison.Ordinal) - 2, suggestions.ReplaceFrom);
        }

        [Fact]
        [DisplayName("A boolean attribute's value suggestions are true and false; free text gets none")]
        public void At_BooleanAndTextValues()
        {
            Assert.Equal(["true", "false"],
                Labels(SuggestionsAt("""<FormSchema><Tables><FormTable><Fields><FormField Visible="|""")));
            Assert.Empty(SuggestionsAt("""<FormSchema><Tables><FormTable><Fields><FormField Caption="|""").Items);
        }

        [Fact]
        [DisplayName("Completion follows the schema of the document's own root type")]
        public void At_OtherRootType_UsesItsSchema()
        {
            var suggestions = SuggestionsAt("<LanguageResource>\n<|", typeof(LanguageResource));

            Assert.Contains("Items", Labels(suggestions));
            Assert.Contains("Enums", Labels(suggestions));
        }

        [Fact]
        [DisplayName("An empty document suggests its root element")]
        public void At_EmptyDocument_SuggestsRoot()
        {
            Assert.Equal(["FormSchema"], Labels(SuggestionsAt("<?xml version=\"1.0\"?>\n<|")));
        }

        [Fact]
        [DisplayName("Value sources are asked with the caret element's place among its siblings")]
        public void At_ValueSource_GetsElementSteps()
        {
            IReadOnlyList<XmlElementStep>? asked = null;
            var sources = new XmlCompletionSources(a => a.TypeName, "default", (steps, _) =>
            {
                asked = steps;
                return ["x"];
            });
            var (text, offset) = Marked("""
                <FormSchema><Tables><FormTable><Fields>
                  <FormField FieldName="a" /><!-- <FormField> -->
                  <FormField FieldName="b"><ListItems /></FormField>
                  <FormField Caption="|
                """);

            Assert.Equal(["x"], Labels(XmlCompletion.At(text, offset, typeof(FormSchema), sources)));
            Assert.Equal(new XmlElementStep("FormField", 2, 2), asked![^1]);
            Assert.Equal(new XmlElementStep("FormTable", 0, 0), asked[2]);
        }

        [Fact]
        [DisplayName("Attributes the tag has after the caret are not suggested again")]
        public void At_AttributeName_SkipsAttributesAfterCaret()
        {
            var labels = Labels(SuggestionsAt(
                """<FormSchema><Tables><FormTable><Fields><FormField FieldName="a" | Caption="x y" DbType="String" />"""));

            Assert.DoesNotContain("Caption", labels);
            Assert.DoesNotContain("DbType", labels);
            Assert.DoesNotContain("FieldName", labels);
            Assert.Contains("Visible", labels);
        }
    }
}
