using Polhem.Base.Attributes;
using System.ComponentModel;

namespace Polhem.Definition.Logging
{
    /// <summary>
    /// Logging options for controlling whether each module logs information.
    /// </summary>
    [Description("Logging options for controlling whether each module logs information.")]
    [TreeNode("Logging")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class LogOptions
    {
        /// <summary>
        /// Logging options for the DbAccess module.
        /// </summary>
        [Description("Logging options for the DbAccess module.")]
        public DbAccessAnomalyLogOptions DbAccess { get; set; } = new DbAccessAnomalyLogOptions();

        /// <summary>
        /// Object description.
        /// </summary>
        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
