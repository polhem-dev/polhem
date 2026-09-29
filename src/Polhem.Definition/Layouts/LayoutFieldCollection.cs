using System.ComponentModel;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// A collection of layout fields within a <see cref="LayoutSection"/>.
    /// </summary>
    [Description("Layout field collection.")]
    [TreeNode("Fields", false)]
    public sealed class LayoutFieldCollection : CollectionBase<LayoutField>
    {
    }
}
