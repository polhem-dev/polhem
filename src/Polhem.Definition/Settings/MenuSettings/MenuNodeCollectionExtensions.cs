namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Provides extension methods for <see cref="MenuNodeCollection"/>.
    /// </summary>
    /// <remarks>
    /// These are extensions rather than instance members because the collections they build are
    /// reached through nullable properties (<see cref="MenuSettings.Items"/>,
    /// <see cref="MenuFolder.Items"/>): an extension can be called on the property as is and throws
    /// <see cref="ArgumentNullException"/> when it is null. The names are not <c>Add</c>, so the
    /// single-<c>Add</c> limit of the reflection-only XmlSerializer on AOT targets does not apply to
    /// them.
    /// </remarks>
    public static class MenuNodeCollectionExtensions
    {
        /// <summary>
        /// Adds a folder to the collection.
        /// </summary>
        /// <param name="collection">The collection to add to.</param>
        /// <param name="id">The node ID.</param>
        /// <param name="caption">The caption.</param>
        public static MenuFolder AddFolder(this MenuNodeCollection? collection, string id, string caption)
        {
            ArgumentNullException.ThrowIfNull(collection);
            var folder = new MenuFolder(id, caption);
            collection.Add(folder);
            return folder;
        }

        /// <summary>
        /// Adds a program entry to the collection.
        /// </summary>
        /// <param name="collection">The collection to add to.</param>
        /// <param name="id">The node ID.</param>
        /// <param name="progId">The program ID this entry opens.</param>
        /// <param name="caption">The caption.</param>
        public static MenuEntry AddEntry(this MenuNodeCollection? collection, string id, string progId, string caption)
        {
            ArgumentNullException.ThrowIfNull(collection);
            var entry = new MenuEntry(id, progId, caption);
            collection.Add(entry);
            return entry;
        }
    }
}
