using System.ComponentModel;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Layouts
{
    /// <summary>
    /// A collection of layout sections.
    /// </summary>
    [Description("Layout section collection.")]
    [TreeNode("Sections", false)]
    public class LayoutSectionCollection : CollectionBase<LayoutSection>
    {
    }
}
