using System.Data;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.DataObjects;

namespace Avalonia.DemoCenter.Modules.Views
{
    /// <summary>
    /// Builds the shared Employee (master) + Phones (detail) schema and in-memory fake
    /// data used by the Grid / Views scenarios. The <see cref="FormDataObject"/> acts as
    /// the view-model: no backend connector is involved — the data is seeded locally and
    /// the front-end binds straight to it.
    /// </summary>
    internal static class SampleFormData
    {
        /// <summary>
        /// Builds a master-only Employee <see cref="FormSchema"/> (no detail), for the Layout
        /// scenarios. Includes a memo field so column-span layout can be shown.
        /// </summary>
        public static FormSchema BuildMasterFormSchema()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_code", "Code", FieldDbType.String);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);
            master.Fields.Add("notes", "Notes", FieldDbType.String);
            return schema;
        }

        /// <summary>
        /// Builds a master-only data object (from <paramref name="schema"/>) seeded with one
        /// employee.
        /// </summary>
        public static FormDataObject BuildMasterForm(FormSchema schema)
        {
            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("emp_code", "EMP-001");
            data.SetField("emp_name", "Alice Chen");
            data.SetField("dept", "IT");
            data.SetField("hire_date", "2026-06-11");
            data.SetField("is_active", bool.TrueString);
            data.SetField("notes", "Some notes");
            return data;
        }

        /// <summary>
        /// The Phones detail layout used by the Grid / Master-Detail scenarios.
        /// </summary>
        public static LayoutGrid BuildPhonesLayout()
        {
            var layout = new LayoutGrid("Phones", "Phones");
            layout.Columns!.Add(new LayoutColumn("phone", "Number", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("type", "Type", ControlType.DropDownEdit));
            layout.Columns.Add(new LayoutColumn("is_primary", "Primary", ControlType.CheckEdit));
            layout.Columns.Add(new LayoutColumn("valid_from", "Valid From", ControlType.DateEdit));
            return layout;
        }

        /// <summary>
        /// Builds the Employee + Phones <see cref="FormSchema"/> (master-detail).
        /// </summary>
        public static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Employee", "Employee");

            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_code", "Code", FieldDbType.String);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);

            var phones = schema.Tables.Add("Phones", "Phones");
            phones.Fields!.Add("phone", "Number", FieldDbType.String);
            var phoneType = phones.Fields.Add("type", "Type", FieldDbType.String);
            phoneType.ListItems!.Add("Office", "Office");
            phoneType.ListItems.Add("Mobile", "Mobile");
            phoneType.ListItems.Add("Home", "Home");
            phones.Fields.Add("is_primary", "Primary", FieldDbType.Boolean);
            phones.Fields.Add("valid_from", "Valid From", FieldDbType.Date);

            return schema;
        }

        /// <summary>
        /// Builds a master-detail data object (from <paramref name="schema"/>) seeded with
        /// one employee and two phone rows. A fresh instance keeps each consuming view
        /// isolated.
        /// </summary>
        public static FormDataObject BuildMasterDetail(FormSchema schema)
        {
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.SetField("emp_code", "EMP-001");
            dataObject.SetField("emp_name", "Alice Chen");
            dataObject.SetField("hire_date", "2026-06-11");
            dataObject.SetField("dept", "IT");
            dataObject.SetField("is_active", bool.TrueString);

            var phones = dataObject.DataSet.Tables["Phones"]!;
            phones.Rows.Add("02-1234-5678", "Office", true,
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified));
            phones.Rows.Add("0912-345-678", "Mobile", false,
                new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified));
            return dataObject;
        }

        /// <summary>
        /// The list layout (one row per employee) used by the ListView scenario.
        /// </summary>
        public static LayoutGrid BuildEmployeeListLayout()
        {
            var layout = new LayoutGrid("Employee", "Employee list");
            layout.Columns!.Add(new LayoutColumn("emp_code", "Code", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("emp_name", "Name", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("dept", "Department", ControlType.DropDownEdit));
            layout.Columns.Add(new LayoutColumn("hire_date", "Hire Date", ControlType.DateEdit));
            layout.Columns.Add(new LayoutColumn("is_active", "Active", ControlType.CheckEdit));
            return layout;
        }

        /// <summary>
        /// A standalone table of employees (list-mode rows), matching
        /// <see cref="BuildEmployeeListLayout"/>'s columns.
        /// </summary>
        public static DataTable BuildEmployeeListTable()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("emp_code", typeof(string));
            table.Columns.Add("emp_name", typeof(string));
            table.Columns.Add("dept", typeof(string));
            table.Columns.Add("hire_date", typeof(DateTime));
            table.Columns.Add("is_active", typeof(bool));

            table.Rows.Add("EMP-001", "Alice Chen", "IT", new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Unspecified), true);
            table.Rows.Add("EMP-002", "Bob Liu", "HR", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), true);
            table.Rows.Add("EMP-003", "Carol Wang", "FIN", new DateTime(2024, 11, 20, 0, 0, 0, DateTimeKind.Unspecified), false);
            return table;
        }
    }
}
