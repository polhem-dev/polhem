using Blazor.Fluent.DemoCenter.Modules.ControlTypes;
using Blazor.Fluent.DemoCenter.Modules.DataBinding;
using Blazor.Fluent.DemoCenter.Modules.FormModes;
using Blazor.Fluent.DemoCenter.Modules.Grids;
using Blazor.Fluent.DemoCenter.Modules.Layouts;
using Blazor.Fluent.DemoCenter.Modules.Lookup;
using Blazor.Fluent.DemoCenter.Modules.MasterDetail;
using Blazor.Fluent.DemoCenter.Modules.Permissions;
using Blazor.Fluent.DemoCenter.Modules.ReadOnlyRequired;
using Blazor.Fluent.DemoCenter.Modules.WithoutBackend;
using Polhem.Definition.Numbers;

namespace Blazor.Fluent.DemoCenter.Modules;

/// <summary>
/// Every demo case, in navigation order. The topics, titles and component names match the modules of
/// <c>samples/Avalonia.DemoCenter</c>, so a control's behavior can be compared case by case.
/// </summary>
public static class DemoModuleRegistry
{
    /// <summary>Gets the registered cases.</summary>
    public static IReadOnlyList<DemoModule> Modules { get; } =
    [
        new("Control Types", "Control gallery",
            "One derived control per ControlType, bound through FormScope. Text controls update the values when you leave them; drop-downs, check boxes and pickers update them on selection.",
            typeof(ControlGalleryModule)),
        new("Control Types", "Native vs derived (BindFieldControl)",
            "Field editors (IFieldEditor) side by side with the Fluent UI inputs they wrap, with a normal and a read-only row for each ControlType.",
            typeof(FieldControlComparisonModule)),
        new("Control Types", "Native vs derived (BindTableControl)",
            "GridControl (IBindTableControl) side by side with a plain FluentDataGrid, in the in-cell and EditForm edit modes.",
            typeof(TableControlComparisonModule)),

        new("Data Binding", "Ambient binding",
            "The container sets the data object once with FormScope. Child editors only set FieldName and bind by themselves.",
            typeof(AmbientBindingModule)),
        new("Data Binding", "Explicit binding",
            "editor.Bind(dataObject, layoutField) binds directly, without a scope around the editor.",
            typeof(ExplicitBindingModule)),
        new("Data Binding", "Two-way sync",
            "Two controls bound to the same field: leave one (or press Enter) to commit, and the other and the values below follow. FormDataObject is the single source.",
            typeof(TwoWaySyncModule)),
        new("Data Binding", "DataObject events",
            "A live log of FieldValueChanged, RowAdded, RowDeleted, IsDirtyChanged and DataSetReplaced.",
            typeof(DataObjectEventsModule)),

        new("Read-only & Required", "LayoutField.ReadOnly",
            "LayoutField.ReadOnly makes a field read-only whatever the form mode.",
            typeof(ReadOnlyFieldModule)),
        new("Read-only & Required", "Required / read-only markers",
            "GridControl marks a column's state in its header: a read-only caption is parenthesised and a required one shows the required cue.",
            typeof(RequiredReadOnlyMarkModule)),

        new("FormMode States", "Interactive switching",
            "A FormMode drop-down drives the read-only or editable state of these controls and the detail grid live.",
            typeof(InteractiveFormModeModule)),
        new("FormMode States", "Controls × the three FormModes (with AllowEditModes)",
            "Three columns pinned to View, Add and Edit, with the same fields and different AllowEditModes: how each control renders per mode and how editing is gated per field.",
            typeof(FormModeStatesModule)),
        new("FormMode States", "Grid × FormMode",
            "The same GridControl in the three modes: View is read-only with the toolbar hidden; Add and Edit are editable and show it.",
            typeof(GridFormModeModule)),

        new("Lookup", "ButtonEdit lookup picker",
            "The magnifier raises ButtonClick for a local picker, or opens LookupDialog for a field with a RelationProgId, here over an in-memory source.",
            typeof(LookupPickerModule)),

        new("Layout", "FormLayout generated at design time",
            "FormLayoutGenerator.Generate produces sections and field placement from the schema, as a design-time starting point.",
            typeof(AutoFormLayoutModule)),
        new("Layout", "Multi-column layout (ColumnCount / ColumnSpan)",
            "ColumnCount=2 lays the fields out in two columns, and the notes field spans the row with ColumnSpan=2.",
            typeof(MultiColumnLayoutModule)),

        new("Grid", "In-cell editing",
            "GridControl InCell mode: a cell opens its editor when it takes the focus; Enter or leaving the cell commits, Escape drops what was typed. Built-in add and delete toolbar.",
            typeof(InCellEditModule)),
        new("Grid", "EditForm dialog",
            "The grid is read-only; a dialog edits the whole row and buffers the edits until OK.",
            typeof(EditFormModule)),
        new("Grid", "Ambient binding",
            "Only TableName and the columns are set: the grid binds to the detail table through FormScope.",
            typeof(AmbientGridModule)),
        new("Grid", "List mode (read-only list)",
            "GridControl over a standalone DataTable: read-only, no toolbar, columns from a LayoutGrid.",
            typeof(ListModeModule)),
        new("Grid", "Number formatting",
            "Each column resolves its decimals from its NumberKind; quantity and weight follow the row's unit. Switching the company applies its overridden decimals live.",
            typeof(NumberFormatModule)),
        new("Grid", "Multi-currency amounts",
            "Amount decimals follow the row's currency (USD 2 / JPY 0 / BHD 3). The original-currency total shows only for a single currency; the home-currency total always shows.",
            typeof(MultiCurrencyModule)),
        new("Grid", "Multi-unit quantities",
            "Quantity decimals follow the row's unit (PCS 0 / KG 3 / M 2). The total shows only when every row has the same unit.",
            typeof(MultiUnitModule)),

        new("Master-Detail", "Master + detail",
            "A master section and a detail grid over one FormDataObject, with a computed line amount. Switch the detail editing model or the form mode.",
            typeof(MasterDetailModule)),

        new("Permission Capability", "Interactive permission simulator (master/detail)",
            "Tick simulated role grants, the snapshot EnterCompany would return: toolbar commands, the sensitive master field and the sensitive detail column degrade live. Without the capability the snapshot is null, which allows everything.",
            typeof(PermissionCapabilityModule)),

        new("Without a Backend", "List and record over your own data",
            "A list and a record over an in-memory IFormApiConnector, assembled from GridControl and FormLayoutPresenter. FormPage itself needs a PolhemApiClient, so it is not used here.",
            typeof(WithoutBackendModule)),
    ];
}
