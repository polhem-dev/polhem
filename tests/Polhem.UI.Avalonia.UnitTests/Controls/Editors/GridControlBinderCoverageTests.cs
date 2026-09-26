using System.ComponentModel;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Covers the ambient binding path of <see cref="GridControlBinder"/>: it calls NotifyAttached / NotifyDetached through
    /// reflection on the private _binder of <see cref="GridControl"/>,
    /// simulating how OnAttachedToLogicalTree / OnDetachedFromLogicalTree trigger them.
    /// It also verifies that OnBindingContextChanged rebinds when the DataObject changes after attaching.
    /// </summary>
    public class GridControlBinderCoverageTests
    {
        private static FormDataObject BuildDataObjectWithDetail(string phone = "02-1234")
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("EmployeePhone", "Phones");
            detail.Fields!.Add("phone", "Phone", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.DataSet.Tables["EmployeePhone"]!.Rows.Add(phone);
            return dataObject;
        }

        private static object GetBinder(GridControl grid)
        {
            var field = typeof(GridControl).GetField("_binder", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return field!.GetValue(grid)!;
        }

        private static void InvokeNotifyAttached(object binder)
        {
            var method = binder.GetType().GetMethod("NotifyAttached");
            Assert.NotNull(method);
            method!.Invoke(binder, null);
        }

        private static void InvokeNotifyDetached(object binder)
        {
            var method = binder.GetType().GetMethod("NotifyDetached");
            Assert.NotNull(method);
            method!.Invoke(binder, null);
        }

        private static object? GetBinderDataObject(object binder)
        {
            var prop = binder.GetType().GetProperty("DataObject");
            Assert.NotNull(prop);
            return prop!.GetValue(binder);
        }

        [Fact]
        [DisplayName("NotifyAttached binds the grid and loads the detail table when the ambient DataObject and TableName are set")]
        public void NotifyAttached_WithAmbientDataObjectAndTableName_BindsGridToDetailTable()
        {
            var dataObject = BuildDataObjectWithDetail("02-1234");
            var grid = new GridControl();
            grid.TableName = "EmployeePhone";
            FormScope.SetDataObject(grid, dataObject);

            InvokeNotifyAttached(GetBinder(grid));

            Assert.NotNull(grid.DataTable);
            Assert.Equal("EmployeePhone", grid.DataTable!.TableName);
        }

        [Fact]
        [DisplayName("NotifyAttached does not bind when the ambient DataObject is not set, and DataTable stays null")]
        public void NotifyAttached_NoAmbientDataObject_DoesNotBind()
        {
            var grid = new GridControl();
            grid.TableName = "EmployeePhone";

            InvokeNotifyAttached(GetBinder(grid));

            Assert.Null(grid.DataTable);
        }

        [Fact]
        [DisplayName("After NotifyDetached on an ambient binding, the binder releases the DataObject")]
        public void NotifyDetached_AfterAmbientBind_ClearsBinderDataObject()
        {
            var dataObject = BuildDataObjectWithDetail("02-1234");
            var grid = new GridControl();
            grid.TableName = "EmployeePhone";
            FormScope.SetDataObject(grid, dataObject);
            var binder = GetBinder(grid);
            InvokeNotifyAttached(binder);
            Assert.NotNull(grid.DataTable);

            InvokeNotifyDetached(binder);

            Assert.Null(GetBinderDataObject(binder));
        }

        [Fact]
        [DisplayName("OnBindingContextChanged rebinds the grid to the new DataObject's detail table when it changes after attaching")]
        public void OnBindingContextChanged_AfterAttach_NewDataObject_Rebinds()
        {
            var dataObject1 = BuildDataObjectWithDetail("phone-1");
            var dataObject2 = BuildDataObjectWithDetail("phone-2");

            var grid = new GridControl();
            grid.TableName = "EmployeePhone";
            FormScope.SetDataObject(grid, dataObject1);
            InvokeNotifyAttached(GetBinder(grid));
            Assert.Same(dataObject1.DataSet.Tables["EmployeePhone"], grid.DataTable);

            // Changing the DataObject fires the `DataObjectProperty.Changed` class handler, which goes through `OnBindingContextChanged` and `TryAmbientBind` to rebind.
            FormScope.SetDataObject(grid, dataObject2);

            Assert.Same(dataObject2.DataSet.Tables["EmployeePhone"], grid.DataTable);
        }

        [Fact]
        [DisplayName("OnBindingContextChanged does not bind before attaching (_attached=false), and DataTable stays null")]
        public void OnBindingContextChanged_NotAttached_IsNoOp()
        {
            var dataObject = BuildDataObjectWithDetail("02-1234");
            var grid = new GridControl();
            grid.TableName = "EmployeePhone";

            // Without `NotifyAttached`, `_attached` is false: the class handler fires but `OnBindingContextChanged` returns early.
            FormScope.SetDataObject(grid, dataObject);

            Assert.Null(grid.DataTable);
        }
    }
}
