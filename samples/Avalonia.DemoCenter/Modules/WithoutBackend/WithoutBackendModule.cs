using System.Data;
using Avalonia.Controls;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core;

namespace Avalonia.DemoCenter.Modules.WithoutBackend
{
    /// <summary>
    /// The list and record views with no Polhem backend: the schema and layout are built in code and the data
    /// comes from an <see cref="InMemoryFormApiConnector"/>. Nothing is registered in <c>UIServices</c>; the host
    /// sets <c>Schema</c>, <c>Layout</c> and <c>FormConnector</c> on the views itself.
    /// </summary>
    public sealed class WithoutBackendModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Without a Backend";

        /// <inheritdoc/>
        public override string Title => "List and record over your own data";

        /// <inheritdoc/>
        public override string Description =>
            "ListView and FormView driven by a FormSchema, a FormLayout and an in-memory IFormApiConnector. No Polhem "
            + "backend and no API client: the application implements the connector over its own data source.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = BuildSchema();
            var connector = new InMemoryFormApiConnector(schema, SeedRows(schema));
            var layout = FormLayoutGenerator.Generate(schema, schema.ProgId);

            var host = new ContentControl();
            var list = new ListView { Schema = schema, FormConnector = connector };
            var record = new FormView { Schema = schema, Layout = layout, FormConnector = connector };

            void ShowList()
            {
                host.Content = list;
                _ = list.ReloadAsync();
            }

            async Task ShowRecordAsync(Func<FormView, Task> open)
            {
                host.Content = record;
                await open(record).ConfigureAwait(true);
            }

            list.ViewRequested += (_, rowId) => _ = ShowRecordAsync(view => view.ViewAsync(rowId));
            list.EditRequested += (_, rowId) => _ = ShowRecordAsync(view => view.EditAsync(rowId));
            list.AddRequested += (_, _) => _ = ShowRecordAsync(view => view.NewAsync());
            record.Saved += (_, _) => ShowList();
            record.Closed += (_, _) => ShowList();

            host.Content = list;
            return host;
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Staff", "Staff") { ListFields = "emp_code,emp_name,dept,hire_date,is_active" };
            var master = schema.Tables!.Add("Staff", "Staff");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("emp_code", "Code", FieldDbType.String);
            master.Fields.Add("emp_name", "Name", FieldDbType.String);
            var dept = master.Fields.Add("dept", "Department", FieldDbType.String);
            dept.ListItems!.Add("HR", "Human Resources");
            dept.ListItems.Add("IT", "Information Technology");
            dept.ListItems.Add("FIN", "Finance");
            master.Fields.Add("hire_date", "Hire Date", FieldDbType.Date);
            master.Fields.Add("is_active", "Active", FieldDbType.Boolean);
            return schema;
        }

        private static DataTable SeedRows(FormSchema schema)
        {
            // The store has exactly the columns the views build from the schema.
            var rows = FormValueBinding.BuildEmptyDataSet(schema).Tables[schema.ProgId]!;
            AddRow(rows, "EMP-001", "Alice Chen", "IT", new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Unspecified), true);
            AddRow(rows, "EMP-002", "Bob Liu", "HR", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), true);
            AddRow(rows, "EMP-003", "Carol Wang", "FIN", new DateTime(2024, 11, 20, 0, 0, 0, DateTimeKind.Unspecified), false);
            rows.AcceptChanges();
            return rows;
        }

        private static void AddRow(DataTable rows, string code, string name, string dept, DateTime hired, bool active)
        {
            var row = rows.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["emp_code"] = code;
            row["emp_name"] = name;
            row["dept"] = dept;
            row["hire_date"] = hired;
            row["is_active"] = active;
            rows.Rows.Add(row);
        }
    }
}
