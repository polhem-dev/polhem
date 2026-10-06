using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Core.Collections;

namespace Polhem.Definition.Language
{
    /// <summary>
    /// A keyed collection of <see cref="LanguageEnum"/> instances within a
    /// <see cref="LanguageResource"/>. Lookup by enum name is O(1).
    /// </summary>
    [Description("Language enum collection.")]
    [TreeNode("Enums", true)]
    public sealed class LanguageEnumCollection : KeyCollectionBase<LanguageEnum>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LanguageEnumCollection"/>.
        /// </summary>
        public LanguageEnumCollection() { }

        /// <summary>
        /// Initializes a new instance of <see cref="LanguageEnumCollection"/> with an owning resource.
        /// </summary>
        /// <param name="resource">The owning language resource.</param>
        public LanguageEnumCollection(LanguageResource resource) : base(resource) { }
    }
}
