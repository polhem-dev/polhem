namespace Polhem.DefineEditor.Models;

/// <summary>
/// The views the activity bar switches the sidebar between, each a different angle on the same define files.
/// </summary>
public enum SidebarView
{
    /// <summary>The DefinePath folder as it is on disk.</summary>
    Explorer,
    /// <summary>The programs registered in ProgramSettings, each with the define files that belong to it.</summary>
    Programs,
    /// <summary>The database categories in DbCategorySettings, each with its tables.</summary>
    Databases
}
