using System.ComponentModel;
using Polhem.Base.Attributes;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Website parameters and environment settings.
    /// </summary>
    [Description("Website parameters and environment settings.")]
    [TreeNode("Website")]
    public sealed class WebsiteConfiguration
    {
        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
