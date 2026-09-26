using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.UnitTests.Layouts
{
    /// <summary>
    /// Unit tests for LayoutGrid.
    /// </summary>
    public class LayoutGridTests
    {
        [Fact]
        [DisplayName("Default constructor initializes the expected default values")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var grid = new LayoutGrid();

            Assert.Equal(string.Empty, grid.TableName);
            Assert.Equal(string.Empty, grid.Caption);
            Assert.Equal(GridControlAllowActions.All, grid.AllowActions);
            Assert.Equal(FormEditModes.All, grid.AllowEditModes);
        }

        [Fact]
        [DisplayName("A non-default AllowEditModes is restored through an XML round-trip, and the default is not written")]
        public void AllowEditModes_XmlRoundTrip_PreservesValueAndOmitsDefault()
        {
            var layout = new FormLayout();
            var detail = new LayoutGrid("Orders", "訂單") { AllowEditModes = FormEditModes.Edit };
            detail.Columns!.Add(new LayoutColumn { FieldName = "qty", Caption = "Qty" });
            layout.Details!.Add(detail);
            layout.Details.Add(new LayoutGrid("Notes", "備註"));

            var xml = XmlCodec.Serialize(layout);
            Assert.Contains("AllowEditModes=\"Edit\"", xml);
            // The default (All) must not be written, so existing layout files stay untouched.
            Assert.Single(xml.Split("AllowEditModes", StringSplitOptions.None).Skip(1));

            var restored = XmlCodec.Deserialize<FormLayout>(xml);
            Assert.NotNull(restored);
            Assert.Equal(FormEditModes.Edit, restored!.Details![0].AllowEditModes);
            Assert.Equal(FormEditModes.All, restored.Details[1].AllowEditModes);
        }

        [Fact]
        [DisplayName("Parameterized constructor sets TableName and Caption")]
        public void ParameterizedConstructor_SetsProperties()
        {
            var grid = new LayoutGrid("Orders", "訂單");

            Assert.Equal("Orders", grid.TableName);
            Assert.Equal("訂單", grid.Caption);
        }

        [Fact]
        [DisplayName("ToString returns \"TableName - Caption\"")]
        public void ToString_ReturnsFormatted()
        {
            var grid = new LayoutGrid("Orders", "訂單");

            Assert.Equal("Orders - 訂單", grid.ToString());
        }

        [Fact]
        [DisplayName("Columns returns the collection instance when not serializing")]
        public void Columns_DefaultState_ReturnsCollection()
        {
            var grid = new LayoutGrid();

            Assert.NotNull(grid.Columns);
        }

        [Fact]
        [DisplayName("Columns returns null when serializing an empty collection")]
        public void Columns_EmptyDuringSerialize_ReturnsNull()
        {
            var grid = new LayoutGrid();
            grid.SetSerializeState(SerializeState.Serialize);

            Assert.Null(grid.Columns);
        }

        [Fact]
        [DisplayName("SetSerializeState sets the object's own state")]
        public void SetSerializeState_UpdatesState()
        {
            var grid = new LayoutGrid();

            grid.SetSerializeState(SerializeState.Serialize);

            Assert.Equal(SerializeState.Serialize, grid.SerializeState);
        }

        [Fact]
        [DisplayName("AllowActions can be set and read back")]
        public void AllowActions_Settable()
        {
            var grid = new LayoutGrid { AllowActions = GridControlAllowActions.None };

            Assert.Equal(GridControlAllowActions.None, grid.AllowActions);
        }
    }
}
