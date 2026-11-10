using Polhem.Core.Collections;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Describes a JOIN relationship between two tables.
    /// </summary>
    public sealed class TableJoin : KeyCollectionItem
    {
        /// <summary>
        /// Gets or sets the unique key identifying the source of this JOIN relationship.
        /// </summary>
        public override string Key
        {
            get { return base.Key; }
            set { base.Key = value; }
        }

        /// <summary>
        /// Gets or sets the type of JOIN operation.
        /// </summary>
        public JoinType JoinType { get; set; } = JoinType.Left;

        /// <summary>
        /// Gets or sets the left-side table name.
        /// </summary>
        public string LeftTable { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the left-side table alias.
        /// </summary>
        public string LeftAlias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the left-side column name.
        /// </summary>
        public string LeftField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the right-side table name.
        /// </summary>
        public string RightTable { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the right-side table alias.
        /// </summary>
        public string RightAlias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the right-side column name.
        /// </summary>
        public string RightField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the join also requires the right-side row to belong to the same
        /// company as the main row: both tables carry <see cref="Polhem.Definition.SysFields.CompanyId"/>,
        /// and the join matches it to the main table's (alias <c>A</c>).
        /// </summary>
        /// <remarks>
        /// A foreign key holds a row id, which a caller can set to a row of another company sharing
        /// the database. Matching the company keeps such a row from being read through the join.
        /// </remarks>
        public bool MatchCompany { get; set; }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            var joinKeyword = JoinType.ToString().ToUpperInvariant() + " JOIN";
            return $"{joinKeyword} {RightTable} {RightAlias} ON {LeftAlias}.{LeftField} = {RightAlias}.{RightField}";
        }
    }

}
