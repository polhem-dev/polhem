using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Static enum sources for ComboBox bindings across every editor in the app.
/// Exposed as static arrays so axaml can bind via <c>x:Static</c> without
/// reaching through a view-model instance. Merged from the previous
/// SingletonEditorOptions / FormSchemaEditorOptions split (which duplicated
/// <c>ControlTypes</c> / <c>FieldDbTypes</c> under different names).
/// </summary>
public static class EditorOptions
{
    /// <summary>
    /// PermissionAction excludes <c>None</c> because the editor never produces
    /// a no-op rule — adding one would just immediately fail validation.
    /// </summary>
    public static PermissionAction[] PermissionActions { get; } =
    {
        PermissionAction.Create,
        PermissionAction.Read,
        PermissionAction.Update,
        PermissionAction.Delete,
        PermissionAction.Print,
        PermissionAction.Export,
    };

    public static ScopeStrategy[] ScopeStrategies { get; } = Enum.GetValues<ScopeStrategy>();
    public static DatabaseType[] DatabaseTypes { get; } = Enum.GetValues<DatabaseType>();
    public static FieldDbType[] FieldDbTypes { get; } = Enum.GetValues<FieldDbType>();
    public static FieldType[] FieldTypes { get; } = Enum.GetValues<FieldType>();
    public static ControlType[] ControlTypes { get; } = Enum.GetValues<ControlType>();
    public static ScopeRole[] ScopeRoles { get; } = Enum.GetValues<ScopeRole>();
    public static SortDirection[] SortDirections { get; } = Enum.GetValues<SortDirection>();
    public static GridControlAllowActions[] GridAllowActions { get; } = Enum.GetValues<GridControlAllowActions>();
}
