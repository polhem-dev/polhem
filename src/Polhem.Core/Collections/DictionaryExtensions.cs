namespace Polhem.Core.Collections
{
    /// <summary>
    /// Extension methods for dictionaries.
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Returns <paramref name="dictionary"/> itself when it already matches keys with
        /// <see cref="StringComparer.OrdinalIgnoreCase"/>; otherwise a copy that does, or an empty one for <c>null</c>.
        /// </summary>
        /// <typeparam name="TValue">The type of the values.</typeparam>
        /// <param name="dictionary">The dictionary, or <c>null</c>.</param>
        /// <returns>A dictionary that matches names such as field and table names in any case.</returns>
        /// <remarks>
        /// A property setter calls it so that a reader that replaces the dictionary, as the wire codecs do, does not
        /// lose the case-insensitive keys. A dictionary holding two keys that differ only in case cannot be copied and
        /// throws <see cref="ArgumentException"/>.
        /// </remarks>
        public static Dictionary<string, TValue> WithIgnoreCaseKeys<TValue>(this Dictionary<string, TValue>? dictionary)
            => dictionary is not null && Equals(dictionary.Comparer, StringComparer.OrdinalIgnoreCase)
                ? dictionary
                : new(dictionary ?? [], StringComparer.OrdinalIgnoreCase);
    }
}
