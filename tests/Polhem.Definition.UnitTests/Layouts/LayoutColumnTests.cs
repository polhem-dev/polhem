using System.ComponentModel;
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
            Assert.Empty(column.ExtendedProperties!);
        }

        [Fact]
        [DisplayName("ExtendedProperties is not serialized while it is empty, whether or not it was read")]
        public void ExtendedProperties_EmptyCollection_IsNotSerialized()
        {
            var column = new LayoutColumn();

            Assert.False(column.ExtendedPropertiesSpecified);
            Assert.Empty(column.ExtendedProperties!);
            Assert.False(column.ExtendedPropertiesSpecified);
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
