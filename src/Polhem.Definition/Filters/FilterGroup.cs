using System.ComponentModel;
using System.Xml.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Definition.Filters
{
    /// <summary>
    /// A filter condition group that combines multiple nodes with AND/OR logic.
    /// </summary>
    public sealed class FilterGroup : FilterNode
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FilterGroup"/>.
        /// </summary>
        public FilterGroup()
        {
            Nodes = [];
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FilterGroup"/>.
        /// </summary>
        /// <param name="operator">The logical operator for the group.</param>
        public FilterGroup(LogicalOperator @operator)
        {
            Operator = @operator;
            Nodes = [];
        }

        /// <summary>
        /// Gets the node kind.
        /// </summary>
        public override FilterNodeKind Kind { get { return FilterNodeKind.Group; } }

        /// <summary>
        /// Gets or sets the logical operator for this group.
        /// </summary>
        public LogicalOperator Operator { get; set; }

        /// <summary>
        /// Gets or sets the child node collection.
        /// </summary>
        [XmlArrayItem(typeof(FilterCondition))]
        [XmlArrayItem(typeof(FilterGroup))]
        [JsonConverter(typeof(FilterNodeCollectionJsonConverter))]
        public FilterNodeCollection Nodes { get; set; }

        /// <summary>
        /// Gets whether <see cref="Nodes"/> is written; <c>false</c> while it is empty. <c>XmlSerializer</c>
        /// and <see cref="Polhem.Core.Serialization.JsonCodec"/> read this <c>{Property}Specified</c> member.
        /// </summary>
        [XmlIgnore, JsonIgnore]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool NodesSpecified => Nodes is { Count: > 0 };

        /// <summary>
        /// Creates an AND group.
        /// </summary>
        /// <param name="nodes">The member nodes.</param>
        public static FilterGroup All(params FilterNode[] nodes)
        {
            var g = new FilterGroup();
            g.Operator = LogicalOperator.And;
            if (nodes != null) g.Nodes.AddRange(nodes);
            return g;
        }

        /// <summary>
        /// Creates an OR group.
        /// </summary>
        /// <param name="nodes">The member nodes.</param>
        public static FilterGroup Any(params FilterNode[] nodes)
        {
            var g = new FilterGroup();
            g.Operator = LogicalOperator.Or;
            if (nodes != null) g.Nodes.AddRange(nodes);
            return g;
        }
    }
}
