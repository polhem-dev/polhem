using System.ComponentModel;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests <see cref="GridColumnRules"/>: the minimum width a grid column keeps on every UI head.
    /// </summary>
    public class GridColumnRulesTests
    {
        [Fact]
        [DisplayName("GetMinWidth returns the layout width when the column sets one")]
        public void GetMinWidth_LayoutWidthSet_ReturnsLayoutWidth()
        {
            var column = new LayoutColumn("quantity", "Quantity", ControlType.NumericEdit) { Width = 80 };

            Assert.Equal(80, GridColumnRules.GetMinWidth(column));
        }

        [Fact]
        [DisplayName("GetMinWidth returns DefaultMinWidth when the column sets no width")]
        public void GetMinWidth_NoLayoutWidth_ReturnsDefault()
        {
            var column = new LayoutColumn("quantity", "Quantity", ControlType.NumericEdit);

            Assert.Equal(GridColumnRules.DefaultMinWidth, GridColumnRules.GetMinWidth(column));
        }
    }
}
