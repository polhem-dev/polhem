namespace Polhem.UI.Core.Criteria
{
    /// <summary>
    /// The condition rows a user built for a query, independent of the backend method that runs it. A converter such
    /// as <see cref="GetListFilterConverter"/> turns them into what that method takes.
    /// </summary>
    /// <remarks>
    /// Rows are combined with AND, and the same field may appear in more than one row. Several values in one
    /// <see cref="CriteriaOperator.In"/> row match any of them, so OR within a field needs no group. A list query starts
    /// with no rows; a report starts with its <see cref="CriteriaRowKind.Required"/> and
    /// <see cref="CriteriaRowKind.Default"/> rows.
    /// </remarks>
    public sealed class QueryCriteria
    {
        private readonly List<CriteriaRow> _rows = [];

        /// <summary>Gets the rows, in the order they were added.</summary>
        public IReadOnlyList<CriteriaRow> Rows => _rows;

        /// <summary>Gets the number of rows that have something entered and therefore filter.</summary>
        public int ActiveCount => _rows.Count(r => r.HasValue);

        /// <summary>Gets the required rows that have nothing entered.</summary>
        public IEnumerable<CriteriaRow> MissingRequired => _rows.Where(r => r.Kind == CriteriaRowKind.Required && !r.HasValue);

        /// <summary>Gets a value indicating whether every required row has something entered, so the query can run.</summary>
        public bool IsComplete => !MissingRequired.Any();

        /// <summary>
        /// Adds a condition row.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="operator">The operator; <c>null</c> uses <see cref="CriteriaField.DefaultOperator"/>.</param>
        /// <param name="kind">Why the row is added.</param>
        /// <param name="lockOperator">Whether the user may not change the operator.</param>
        /// <returns>The new row, for its value to be set.</returns>
        /// <exception cref="ArgumentException">The field does not offer the operator.</exception>
        public CriteriaRow Add(CriteriaField field, CriteriaOperator? @operator = null,
            CriteriaRowKind kind = CriteriaRowKind.Optional, bool lockOperator = false)
        {
            ArgumentNullException.ThrowIfNull(field);
            var row = new CriteriaRow(field, @operator ?? field.DefaultOperator, kind, lockOperator);
            _rows.Add(row);
            return row;
        }

        /// <summary>
        /// Removes a row. A required row is never removed.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns><see langword="true"/> when the row was removed.</returns>
        public bool Remove(CriteriaRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return row.Kind != CriteriaRowKind.Required && _rows.Remove(row);
        }

        /// <summary>
        /// Removes every row that is not required, and clears what was entered in the required ones.
        /// </summary>
        public void Clear()
        {
            _rows.RemoveAll(r => r.Kind != CriteriaRowKind.Required);
            foreach (var row in _rows)
                row.ClearValue();
        }
    }
}
