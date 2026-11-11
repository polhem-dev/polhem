using System.ComponentModel;
using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.DataObjects;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Additional coverage for <see cref="FieldEditorBinder"/>: the <c>OnFormModeChanged</c> path triggered through
    /// the FormScope property (the class handler path, as opposed to calling
    /// <c>SetControlState</c> directly), the attached layout, and a row binding when the dataset is replaced.
    /// </summary>
    public class FieldEditorBinderAdditionalTests
    {
        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add("emp_name", "Name", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        [Fact]
        [DisplayName("Setting FormScope.FormModeProperty triggers OnFormModeChanged and View mode applies read-only")]
        public void OnFormModeChanged_ViaFormScopeProperty_ViewMode_SetsReadOnly()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");
            editor.SetControlState(SingleFormMode.Edit);
            Assert.False(editor.IsReadOnly);

            // class handler: FormScope.FormModeProperty.Changed.AddClassHandler<TextEdit>(
            //   (o, e) => o._binder.OnFormModeChanged((SingleFormMode)e.NewValue!))
            FormScope.SetFormMode(editor, SingleFormMode.View);

            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("Switching FormScope.FormModeProperty to Edit mode makes the editor editable")]
        public void OnFormModeChanged_ViaFormScopeProperty_EditMode_ClearsReadOnly()
        {
            var dataObject = BuildDataObject();
            var editor = new TextEdit();
            editor.Bind(dataObject, "emp_name");
            FormScope.SetFormMode(editor, SingleFormMode.View);
            Assert.True(editor.IsReadOnly);

            FormScope.SetFormMode(editor, SingleFormMode.Edit);

            Assert.False(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("With AllowEditModes=Add the editor stays read-only after Edit mode is set through FormScope")]
        public void OnFormModeChanged_ViaFormScope_AllowEditModesAdd_EditModeReadOnly()
        {
            var dataObject = BuildDataObject();
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            var editor = new TextEdit();
            editor.Bind(dataObject, field);

            FormScope.SetFormMode(editor, SingleFormMode.Add);
            Assert.False(editor.IsReadOnly);

            FormScope.SetFormMode(editor, SingleFormMode.Edit);
            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("AttachLayout applies the layout's ReadOnly to an editor that has no data object")]
        public void AttachLayout_NoDataObject_AppliesReadOnly()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;

            binder.AttachLayout(new LayoutField { FieldName = "emp_name", ReadOnly = true });

            Assert.False(binder.IsBound);
            Assert.True(editor.IsReadOnly);
        }

        [Fact]
        [DisplayName("The attached layout survives Unbind and still decides AllowsEdit")]
        public void AttachLayout_SurvivesUnbind()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;
            var field = new LayoutField { FieldName = "emp_name", AllowEditModes = FormEditModes.Add };
            binder.AttachLayout(field);
            editor.Bind(BuildDataObject(), "emp_name");

            editor.Unbind();

            Assert.Same(field, binder.LayoutField);
            Assert.False(binder.AllowsEdit(SingleFormMode.Edit));
            Assert.True(binder.AllowsEdit(SingleFormMode.Add));
        }

        [Fact]
        [DisplayName("A bind by field name only keeps the attached layout, and an explicit layout bind wins over it")]
        public void AttachLayout_BindByNameKeepsIt_ExplicitLayoutWins()
        {
            var editor = new TextEdit();
            var binder = ((IFieldEditorBinderHost)editor).Binder;
            var attached = new LayoutField { FieldName = "emp_name", ReadOnly = true };
            var explicitField = new LayoutField { FieldName = "emp_name" };
            binder.AttachLayout(attached);

            editor.Bind(BuildDataObject(), "emp_name");
            Assert.Same(attached, binder.LayoutField);

            editor.Bind(BuildDataObject(), explicitField);
            Assert.Same(explicitField, binder.LayoutField);
        }

        [Fact]
        [DisplayName("Replacing the dataset leaves a row-bound editor alone instead of reading a row the data object no longer owns")]
        public async Task DataSetReplaced_RowBinding_DoesNotRereadTheOldRow()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add("note", "Note", FieldDbType.String);
            schema.Tables.Add("Line", "Line").Fields!.Add("item", "Item", FieldDbType.String);
            var connector = new NewDataConnector(schema);
            var dataObject = new FormDataObject(schema, connector);
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["Line"]!;
            var line = lines.NewRow();
            line["item"] = "Bolt";
            lines.Rows.Add(line);
            var editor = new TextEdit();
            editor.Bind(dataObject, new LayoutField { FieldName = "item", Caption = "Item" }, line);

            var exception = await Record.ExceptionAsync(() => dataObject.NewAsync());

            Assert.Null(exception);
            Assert.Equal("Bolt", editor.Text);
        }

        /// <summary>
        /// Serves a fresh dataset for <see cref="FormDataObject.NewAsync"/>, which replaces the data object's dataset.
        /// </summary>
        private sealed class NewDataConnector(FormSchema schema) : IFormApiConnector
        {
            public string ProgId => schema.ProgId;

            public Task<IGetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
            {
                var dataSet = new DataSet();
                foreach (var table in schema.Tables!)
                {
                    var dataTable = dataSet.Tables.Add(table.TableName);
                    foreach (var field in table.Fields!) { dataTable.Columns.Add(field.FieldName); }
                }
                dataSet.Tables[0].Rows.Add(dataSet.Tables[0].NewRow());
                return Task.FromResult<IGetNewDataResponse>(new NewData(dataSet));
            }

            public Task<IGetListResponse> GetListAsync(string selectFields = "", FilterNode? filter = null,
                SortFieldCollection? sortFields = null, PagingOptions? paging = null, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IGetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<ISaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IDeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            private sealed record NewData(DataSet? DataSet) : IGetNewDataResponse;
        }
    }
}
