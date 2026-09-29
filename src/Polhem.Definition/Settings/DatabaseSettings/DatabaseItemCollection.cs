using System.ComponentModel;
using Polhem.Core.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of database items.
    /// </summary>
    [Description("Database item collection.")]
    [TreeNode("Databases", true)]
    public sealed class DatabaseItemCollection : KeyCollectionBase<DatabaseItem>
    {
    }
}
