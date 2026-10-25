using System.ComponentModel;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests the edit permission rule of <see cref="FieldEditRules"/>.
    /// </summary>
    public class FieldEditRulesTests
    {
        [Fact]
        [DisplayName("View mode never allows editing")]
        public void AllowsEdit_ViewMode_ReturnsFalse()
        {
            Assert.False(FieldEditRules.AllowsEdit(SingleFormMode.View, null));
            Assert.False(FieldEditRules.AllowsEdit(SingleFormMode.View, new LayoutField { FieldName = "a" }));
        }

        [Fact]
        [DisplayName("A read-only layout field never allows editing")]
        public void AllowsEdit_ReadOnlyField_ReturnsFalse()
        {
            Assert.False(FieldEditRules.AllowsEdit(SingleFormMode.Edit, new LayoutField { FieldName = "a", ReadOnly = true }));
        }

        [Fact]
        [DisplayName("AllowEditModes restricts editing to the listed modes")]
        public void AllowsEdit_AllowEditModesAddOnly_AllowsAddNotEdit()
        {
            var field = new LayoutField { FieldName = "a", AllowEditModes = FormEditModes.Add };

            Assert.True(FieldEditRules.AllowsEdit(SingleFormMode.Add, field));
            Assert.False(FieldEditRules.AllowsEdit(SingleFormMode.Edit, field));
        }

        [Fact]
        [DisplayName("A grid column follows the mode alone")]
        public void AllowsEdit_GridColumn_FollowsMode()
        {
            var column = new LayoutColumn { FieldName = "a" };

            Assert.True(FieldEditRules.AllowsEdit(SingleFormMode.Edit, column));
            Assert.False(FieldEditRules.AllowsEdit(SingleFormMode.View, column));
        }
    }
}
