namespace Polhem.Analyzers.Serialization
{
    /// <summary>
    /// Metadata names of the framework base types the wire contract rules resolve.
    /// </summary>
    /// <remarks>
    /// Resolved by metadata name rather than referenced directly: the analyzer targets netstandard2.0
    /// and cannot reference the net10.0 framework assemblies, and a consumer that uses none of these
    /// types should see no diagnostics at all rather than have the rules guess.
    /// </remarks>
    internal static class SerializationTypeNames
    {
        /// <summary>
        /// The framework base type for keyed collections.
        /// </summary>
        public const string KeyCollectionBase = "Polhem.Core.Collections.KeyCollectionBase`1";

        /// <summary>
        /// The framework base type for collections.
        /// </summary>
        public const string CollectionBase = "Polhem.Core.Collections.CollectionBase`1";

        /// <summary>
        /// The framework base type for keyed collection items.
        /// </summary>
        public const string KeyCollectionItem = "Polhem.Core.Collections.KeyCollectionItem";

        /// <summary>
        /// The framework base type for collection items.
        /// </summary>
        public const string CollectionItem = "Polhem.Core.Collections.CollectionItem";
    }
}
