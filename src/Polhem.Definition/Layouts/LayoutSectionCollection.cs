using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// A collection of layout sections.
    /// </summary>
    [Description("Layout section collection.")]
    [TreeNode("Sections", false)]
    public sealed class LayoutSectionCollection : CollectionBase<LayoutSection>
    {
    }
}
