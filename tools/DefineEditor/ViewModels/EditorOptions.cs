using Polhem.Core.Data;
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
    /// The grantable single actions: <c>None</c> is excluded because the editor never produces
    /// a no-op rule — adding one would just immediately fail validation.
    /// </summary>
    public static PermissionActions[] PermissionActionValues { get; } =
    {
        PermissionActions.Create,
        PermissionActions.Read,
        PermissionActions.Update,
        PermissionActions.Delete,
        PermissionActions.Print,
        PermissionActions.Export,
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
