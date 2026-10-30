namespace Polhem.Repository
{
    /// <summary>
    /// Computes the number of rows a page-based query skips.
    /// </summary>
    internal static class PagingOffset
    {
        /// <summary>
        /// Returns the rows to skip for <paramref name="page"/>. A page whose offset does not fit an <see cref="int"/>
        /// resolves to the largest usable offset, so the query still returns the columns but finds no rows.
        /// </summary>
        /// <param name="page">The 1-based page index, already clamped to at least 1.</param>
        /// <param name="pageSize">The page size, already clamped to the framework cap.</param>
        /// <remarks>
        /// The bound leaves room for the page and its probe row, so adding either to the offset cannot overflow.
        /// </remarks>
        public static int Compute(int page, int pageSize)
            => (int)Math.Min((page - 1L) * pageSize, (long)int.MaxValue - pageSize - 1);
    }
}
