using System.ComponentModel;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of table items in a database category.
    /// </summary>
    [Description("Table item collection.")]
    [TreeNode("Tables", false)]
    public sealed class TableItemCollection : KeyCollectionBase<TableItem>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableItemCollection"/>.
        /// </summary>
        /// <remarks>
        /// Required by XmlSerializer's reflection-only deserialization path (AOT targets such as iOS
        /// create the collection via the public parameterless constructor).
        /// </remarks>
        public TableItemCollection() : base()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="TableItemCollection"/>.
        /// </summary>
        /// <param name="category">The owning database category.</param>
        public TableItemCollection(DbCategory category) : base(category)
        { }
    }
}
