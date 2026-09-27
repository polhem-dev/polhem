using System.ComponentModel;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// A collection of layout grids (detail tables under a <see cref="FormLayout"/>).
    /// </summary>
    [Description("Layout grid collection.")]
    [TreeNode("Details", false)]
    public sealed class LayoutGridCollection : CollectionBase<LayoutGrid>
    {
    }
}
