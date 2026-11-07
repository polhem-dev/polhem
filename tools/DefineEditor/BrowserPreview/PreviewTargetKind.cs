namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>What kind of part of a previewed form a <see cref="PreviewTarget"/> names.</summary>
public enum PreviewTargetKind
{
    /// <summary>A master section; <see cref="PreviewTarget.Index"/> is its position among the sections.</summary>
    Section,

    /// <summary>
    /// A field; <see cref="PreviewTarget.Index"/> is its section's position and <see cref="PreviewTarget.Item"/> its
    /// position among the section's visible fields.
    /// </summary>
    Field,

    /// <summary>A detail grid; <see cref="PreviewTarget.Index"/> is its position among the details.</summary>
    Grid,

    /// <summary>
    /// A detail grid's column; <see cref="PreviewTarget.Index"/> is the grid's position and
    /// <see cref="PreviewTarget.Item"/> the column's position among the grid's visible columns.
    /// </summary>
    Column,
}
