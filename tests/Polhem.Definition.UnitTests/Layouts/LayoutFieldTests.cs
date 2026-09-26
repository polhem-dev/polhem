using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutField.
    /// </summary>
    public class LayoutFieldTests
    {
        [Fact]
        [DisplayName("Default constructor initializes the expected default values")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var field = new LayoutField();

            Assert.Equal(string.Empty, field.FieldName);
            Assert.Equal(string.Empty, field.Caption);
            Assert.Equal(ControlType.TextEdit, field.ControlType);
            Assert.Equal(1, field.RowSpan);
            Assert.Equal(1, field.ColumnSpan);
            Assert.True(field.Visible);
            Assert.False(field.ReadOnly);
            Assert.Equal(string.Empty, field.DisplayFormat);
            Assert.Equal(string.Empty, field.NumberFormat);
            Assert.Equal(FormEditModes.All, field.AllowEditModes);
        }

        [Fact]
        [DisplayName("A non-default AllowEditModes is restored through an XML round-trip, and the default is not written")]
        public void AllowEditModes_XmlRoundTrip_PreservesValueAndOmitsDefault()
        {
            var layout = new FormLayout();
            var section = new LayoutSection { Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "doc_no", AllowEditModes = FormEditModes.Add });
            section.Fields.Add(new LayoutField { FieldName = "emp_name" });
            layout.Sections!.Add(section);

            var xml = XmlCodec.Serialize(layout);
            Assert.Contains("AllowEditModes=\"Add\"", xml);
            // The default (All) must not be written, so existing layout files stay untouched.
            Assert.Single(xml.Split("AllowEditModes", StringSplitOptions.None).Skip(1));

            var restored = XmlCodec.Deserialize<FormLayout>(xml);
            Assert.NotNull(restored);
            Assert.Equal(FormEditModes.Add, restored!.Sections![0].Fields![0].AllowEditModes);
            Assert.Equal(FormEditModes.All, restored.Sections[0].Fields![1].AllowEditModes);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(-100, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(5, 5)]
        [DisplayName("RowSpan below 1 is corrected to 1")]
        public void RowSpan_BelowOne_ClampedToOne(int input, int expected)
        {
            var field = new LayoutField { RowSpan = input };

            Assert.Equal(expected, field.RowSpan);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(-100, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(10, 10)]
        [DisplayName("ColumnSpan below 1 is corrected to 1")]
        public void ColumnSpan_BelowOne_ClampedToOne(int input, int expected)
        {
            var field = new LayoutField { ColumnSpan = input };

            Assert.Equal(expected, field.ColumnSpan);
        }

        [Fact]
        [DisplayName("ToString returns \"FieldName - Caption\"")]
        public void ToString_ReturnsFormatted()
        {
            var field = new LayoutField { FieldName = "Amount", Caption = "金額" };

            Assert.Equal("Amount - 金額", field.ToString());
        }

        [Fact]
        [DisplayName("ExtendedProperties returns the collection instance when not serializing")]
        public void ExtendedProperties_DefaultState_ReturnsCollection()
        {
            var field = new LayoutField();

            Assert.NotNull(field.ExtendedProperties);
        }

        [Fact]
        [DisplayName("ExtendedProperties returns null when serializing an empty collection")]
        public void ExtendedProperties_EmptyDuringSerialize_ReturnsNull()
        {
            var field = new LayoutField();
            field.SetSerializeState(SerializeState.Serialize);

            Assert.Null(field.ExtendedProperties);
        }

        [Fact]
        [DisplayName("SetSerializeState sets the object's own state")]
        public void SetSerializeState_UpdatesState()
        {
            var field = new LayoutField();

            field.SetSerializeState(SerializeState.Serialize);

            Assert.Equal(SerializeState.Serialize, field.SerializeState);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var field = new LayoutField
            {
                FieldName = "Amount",
                Caption = "金額",
                ControlType = ControlType.CheckEdit,
                RowSpan = 2,
                ColumnSpan = 3,
                Visible = false,
                ReadOnly = true,
                DisplayFormat = "{0:C}",
                NumberFormat = "N2"
            };

            Assert.Equal("Amount", field.FieldName);
            Assert.Equal("金額", field.Caption);
            Assert.Equal(ControlType.CheckEdit, field.ControlType);
            Assert.Equal(2, field.RowSpan);
            Assert.Equal(3, field.ColumnSpan);
            Assert.False(field.Visible);
            Assert.True(field.ReadOnly);
            Assert.Equal("{0:C}", field.DisplayFormat);
            Assert.Equal("N2", field.NumberFormat);
        }
    }
}
