namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// What <see cref="UserQueryCodec.Deserialize"/> read from a stored user query.
    /// </summary>
    public sealed class UserQueryLoadResult
    {
        internal UserQueryLoadResult(QueryCriteria criteria, IReadOnlyList<UserQuerySkip> skipped, bool isSupported)
        {
            Criteria = criteria;
            Skipped = skipped;
            IsSupported = isSupported;
        }

        /// <summary>
        /// Gets a value indicating whether the stored text could be read at all. It is <see langword="false"/> when
        /// the text is not a user query, or was written in a format version this version does not know; then
        /// <see cref="Criteria"/> is empty.
        /// </summary>
        public bool IsSupported { get; }

        /// <summary>Gets the condition rows that could be read, all <see cref="CriteriaRowKind.Optional"/>.</summary>
        public QueryCriteria Criteria { get; }

        /// <summary>Gets the stored rows that were left out, in their stored order.</summary>
        public IReadOnlyList<UserQuerySkip> Skipped { get; }
    }
}
