namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// What an import does with a FormLayout file that already exists.
/// </summary>
/// <remarks>
/// A FormLayout is the part people arrange by hand after it was first generated, and a form describes no arrangement,
/// so replacing it from the form would drop that work.
/// </remarks>
public enum FormLayoutImportMode
{
    /// <summary>Leaves the file as it is; new fields stay off the edit screen.</summary>
    Keep,

    /// <summary>
    /// Adds the fields and detail grids the form has and the layout lacks, each next to its neighbour on the form;
    /// changes and removes nothing (<see cref="Polhem.Definition.Layouts.FormLayoutGenerator.Merge"/>).
    /// </summary>
    Merge,

    /// <summary>Replaces the file with the layout generated from the form.</summary>
    Regenerate,
}
