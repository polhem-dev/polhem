using Polhem.Core.Collections;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// A collection of field mappings.
    /// </summary>
    public sealed class FieldMappingCollection : CollectionBase<FieldMapping>
    {
    }

    /// <summary>
    /// Convenience extension methods for <see cref="FieldMappingCollection"/>.
    /// </summary>
    public static class FieldMappingCollectionExtensions
    {
        /// <summary>
        /// Adds a field mapping entry.
        /// </summary>
        /// <param name="collection">The collection to add to.</param>
        /// <param name="sourceField">The source field.</param>
        /// <param name="destinationField">The destination field.</param>
        public static FieldMapping Add(this FieldMappingCollection? collection, string sourceField, string destinationField)
        {
            ArgumentNullException.ThrowIfNull(collection);
            var field = new FieldMapping(sourceField, destinationField);
            collection.Add(field);
            return field;
        }
    }
}
