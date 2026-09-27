using System.ComponentModel;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutSection.
    /// </summary>
    public class LayoutSectionTests
    {
        [Fact]
        [DisplayName("Default constructor initializes the expected default values")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var section = new LayoutSection();

            Assert.Equal(string.Empty, section.Name);
            Assert.Equal(string.Empty, section.Caption);
            Assert.True(section.ShowCaption);
        }

        [Fact]
        [DisplayName("ToString returns \"Name - Caption\"")]
        public void ToString_ReturnsFormatted()
        {
            var section = new LayoutSection { Name = "Main", Caption = "主資料" };

            Assert.Equal("Main - 主資料", section.ToString());
        }

        [Fact]
        [DisplayName("Fields returns the collection instance when not serializing")]
        public void Fields_DefaultState_ReturnsCollection()
        {
            var section = new LayoutSection();

            Assert.NotNull(section.Fields);
            Assert.Empty(section.Fields!);
        }

        [Fact]
        [DisplayName("Fields is not serialized while it is empty, whether or not it was read")]
        public void Fields_EmptyCollection_IsNotSerialized()
        {
            var section = new LayoutSection();

            Assert.False(section.FieldsSpecified);
            Assert.Empty(section.Fields!);
            Assert.False(section.FieldsSpecified);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var section = new LayoutSection
            {
                Name = "Main",
                Caption = "基本資料",
                ShowCaption = false
            };

            Assert.Equal("Main", section.Name);
            Assert.Equal("基本資料", section.Caption);
            Assert.False(section.ShowCaption);
        }
    }
}
