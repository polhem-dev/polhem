using System.ComponentModel;
using Polhem.Base.Attributes;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// Background service parameters and environment settings.
    /// </summary>
    [Description("Background service parameters and environment settings.")]
    [TreeNode("BackgroundService")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class BackgroundServiceConfiguration
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
