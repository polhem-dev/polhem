using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of database category definitions.
    /// </summary>
    [Description("Database category collection.")]
    [TreeNode("Database Categories", false)]
    public sealed class DbCategoryCollection : KeyCollectionBase<DbCategory>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DbCategoryCollection"/>.
        /// </summary>
        /// <remarks>
        /// Required by XmlSerializer's reflection-only deserialization path (AOT targets such as iOS
        /// create the collection via the public parameterless constructor).
        /// </remarks>
        public DbCategoryCollection() : base()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="DbCategoryCollection"/>.
        /// </summary>
        /// <param name="settings">The owning database category settings.</param>
        public DbCategoryCollection(DbCategorySettings settings) : base(settings)
        { }
    }
}
