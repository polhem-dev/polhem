using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutColumn.
    /// </summary>
    public class LayoutColumnTests
    {
        [Fact]
        [DisplayName("Default constructor initializes the expected default values")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var column = new LayoutColumn();

            Assert.Equal(string.Empty, column.FieldName);
            Assert.Equal(string.Empty, column.Caption);
            Assert.Equal(ControlType.TextEdit, column.ControlType);
            Assert.True(column.Visible);
            Assert.False(column.ReadOnly);
            Assert.Equal(0, column.Width);
            Assert.Equal(string.Empty, column.DisplayFormat);
            Assert.Equal(string.Empty, column.NumberFormat);
        }

        [Fact]
        [DisplayName("Parameterized constructor sets FieldName, Caption and ControlType")]
        public void ParameterizedConstructor_SetsProperties()
        {
            var column = new LayoutColumn("Amount", "金額", ControlType.TextEdit);

            Assert.Equal("Amount", column.FieldName);
            Assert.Equal("金額", column.Caption);
            Assert.Equal(ControlType.TextEdit, column.ControlType);
        }

        [Fact]
        [DisplayName("ToString returns \"FieldName - Caption\"")]
        public void ToString_ReturnsFormatted()
        {
            var column = new LayoutColumn("Amount", "金額", ControlType.TextEdit);

            Assert.Equal("Amount - 金額", column.ToString());
        }

        [Fact]
        [DisplayName("ExtendedProperties returns the collection instance when not serializing")]
        public void ExtendedProperties_DefaultState_ReturnsCollection()
        {
            var column = new LayoutColumn();

            Assert.NotNull(column.ExtendedProperties);
        }

        [Fact]
        [DisplayName("ExtendedProperties returns null when serializing an empty collection")]
        public void ExtendedProperties_EmptyDuringSerialize_ReturnsNull()
        {
            var column = new LayoutColumn();
            column.SetSerializeState(SerializeState.Serialize);

            Assert.Null(column.ExtendedProperties);
        }

        [Fact]
        [DisplayName("SetSerializeState sets the object's own state")]
        public void SetSerializeState_UpdatesState()
        {
            var column = new LayoutColumn();

            column.SetSerializeState(SerializeState.Serialize);

            Assert.Equal(SerializeState.Serialize, column.SerializeState);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var column = new LayoutColumn
            {
                FieldName = "A",
                Caption = "B",
                ControlType = ControlType.CheckEdit,
                Visible = false,
                ReadOnly = true,
                Width = 120,
                DisplayFormat = "{0:C}",
                NumberFormat = "N2"
            };

            Assert.Equal("A", column.FieldName);
            Assert.Equal("B", column.Caption);
            Assert.Equal(ControlType.CheckEdit, column.ControlType);
            Assert.False(column.Visible);
            Assert.True(column.ReadOnly);
            Assert.Equal(120, column.Width);
            Assert.Equal("{0:C}", column.DisplayFormat);
            Assert.Equal("N2", column.NumberFormat);
        }
    }
}
