using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Connectors;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.UnitTests.Controls
{
    /// <summary>
    /// The choices a Boolean or list filter offers, and the text each shows.
    /// </summary>
    public class GridColumnFilterBaseTests : BunitContext
    {
        private ProbeFilter RenderProbe(ColumnFilterKind kind, FormField field)
            => Render<ProbeFilter>(p => p
                .Add(c => c.Column, new LayoutColumn { FieldName = field.FieldName, Caption = field.Caption })
                .Add(c => c.Field, field)
                .Add(c => c.Kind, kind)
                .Add(c => c.Filters, new ListFilterState())).Instance;

        [Fact]
        [DisplayName("A Boolean filter offers no filter, true and false, and shows true for any casing of True")]
        public void Boolean_ChoicesAndText()
        {
            var probe = RenderProbe(ColumnFilterKind.Boolean, new FormField("active", "Active", FieldDbType.Boolean));

            Assert.Equal([string.Empty, bool.TrueString, bool.FalseString], probe.ChoiceValues);
            Assert.Equal(probe.TextOf(bool.TrueString), probe.TextOf("true"));
            Assert.NotEqual(probe.TextOf(bool.TrueString), probe.TextOf(bool.FalseString));
            Assert.Equal(string.Empty, probe.TextOf(string.Empty));
        }

        [Fact]
        [DisplayName("A list filter offers no filter and each list item, and shows an item's text for its value")]
        public void Choice_ChoicesAndText()
        {
            var field = new FormField("status", "Status", FieldDbType.String);
            field.ListItems!.Add(new ListItem("A", "Active"));
            field.ListItems.Add(new ListItem("C", "Closed"));
            var probe = RenderProbe(ColumnFilterKind.Choice, field);

            Assert.Equal([string.Empty, "A", "C"], probe.ChoiceValues);
            Assert.Equal("Closed", probe.TextOf("C"));
            Assert.Equal("Z", probe.TextOf("Z"));
        }

        private sealed class ProbeFilter : GridColumnFilterBase
        {
            public IReadOnlyList<string> ChoiceValues => Choices.Select(c => c.Key).ToList();

            public string TextOf(string value) => ChoiceText(value);
        }
    }
}
