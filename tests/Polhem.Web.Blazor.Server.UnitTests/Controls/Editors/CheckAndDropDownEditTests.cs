using System.ComponentModel;
using Bunit;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls.Editors;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// Behavior tests for <see cref="CheckEdit"/> and <see cref="DropDownEdit"/>.
    /// </summary>
    public class CheckAndDropDownEditTests : BunitContext
    {
        private const string FlagField = "is_active";
        private const string StatusField = "status";

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var fields = schema.Tables!.Add("Employee", "Employee").Fields!;
            fields.Add(FlagField, "Active", FieldDbType.Boolean);
            var status = fields.Add(StatusField, "Status", FieldDbType.String);
            status.ListItems!.Add("A", "Active");
            status.ListItems.Add("I", "Inactive");
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Checking the box writes True to the field")]
        public void CheckEdit_Change_WritesTrue()
        {
            var dataObject = BuildDataObject();
            var cut = Render<CheckEdit>(p => p.Add(c => c.FieldName, FlagField).Add(c => c.DataObject, dataObject));

            cut.Find("input[type='checkbox']").Change(true);

            Assert.Equal(bool.TrueString, dataObject.GetField(FlagField));
        }

        [Fact]
        [DisplayName("The check box is disabled in view mode")]
        public void CheckEdit_ViewMode_IsDisabled()
        {
            var dataObject = BuildDataObject();

            var cut = Render<FormScope>(p => p
                .Add(c => c.DataObject, dataObject)
                .Add(c => c.FormMode, SingleFormMode.View)
                .AddChildContent<CheckEdit>(e => e.Add(c => c.FieldName, FlagField)));

            Assert.True(cut.Find("input[type='checkbox']").HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Choosing an item writes its value, and the empty option is gone once a value matches")]
        public void DropDownEdit_Change_WritesValue()
        {
            var dataObject = BuildDataObject();
            var cut = Render<DropDownEdit>(p => p.Add(c => c.FieldName, StatusField).Add(c => c.DataObject, dataObject));
            Assert.Equal(3, cut.FindAll("option").Count);

            cut.Find("select").Change("I");

            Assert.Equal("I", dataObject.GetField(StatusField));
            Assert.Equal(2, cut.FindAll("option").Count);
        }

        [Fact]
        [DisplayName("The read-only state shows the chosen item's text")]
        public void DropDownEdit_ReadOnly_ShowsItemText()
        {
            var dataObject = BuildDataObject();
            dataObject.SetField(StatusField, "A");

            var cut = Render<DropDownEdit>(p => p
                .Add(c => c.LayoutField, new LayoutField { FieldName = StatusField, ReadOnly = true })
                .Add(c => c.DataObject, dataObject));

            Assert.Empty(cut.FindAll("select"));
            Assert.Equal("Active", cut.Find("input.polhem-field-editor--select").GetAttribute("value"));
        }
    }
}
