namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// What the application that drafts a form supplies: the forms that already exist, so a field can be matched to one,
    /// and the validation of the result against its own form conventions.
    /// </summary>
    public interface IFormDraftingHost
    {
        /// <summary>
        /// Gets the existing forms a drafted field may pick a record from.
        /// </summary>
        /// <returns>Each form with its names and the fields of its master table.</returns>
        IReadOnlyList<DraftCatalogEntry> GetCatalog();

        /// <summary>
        /// Gets the fields of an existing form's master table.
        /// </summary>
        /// <param name="progId">The ProgId of the form.</param>
        /// <returns>The fields, or an empty list when no such form exists.</returns>
        IReadOnlyList<DraftCatalogField> GetFields(string progId);

        /// <summary>
        /// Checks a converted draft the way the host checks a form it is about to import.
        /// </summary>
        /// <param name="result">The converted draft.</param>
        /// <returns>The problems found; an empty list when there are none.</returns>
        IReadOnlyList<DraftIssue> Validate(FormDraftResult result);
    }
}
