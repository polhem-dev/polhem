namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The record a relation field points at: the related form and the row of it the field's value names.
    /// </summary>
    /// <param name="ProgId">The related form, the field's <see cref="Polhem.Definition.Forms.FormField.RelationProgId"/>.</param>
    /// <param name="RowId">The related record's <see cref="Polhem.Definition.SysFields.RowId"/>.</param>
    public readonly record struct RelationTarget(string ProgId, Guid RowId);
}
