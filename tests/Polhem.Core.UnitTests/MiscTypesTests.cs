using System.ComponentModel;
using Polhem.Core.Attributes;

namespace Polhem.Core.UnitTests
{
    public class TreeNodeAttributeTests
    {
        [TreeNode("Literal Label")]
        private sealed class LiteralClass { }

        [TreeNode("{0}-{1}", "Name,Age")]
        private sealed class FormattedClass
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [TreeNode("{0}", "Name")]
        private sealed class EmptyFormattedClass : IDisplayName
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        private sealed class NoAttrClass
        {
            public override string ToString() => "no-attr-tostring";
        }

        [TreeNode("label", collectionFolder: true)]
        private sealed class CollectionFolderClass { }

        [Fact]
        [DisplayName("GetDisplayText returns the literal string of the attribute directly")]
        public void GetDisplayText_Literal_ReturnsDisplayFormat()
        {
            Assert.Equal("Literal Label", TreeNodeAttribute.GetDisplayText(new LiteralClass()));
        }

        [Fact]
        [DisplayName("GetDisplayText formats the property values when the attribute has a PropertyName")]
        public void GetDisplayText_WithPropertyName_FormatsValues()
        {
            var obj = new FormattedClass { Name = "Alice", Age = 30 };
            Assert.Equal("Alice-30", TreeNodeAttribute.GetDisplayText(obj));
        }

        [Fact]
        [DisplayName("GetDisplayText returns ToString when there is no attribute")]
        public void GetDisplayText_NoAttribute_ReturnsToString()
        {
            Assert.Equal("no-attr-tostring", TreeNodeAttribute.GetDisplayText(new NoAttrClass()));
        }

        [Fact]
        [DisplayName("GetDisplayText falls back to DisplayName when the formatted result is empty and the type implements IDisplayName")]
        public void GetDisplayText_EmptyFormattedValue_FallsBackToIDisplayName()
        {
            var obj = new EmptyFormattedClass { Name = string.Empty, DisplayName = "fallback" };
            Assert.Equal("fallback", TreeNodeAttribute.GetDisplayText(obj));
        }

        [Fact]
        [DisplayName("The TreeNodeAttribute constructor stores CollectionFolder and DisplayFormat")]
        public void Ctor_CollectionFolder_StoresFlag()
        {
            var attr = new TreeNodeAttribute("label", collectionFolder: true);
            Assert.Equal("label", attr.DisplayFormat);
            Assert.True(attr.CollectionFolder);
        }

        [Fact]
        [DisplayName("The parameterless TreeNodeAttribute constructor initializes empty strings and false")]
        public void Ctor_Parameterless_Defaults()
        {
            var attr = new TreeNodeAttribute();
            Assert.Equal(string.Empty, attr.DisplayFormat);
            Assert.Equal(string.Empty, attr.PropertyName);
            Assert.False(attr.CollectionFolder);
        }

        [Fact]
        [DisplayName("TreeNodeIgnoreAttribute can be instantiated")]
        public void TreeNodeIgnoreAttribute_CanBeInstantiated()
        {
            Assert.NotNull(new TreeNodeIgnoreAttribute());
        }
    }
}
