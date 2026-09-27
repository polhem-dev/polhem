using System.ComponentModel;
using Polhem.Base.Attributes;
using Polhem.Base.Collections;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A collection of database server configurations.
    /// </summary>
    [Description("Database server collection.")]
    [TreeNode("Servers", true)]
    public sealed class DatabaseServerCollection : KeyCollectionBase<DatabaseServer>
    {
    }
}
