namespace Polhem.DefineEditor.ViewModels;

/// <summary>A command on a FormSchema that the main window carries out, since it owns the solution tree and the database tools.</summary>
public enum FormSchemaAction
{
    /// <summary>Brings the form's TableSchemas, FormLayout and table registrations up to date with it.</summary>
    SyncDefinitions,

    /// <summary>Upgrades the database tables of the form.</summary>
    UpgradeTables,

    /// <summary>Previews the upgrade of the database tables of the form.</summary>
    PreviewUpgradeTables,
}
