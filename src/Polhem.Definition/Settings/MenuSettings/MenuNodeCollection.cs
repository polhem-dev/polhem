using System.ComponentModel;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of menu nodes. Folders and entries share this one collection type and
    /// therefore one key space.
    /// </summary>
    [Description("Menu node collection.")]
    [TreeNode("Items", false)]
    public sealed class MenuNodeCollection : KeyCollectionBase<MenuNodeBase>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MenuNodeCollection"/>.
        /// </summary>
        /// <remarks>
        /// Required by XmlSerializer's reflection-only deserialization path (AOT targets such as iOS
        /// create the collection via the public parameterless constructor).
        /// </remarks>
        public MenuNodeCollection() : base()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="MenuNodeCollection"/>.
        /// </summary>
        /// <param name="owner">The owning <see cref="MenuSettings"/> or <see cref="MenuFolder"/>.</param>
        public MenuNodeCollection(object owner) : base(owner)
        { }

        /// <summary>
        /// Returns the visible nodes in display order (ascending <see cref="MenuNodeBase.Order"/>,
        /// ties keeping document order).
        /// </summary>
        /// <remarks>
        /// A shell builds its menu from this rather than from raw enumeration, so ordering and the
        /// <see cref="MenuNodeBase.Visible"/> switch are applied identically on every UI head. This
        /// applies the design-time switch only — per-user permission filtering is a separate
        /// concern the caller layers on top.
        /// </remarks>
        public IEnumerable<MenuNodeBase> GetDisplayNodes()
            => this.Where(node => node.Visible).OrderBy(node => node.Order);
    }
}
