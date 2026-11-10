using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Core.DataObjects;

namespace Blazor.Fluent.DemoCenter.Modules.DataEditors;

/// <summary>Builds the small data objects the binding cases share.</summary>
internal static class DataEditorParts
{
    /// <summary>Builds a data object with one master table carrying a single field.</summary>
    public static FormDataObject SingleField(string fieldName, string caption, FieldDbType dbType, string? initialValue = null)
    {
        var schema = new FormSchema("Demo", "Demo");
        schema.Tables!.Add("Demo", "Demo").Fields!.Add(fieldName, caption, dbType);
        var data = new FormDataObject(schema);
        data.InitializeNewMaster();
        if (initialValue is not null)
            data.SetField(fieldName, initialValue);
        return data;
    }

    /// <summary>Adds the department field with its drop-down list items.</summary>
    public static void AddDepartment(FormFieldCollection fields, string fieldName = "dept")
    {
        var dept = fields.Add(fieldName, "Department", FieldDbType.String);
        dept.ListItems!.Add("HR", "Human Resources");
        dept.ListItems.Add("IT", "Information Technology");
        dept.ListItems.Add("FIN", "Finance");
    }
}
