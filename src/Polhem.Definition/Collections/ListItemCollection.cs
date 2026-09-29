using Polhem.Core.Collections;

namespace Polhem.Definition.Collections
{
    /// <summary>
    /// List item collection.
    /// </summary>
    public sealed class ListItemCollection : KeyCollectionBase<ListItem>
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="ListItemCollection"/>.
        /// </summary>
        public ListItemCollection()
        { }

        #endregion
    }

    /// <summary>
    /// Extension methods for <see cref="ListItemCollection"/>.
    /// </summary>
    public static class ListItemCollectionExtensions
    {
        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        /// <param name="collection">The collection to add to.</param>
        /// <param name="value">The item value.</param>
        /// <param name="text">The display text.</param>
        public static ListItem Add(this ListItemCollection? collection, string value, string text)
        {
            ArgumentNullException.ThrowIfNull(collection);
            var item = new ListItem(value, text);
            collection.Add(item);
            return item;
        }
    }
}
