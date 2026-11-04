using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// The names assistive technology reads for editors whose content is not text: the lookup button of
    /// <see cref="ButtonEdit"/> holds only a glyph, and the picker of <see cref="DateEdit"/> holds a grid of date parts,
    /// so without a name each was read as its content's type name.
    /// </summary>
    public class EditorAccessibleNameTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("hire_date", "Hire Date", FieldDbType.Date);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("The lookup button of a ButtonEdit is named Search")]
        public void ButtonEdit_LookupButton_IsNamedSearch()
        {
            var editor = new ButtonEdit();

            var button = Assert.IsType<Button>(editor.InnerRightContent);

            Assert.Equal(UIText.Get(PolhemUIText.Search), AutomationProperties.GetName(button));
        }

        [Fact]
        [DisplayName("A DateEdit is named after the caption of the field it is bound to")]
        public void DateEdit_BoundToField_IsNamedAfterCaption()
        {
            var editor = new DateEdit();

            editor.Bind(BuildDataObject(), "hire_date");

            Assert.Equal("Hire Date", AutomationProperties.GetName(editor));
        }

        [Fact]
        [DisplayName("A layout caption names a DateEdit before the schema caption")]
        public void DateEdit_LayoutCaption_WinsOverSchemaCaption()
        {
            var editor = new DateEdit();

            editor.Bind(BuildDataObject(), new LayoutField { FieldName = "hire_date", Caption = "Joined" });

            Assert.Equal("Joined", AutomationProperties.GetName(editor));
        }

        [Fact]
        [DisplayName("A name the application set on a DateEdit is kept when it binds")]
        public void DateEdit_ApplicationName_IsKept()
        {
            var editor = new DateEdit();
            AutomationProperties.SetName(editor, "Start of employment");

            editor.Bind(BuildDataObject(), "hire_date");

            Assert.Equal("Start of employment", AutomationProperties.GetName(editor));
        }
    }
}
