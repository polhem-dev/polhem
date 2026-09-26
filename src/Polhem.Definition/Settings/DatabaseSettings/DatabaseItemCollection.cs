using System.ComponentModel;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of database items.
    /// </summary>
    [Description("Database item collection.")]
    [TreeNode("Databases", true)]
    public class DatabaseItemCollection : KeyCollectionBase<DatabaseItem>
    {
    }
}
