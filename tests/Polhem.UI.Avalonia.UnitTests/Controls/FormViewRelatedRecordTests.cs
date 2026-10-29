using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Opening a relation field's record from a viewed record on the Avalonia head: when a <see cref="ButtonEdit"/>
    /// offers it, and how a <see cref="FormView"/> answers the request: a refusal in its message bar when the user may
    /// not read the related form, otherwise a related view, said to close rather than go back, shown as a dialog.
    /// </summary>
    /// <remarks>
    /// No Avalonia platform runs, so the dialog itself is replaced: a <see cref="RelationFormView"/> overrides
    /// <c>ShowRelatedViewAsync</c> to record what it would show, and <c>CreateRelatedView</c> to return a view that
    /// reaches no registered services.
    /// </remarks>
    public class FormViewRelatedRecordTests
    {
        private const string LeaveTypeModelId = "LeaveTypeModel";
        private static readonly Guid s_annualRowId = new("dddddddd-0000-0000-0000-000000000001");
        private static readonly RelationTarget s_target = new("LeaveType", s_annualRowId);

        private static FormSchema BuildLeaveSchema()
        {
            var schema = new FormSchema("Leave", "Leave request");
            var table = schema.Tables!.Add("Leave", "Leave request");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row Id", FieldDbType.Guid));
            var leaveType = new FormField("leave_type_rowid", "Leave type", FieldDbType.Guid) { RelationProgId = "LeaveType" };
            leaveType.RelationFieldMappings!.Add(SysFields.Name, "ref_leave_type_name");
            table.Fields!.Add(leaveType);
            table.Fields!.Add(new FormField("ref_leave_type_name", "Leave type name", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        private static FormSchema BuildLeaveTypeSchema(bool permissionBound)
        {
            var schema = new FormSchema("LeaveType", "Leave type");
            if (permissionBound) { schema.PermissionModelId = LeaveTypeModelId; }
            var table = schema.Tables!.Add("LeaveType", "Leave type");
            table.Fields!.Add(new FormField(SysFields.RowId, "Row Id", FieldDbType.Guid));
            table.Fields!.Add(new FormField(SysFields.Name, "Name", FieldDbType.String));
            return schema;
        }

        // Binds a ButtonEdit to the leave request's relation field, under a parent that stands in for the view: it
        // carries the form mode and, when asked, the flag that says the request is handled.
        private static ButtonEdit BindEditor(SingleFormMode formMode, bool handled, Guid? leaveTypeRowId)
        {
            var schema = BuildLeaveSchema();
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            if (leaveTypeRowId is { } rowId)
                dataObject.SetField("leave_type_rowid", rowId.ToString());
            var layoutField = FormLayoutGenerator.Generate(schema, "default")
                .Sections![0].Fields!.First(f => f.FieldName == "leave_type_rowid");

            var editor = new ButtonEdit();
            var parent = new Border { Child = editor };
            FormScope.SetFormMode(parent, formMode);
            if (handled) RelationOpening.SetIsEnabled(parent, true);
            editor.Bind(dataObject, layoutField);
            return editor;
        }

        private static T GetPrivateField<T>(FormView view, string fieldName)
        {
            var field = typeof(FormView).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(view)!;
        }

        [Fact]
        [DisplayName("In View mode under a form that handles it, a relation field with a value offers its record and shows the hint")]
        public void ButtonEdit_ViewModeWithValue_OffersRelatedRecord()
        {
            var editor = BindEditor(SingleFormMode.View, handled: true, s_annualRowId);

            Assert.True(editor.TryGetOpenTarget(out var target));
            Assert.Equal(s_target, target);
            Assert.Equal(UIText.Get(PolhemUIText.OpenRelatedRecord), ToolTip.GetTip(editor));
        }

        [Theory]
        [InlineData(SingleFormMode.Edit, true, true)]
        [InlineData(SingleFormMode.Add, true, true)]
        [InlineData(SingleFormMode.View, false, true)]
        [InlineData(SingleFormMode.View, true, false)]
        [DisplayName("While edited, outside a form that handles it, or without a value, a relation field offers nothing")]
        public void ButtonEdit_NotOffering_ReturnsFalseWithoutHint(SingleFormMode formMode, bool handled, bool hasValue)
        {
            var editor = BindEditor(formMode, handled, hasValue ? s_annualRowId : null);

            Assert.False(editor.TryGetOpenTarget(out _));
            Assert.Null(ToolTip.GetTip(editor));
        }

        [Fact]
        [DisplayName("A user without Read on the related form sees a message naming it, and no related view is created")]
        public async Task OpenRelatedRecord_NotReadable_ShowsMessageWithoutOpening()
        {
            var view = new RelationFormView(BuildLeaveTypeSchema(permissionBound: true), PermissionActions.Create);

            await view.OpenRelatedRecordAsync(s_target);

            Assert.Empty(view.Created);
            Assert.Empty(view.Shown);
            var label = GetPrivateField<MessageBar>(view, "_errorLabel");
            Assert.True(label.IsVisible);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, UIText.Get(PolhemUIText.RelatedRecordDenied), "Leave type"),
                label.Text);
        }

        [Fact]
        [DisplayName("A user with Read on the related form's model gets a related view titled with the form's name")]
        public async Task OpenRelatedRecord_Readable_ShowsRelatedView()
        {
            var view = new RelationFormView(BuildLeaveTypeSchema(permissionBound: true), PermissionActions.Read);

            await view.OpenRelatedRecordAsync(s_target);

            var (shown, title) = Assert.Single(view.Shown);
            Assert.Equal("Leave type", title);
            Assert.Same(Assert.Single(view.Created), shown);
            Assert.Equal("LeaveType", shown.ProgId);
            Assert.Equal(SingleFormMode.View, shown.FormMode);
            Assert.Equal(UIText.Get(PolhemUIText.Close), GetPrivateField<Button>(shown, "_backButton").Content);
        }

        [Fact]
        [DisplayName("A related form without a permission model opens even when the snapshot grants nothing")]
        public async Task OpenRelatedRecord_NoPermissionModel_ShowsRelatedView()
        {
            var view = new RelationFormView(BuildLeaveTypeSchema(permissionBound: false), PermissionActions.None);

            await view.OpenRelatedRecordAsync(s_target);

            Assert.Single(view.Shown);
        }

        [Fact]
        [DisplayName("A related form whose schema cannot be found is reported in the message bar")]
        public async Task OpenRelatedRecord_MissingSchema_ReportsError()
        {
            var view = new RelationFormView(relatedSchema: null, PermissionActions.Read);

            await view.OpenRelatedRecordAsync(s_target);

            Assert.Empty(view.Shown);
            Assert.Contains("LeaveType", GetPrivateField<MessageBar>(view, "_errorLabel").Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The view answers a request raised on it by opening the related record")]
        public void RequestedEvent_RaisedOnView_OpensRelatedRecord()
        {
            var view = new RelationFormView(BuildLeaveTypeSchema(permissionBound: false), PermissionActions.None);
            var args = new RelationOpenRequestedEventArgs(s_target);

            view.RaiseEvent(args);

            Assert.True(args.Handled);
            Assert.Single(view.Shown);
        }

        private sealed class FakeUserContext(PermissionActions granted) : IUserContext
        {
            public UserInfo? User => null;
            public IReadOnlyDictionary<string, PermissionActions>? Capabilities { get; } =
                new Dictionary<string, PermissionActions>(StringComparer.Ordinal) { [LeaveTypeModelId] = granted };
            public CompanyInfo? Company => null;
            public string DefaultLanguage => "en-US";
        }

        /// <summary>
        /// A view that resolves only the related schema and the user's capabilities, and records the related views it
        /// creates and would show instead of opening a dialog.
        /// </summary>
        private sealed class RelationFormView(FormSchema? relatedSchema, PermissionActions granted) : FormView
        {
            private readonly IUserContext _userContext = new FakeUserContext(granted);

            public List<FormView> Created { get; } = [];
            public List<(FormView View, string Title)> Shown { get; } = [];

            protected override Task<FormSchema?> ResolveSchemaAsync(string progId, CancellationToken cancellationToken)
                => Task.FromResult(relatedSchema is not null && relatedSchema.ProgId == progId ? relatedSchema : null);

            protected override IFormApiConnector? ResolveFormConnector(string progId) => null;

            protected override IUserContext? ResolveUserContext() => _userContext;

            protected override FormView CreateRelatedView(FormSchema schema)
            {
                var view = new IsolatedFormView { ProgId = schema.ProgId, Schema = schema };
                Created.Add(view);
                return view;
            }

            protected override Task ShowRelatedViewAsync(FormView view, string title, CancellationToken cancellationToken = default)
            {
                Shown.Add((view, title));
                return Task.CompletedTask;
            }
        }

        // A related view that reaches no registered service: it has no connector, so its load stops at once.
        private sealed class IsolatedFormView : FormView
        {
            protected override IFormApiConnector? ResolveFormConnector(string progId) => null;

            protected override IUserContext? ResolveUserContext() => null;
        }
    }
}
