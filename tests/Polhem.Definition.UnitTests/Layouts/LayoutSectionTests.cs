using System.ComponentModel;
using Polhem.Base.Serialization;
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
        }

        [Fact]
        [DisplayName("Fields returns null when serializing an empty collection")]
        public void Fields_EmptyDuringSerialize_ReturnsNull()
        {
            var section = new LayoutSection();
            section.SetSerializeState(SerializeState.Serialize);

            Assert.Null(section.Fields);
        }

        [Fact]
        [DisplayName("SetSerializeState sets the object's own state")]
        public void SetSerializeState_UpdatesState()
        {
            var section = new LayoutSection();

            section.SetSerializeState(SerializeState.Serialize);

            Assert.Equal(SerializeState.Serialize, section.SerializeState);
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
