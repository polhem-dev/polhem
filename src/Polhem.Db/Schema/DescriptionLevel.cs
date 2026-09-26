namespace Polhem.Db.Schema
{
    /// <summary>
    /// Represents the metadata level of a description drift.
    /// </summary>
    public enum DescriptionLevel
    {
        /// <summary>
        /// Table-level description (sourced from <see cref="Polhem.Definition.Database.TableSchema.DisplayName"/>).
        /// </summary>
        Table,

        /// <summary>
        /// Column-level description (sourced from <see cref="Polhem.Definition.Database.DbField.Caption"/>).
        /// </summary>
        Column,
    }
}
